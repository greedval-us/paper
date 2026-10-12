using System;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class ContentConfigurationBoundaryIntegrationTests
    {
        [TestCase(false, "ordinary")] [TestCase(false, "memory")] [TestCase(false, "store")]
        [TestCase(true, "ordinary")] [TestCase(true, "memory")] [TestCase(true, "store")]
        public void MissingOrInvalidConfigurationRejectsPreparationBeforeAnyProfileIo(bool invalidAsset, string mode)
        {
            var host = new GameObject("Unpublished configuration boundary"); host.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            var store = new CountingStore();
            try
            {
                var run = host.AddComponent<RunController>();
                if (invalidAsset) { run.balanceAsset = asset; asset.baseHealth = float.NaN; }
                ArgumentException error;
                if (mode == "ordinary") error = Assert.Catch<ArgumentException>(() => run.Initialize());
                else if (mode == "memory") error = Assert.Catch<ArgumentException>(() => run.InitializeWorkshop(false));
                else error = Assert.Catch<ArgumentException>(() => run.InitializeWorkshop(store));
                Assert.That(error.Message, Does.Contain(invalidAsset ? "baseHealth" : "balanceAsset"));
                Assert.That(store.Loads, Is.Zero); Assert.That(store.Saves, Is.Zero);
                Assert.That(run.Session, Is.Null); Assert.That(run.Workshop, Is.Null); Assert.That(run.Content, Is.Null);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void FailedNextPreparationLeavesThePublishedWorkshopAndProfileUntouched()
        {
            var host = new GameObject("Previously published configuration boundary"); host.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            var initial = new CountingStore(); var rejected = new CountingStore();
            try
            {
                var run = host.AddComponent<RunController>(); run.balanceAsset = asset;
                run.InitializeWorkshop(initial);
                var workshop = run.Workshop; var session = run.Session; var content = run.Content;
                string firstPage = workshop.Album[0].Id; int gold = workshop.Gold;
                asset.baseHealth = float.NaN;
                Assert.Catch<ArgumentException>(() => run.InitializeWorkshop(rejected));
                Assert.That(run.StartRun(out string reason), Is.False);
                Assert.That(reason, Does.Contain("baseHealth"));
                Assert.That(run.Session, Is.SameAs(session)); Assert.That(run.Workshop, Is.SameAs(workshop));
                Assert.That(run.Content, Is.SameAs(content)); Assert.That(run.InWorkshop, Is.True);
                Assert.That(run.Workshop.Album[0].Id, Is.EqualTo(firstPage)); Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
                Assert.That(initial.Loads, Is.EqualTo(1)); Assert.That(initial.Saves, Is.Zero);
                Assert.That(rejected.Loads, Is.Zero); Assert.That(rejected.Saves, Is.Zero);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); }
        }

        sealed class CountingStore : IWorkshopProfileStore
        {
            public int Loads, Saves;
            public ProfileLoadResult Load()
            {
                Loads++;
                var profile = new WorkshopProfile(); profile.GrantGold(87);
                return new ProfileLoadResult(profile, ProfileLoadSource.Primary, "", true);
            }
            public ProfileSaveResult Save(WorkshopProfile profile) { Saves++; return new ProfileSaveResult(true, ""); }
        }
    }
}
