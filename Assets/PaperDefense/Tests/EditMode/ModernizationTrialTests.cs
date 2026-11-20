using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ModernizationTrialTests
    {
        static AlbumEntry Entry => AssemblyResolverFixture.Catalog().Starters[0];
        static GameBalance Rules()
        {
            var rules = GameBalance.CreateDefault();
            rules.Mechanics.FieldModernization = true;
            rules.Mechanics.MaximumModernizationRank = 3;
            rules.Trials.MaximumTicks = 120;
            rules.Trials.CloseTowerX = 20;
            rules.Trials.For(TrialScenario.Crowd).Health = 10000;
            return rules;
        }

        [Test] public void DefaultPreviewIsBaselineAndExplicitSetupChangesActualDamageCycleRangeAndHealth()
        {
            var rules = Rules(); var entry = Entry;
            var baseline = AssemblyTrialRunner.Run(entry, rules, AssemblyTrialScenario.Crowd, 100);
            var setup = new TrialModernizationConfiguration(new ModernizationRanks(power: 1, mechanics: 1, range: 1, resilience: 1));
            var upgraded = AssemblyTrialRunner.Run(entry, rules, AssemblyTrialScenario.Crowd, setup, 100);
            Assert.That(baseline.Copies, Is.EqualTo(2));
            Assert.That(upgraded.Copies, Is.EqualTo(1), "Paid ranks participate in the lane copy budget.");
            var original = baseline.Frames[0].Towers[0]; var tower = upgraded.Frames[0].Towers[0];
            Assert.That(original.Modernization.StepsUsed, Is.Zero);
            Assert.That(tower.ResolvedEntry.Weapons[0].Damage, Is.GreaterThan(original.ResolvedEntry.Weapons[0].Damage));
            Assert.That(tower.ResolvedEntry.Weapons[0].Interval, Is.LessThan(original.ResolvedEntry.Weapons[0].Interval));
            Assert.That(tower.ResolvedEntry.Weapons[0].Range, Is.GreaterThan(original.ResolvedEntry.Weapons[0].Range));
            Assert.That(tower.MaxHealth, Is.GreaterThan(original.MaxHealth));
            var singleBaseline = TrialRunner.Run(entry, rules, TrialScenario.Crowd, 40);
            Assert.That(upgraded.Damage, Is.GreaterThan(singleBaseline.Damage), "Measured WeaponSystem damage must use the effective entry.");
            Assert.That(upgraded.Frames.SelectMany(f => f.Shots).Count(), Is.GreaterThan(singleBaseline.Frames.SelectMany(f => f.Shots).Count()));
            // Report resolution and lane simulation own independent catalog snapshots.
            // Modernization preserves their recipe values, not cross-run object identity.
            Assert.That(tower.ResolvedEntry.Snapshot.BalanceRevision, Is.EqualTo(upgraded.Resolved.Snapshot.BalanceRevision));
            Assert.That(tower.ResolvedEntry.Snapshot.Recipe.Parts, Is.EqualTo(upgraded.Resolved.Snapshot.Recipe.Parts));
            Assert.That(tower.ResolvedEntry.Snapshot.Recipe.Parts, Is.EqualTo(entry.Snapshot.Recipe.Parts));
            Assert.That(tower.ResolvedEntry.Snapshot.Definitions.SelectMany(d => d.Modifiers.Select(m =>
                    (d.Id, d.Revision, m.Stat, m.Operation, m.Value))),
                Is.EqualTo(entry.Snapshot.Definitions.SelectMany(d => d.Modifiers.Select(m =>
                    (d.Id, d.Revision, m.Stat, m.Operation, m.Value)))));
            Assert.That(rules.Mechanics.MaximumModernizationRank, Is.EqualTo(3));
        }

        [Test] public void EveryRecordedFrameOwnsRanksColorAndExplicitTechnology()
        {
            var setup = new TrialModernizationConfiguration(new ModernizationRanks(power: 2, color: InkColor.Blue, colorRank: 2), 2);
            var report = TrialRunner.Run(Entry, Rules(), TrialScenario.Crowd, setup, 100);
            Assert.That(report.Modernization.MaximumRank, Is.EqualTo(2));
            foreach (var frame in report.Frames)
            {
                Assert.That(frame.Modernization, Is.SameAs(setup));
                foreach (var tower in frame.Towers)
                {
                    Assert.That(tower.Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(2));
                    Assert.That(tower.Modernization.ColorRank, Is.EqualTo(2));
                    Assert.That(tower.Color, Is.EqualTo(InkColor.Blue));
                }
            }
        }

        [Test] public void RangeActuallyReachesAnOtherwiseUnreachableTargetAndHealthActuallyDelaysDestruction()
        {
            var rules = Rules(); var entry = Entry; var resolved = TowerCatalog.ResolveEntry(entry, rules).Value;
            rules.Trials.SpawnYVariation = 0; rules.Trials.For(TrialScenario.Crowd).Count = 1;
            rules.Trials.For(TrialScenario.Crowd).Speed = .0001f;
            rules.Trials.SpawnX = rules.Trials.CloseTowerX + .5f - resolved.Weapons[0].Range * 1.18f;
            var baseline = TrialRunner.Run(entry, rules, TrialScenario.Crowd, 40);
            var reached = TrialRunner.Run(entry, rules, TrialScenario.Crowd,
                new TrialModernizationConfiguration(new ModernizationRanks(range: 3), 3), 100);
            Assert.That(baseline.Damage, Is.Zero);
            Assert.That(reached.Damage, Is.GreaterThan(0), "Range must change actual acquisition and firing.");

            rules.Trials.SpawnX = rules.Trials.CloseTowerX - 1;
            rules.Trials.CounterfireDamage = 30; rules.Trials.CounterfireInterval = .2f;
            baseline = TrialRunner.Run(entry, rules, TrialScenario.Crowd, 40, counterfire: true);
            var armored = TrialRunner.Run(entry, rules, TrialScenario.Crowd,
                new TrialModernizationConfiguration(new ModernizationRanks(resilience: 3), 3), 90, counterfire: true);
            var baselineDeath = baseline.Frames.SelectMany(f => f.Events).First(e => e.Kind == SimulationEventKind.StructureDestroyed);
            var armoredDeath = armored.Frames.SelectMany(f => f.Events).First(e => e.Kind == SimulationEventKind.StructureDestroyed);
            Assert.That(armoredDeath.SimulationSeconds, Is.GreaterThan(baselineDeath.SimulationSeconds),
                "Resilience must affect real enemy counterfire, not only report values.");
        }

        [Test] public void RejectsIllegalCapsTechnologyAndNonTowerEvenWhenBudgetCannotBuildAnything()
        {
            Assert.Throws<ArgumentException>(() => new ModernizationRanks(3, 3));
            Assert.Throws<ArgumentException>(() => new ModernizationRanks(2, 2, 2, 1));
            Assert.Throws<ArgumentException>(() => new TrialModernizationConfiguration(new ModernizationRanks(power: 2), 1));
            Assert.Throws<ArgumentException>(() => new TrialModernizationConfiguration(new ModernizationRanks(color: InkColor.Red, colorRank: 3), 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrialModernizationConfiguration(default, 4));
            Assert.Throws<ArgumentException>(() => new TrialModernizationConfiguration(new ModernizationRanks(color: InkColor.Red)));
            var support = AssemblyResolverFixture.Catalog().Starters.First(e => e.Kind == AlbumEntryKind.Structure);
            Assert.Throws<ArgumentException>(() => AssemblyTrialRunner.Run(support, Rules(), AssemblyTrialScenario.Crowd,
                new TrialModernizationConfiguration(new ModernizationRanks(power: 1)), 0));
        }
    }
}
