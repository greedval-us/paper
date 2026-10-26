using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Presentation;

namespace PaperDefense.Tests
{
    public sealed class AlbumReleaseProbeTests
    {
        static Type Probe
        {
            get
            {
                var type = typeof(PlayerGameRoot).Assembly.GetType("PaperDefense.Presentation.AlbumReleaseProbe");
                Assert.That(type, Is.Not.Null, "An opt-in built-player acceptance probe must exist.");
                return type;
            }
        }

        static string Validate(params string[] arguments)
            => (string)Probe.GetMethod("ValidateArguments", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { arguments });

        [Test]
        public void ProbeRequiresExplicitProfileIsolationBeforeRunningAnyJourney()
        {
            Assert.That(Validate("game.exe", "-paper-release-probe", "report.json"), Is.Not.Null.And.Not.Empty);
            Assert.That(Validate("game.exe", "-paper-release-probe", "report.json", "-paper-memory-only"), Is.Null);
        }

        [Test]
        public void ProbeAcceptsOnlyTemporaryStoreAndRejectsPrefixSibling()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "PaperDefense-release", "profile.json");
            string sibling = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "-personal/profile.json";
            Assert.That(Validate("game.exe", "-paper-release-probe", "report.json", "-paper-profile-path", temporary), Is.Null);
            Assert.That(Validate("game.exe", "-paper-release-probe", "report.json", "-paper-profile-path", sibling), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ProbeRejectsMissingOutputAndAmbiguousDuplicateArguments()
        {
            Assert.That(Validate("game.exe", "-paper-release-probe", "-paper-memory-only"), Is.Not.Null.And.Not.Empty);
            Assert.That(Validate("game.exe", "-paper-release-probe", "a.json", "-paper-release-probe", "b.json", "-paper-memory-only"), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void MainThreadAllocationReaderMustPassARealAllocationSanityCheck()
        {
            var factory = Probe.GetMethod("CreateVerifiedAllocationReader", BindingFlags.Public | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null, "The release allocation fallback must verify that the runtime counter actually measures allocations.");
            var reader = (Func<long>)factory.Invoke(null, null);
            bool supported;
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var allocation = new byte[8192];
                long measured = GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(allocation);
                supported = measured >= 8192;
            }
            catch (Exception error) when (error is NotSupportedException || error is NotImplementedException)
            { supported = false; }
            if (!supported) { Assert.That(reader, Is.Null, "Unsupported or non-counting runtimes must report unavailable."); return; }
            Assert.That(reader, Is.Not.Null);
            long start = reader();
            var verified = new byte[16384];
            long delta = reader() - start;
            GC.KeepAlive(verified);
            Assert.That(delta, Is.GreaterThanOrEqualTo(16384), "The reader must measure real main-thread allocations.");
        }
    }
}
