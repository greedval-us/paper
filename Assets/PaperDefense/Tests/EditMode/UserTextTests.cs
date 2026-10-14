using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class UserTextTests
    {
        // Reflection keeps the red test executable while the new Core API is absent.
        static Type Api => typeof(TowerBlueprint).Assembly.GetType("PaperDefense.Core.UserText");
        static Type ResolverApi => typeof(TowerBlueprint).Assembly.GetType("PaperDefense.Core.UserTextResolver");
        static string Message(string key, params object[] args)
        {
            Assert.That(Api, Is.Not.Null, "Semantic player text API has not been implemented.");
            return (string)Api.GetMethod("Message").Invoke(null, new object[] { key, args });
        }
        static object Resolver(Dictionary<string, string> entries)
        {
            Assert.That(ResolverApi, Is.Not.Null, "Immutable text resource snapshot has not been implemented.");
            return Activator.CreateInstance(ResolverApi, entries, null);
        }
        static string Resolve(object resolver, string value) => (string)ResolverApi.GetMethod("Resolve").Invoke(resolver, new object[] { value });

        [Test]
        public void SnapshotSubstitutesResourceAndFormatsNumbersInRussianCulture()
        {
            string token = Message("test.cost", 12.5, 1234);
            var snapshot = Resolver(new Dictionary<string, string> { { "test.cost", "Цена {0:0.0}; {1:00000}" } });
            Assert.That(Resolve(snapshot, token), Is.EqualTo("Цена 12,5; 01234"));
        }

        [Test]
        public void TokensKeepNumericTypesIndependentOfCurrentCultureAndResolveNestedReasons()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US");
                string token = Message("test.reason", Message("test.capacity", 2.75m));
                var snapshot = Resolver(new Dictionary<string, string> { { "test.reason", "Причина: {0}" }, { "test.capacity", "{0:0.00}" } });
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                Assert.That(Resolve(snapshot, token), Is.EqualTo("Причина: 2,75"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void SnapshotCopiesResourcesAndDoesNotRewritePlayerNames()
        {
            var entries = new Dictionary<string, string> { { "test.title", "Сохранён «{0}»" } };
            var first = Resolver(entries);
            entries["test.title"] = "Sentinel {0}";
            var second = Resolver(entries);
            string token = Message("test.title", "Моя башня {0}");
            Assert.That(Resolve(first, token), Is.EqualTo("Сохранён «Моя башня {0}»"));
            Assert.That(Resolve(second, token), Is.EqualTo("Sentinel Моя башня {0}"));
        }

        [Test]
        public void MissingKeysAndWrongArgumentCountsProduceVisibleDiagnostics()
        {
            var snapshot = Resolver(new Dictionary<string, string> { { "test.two", "{0}: {1}" } });
            Assert.That(Resolve(snapshot, Message("test.absent")), Is.EqualTo("[missing:test.absent]"));
            Assert.That(Resolve(snapshot, Message("test.two", "one")), Is.EqualTo("[invalid:test.two]"));
        }

        [Test]
        public void EmbeddedTokensResolveWithoutReformattingLiteralBraces()
        {
            var snapshot = Resolver(new Dictionary<string, string> { { "test.value", "{0,5:0.0}" } });
            Assert.That(Resolve(snapshot, "{saved} / " + Message("test.value", 1.5f) + "!"), Is.EqualTo("{saved} /   1,5!"));
        }

        [Test]
        public void ExactArgumentCountAndFormattedArgumentTypesAreChecked()
        {
            var snapshot = Resolver(new Dictionary<string, string> { { "test.number", "{0:N2}" }, { "test.none", "Ready" } });
            Assert.That(Resolve(snapshot, Message("test.number", "not a number")), Is.EqualTo("[invalid:test.number]"));
            Assert.That(Resolve(snapshot, Message("test.number", 1, 2)), Is.EqualTo("[invalid:test.number]"));
            Assert.That(Resolve(snapshot, Message("test.none", 1)), Is.EqualTo("[invalid:test.none]"));
        }

        [Test]
        public void OverrideCompatibilityRejectsChangedArityAndFormattedArgumentContract()
        {
            var canonical = new UserTextResolver(new Dictionary<string, string> { { "test", "{0:0.0}" } });
            var differentArity = new UserTextResolver(new Dictionary<string, string> { { "test", "{0:0.0} {1}" } });
            var differentType = new UserTextResolver(new Dictionary<string, string> { { "test", "{0}" } });
            var compatible = new UserTextResolver(new Dictionary<string, string> { { "test", "Sentinel {0:N2}" } });
            Assert.Throws<ArgumentException>(() => differentArity.RequireCompatible(canonical));
            Assert.Throws<ArgumentException>(() => differentType.RequireCompatible(canonical));
            Assert.DoesNotThrow(() => compatible.RequireCompatible(canonical));
        }

        [Test]
        public void GeneratedContractsRejectMissingKeyInTheCanonicalResourceItself()
        {
            const string removedKey = "copy.159b7c81e8a7413c";
            var entries = new List<KeyValuePair<string, string>>();
            foreach (var pair in TestText.Resources.Entries) if (pair.Key != removedKey) entries.Add(pair);
            var incomplete = new UserTextResolver(entries);
            var error = Assert.Throws<ArgumentException>(() => UserTextContracts.Validate(incomplete));
            Assert.That(error.Message, Does.Contain(removedKey));
            Assert.DoesNotThrow(() => UserTextContracts.Validate(TestText.Resources));
        }

        [Test]
        public void InvalidFormatIsRejectedBeforeCreatingAResourceSnapshot()
        {
            Assert.Throws<FormatException>(() => new UserTextResolver(new Dictionary<string, string>
            {
                { "test.quality", "Quality {0:Q}" }
            }));
            var canonical = new UserTextResolver(new Dictionary<string, string> { { "test.quality", "{0:0}" } });
            var integerOnly = new UserTextResolver(new Dictionary<string, string> { { "test.quality", "{0:D}" } });
            Assert.Throws<ArgumentException>(() => integerOnly.RequireCompatible(canonical));
        }

        [Test]
        public void ValidNumericDateAndUnformattedDomainArgumentsRemainSupported()
        {
            var snapshot = new UserTextResolver(new Dictionary<string, string>
            {
                { "test.numeric", "{0:F2} / {1:N0}" },
                { "test.date", "{0:yyyy-MM-dd} / {0:O}" },
                { "test.domain", "{0} / {1}" }
            });
            var date = new DateTime(2026, 10, 12, 13, 14, 15, DateTimeKind.Utc);
            Assert.That(snapshot.Resolve(UserText.Message("test.numeric", 12.5, 3)), Is.EqualTo("12,50 / 3"));
            Assert.That(snapshot.Resolve(UserText.Message("test.date", date)), Is.EqualTo("2026-10-12 / 2026-10-12T13:14:15.0000000Z"));
            var cell = new Cell(3, 4);
            Assert.That(snapshot.Resolve(UserText.Message("test.domain", cell, ChassisKind.Heavy)), Is.EqualTo(cell + " / Heavy"));
        }
    }
}
