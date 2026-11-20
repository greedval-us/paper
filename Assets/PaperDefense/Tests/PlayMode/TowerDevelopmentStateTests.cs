#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class TowerDevelopmentStateTests
    {
        static AlbumEntry Entry => AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
            "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot().Starters[0];

        static GameSession Session(int maximumRank = 3, bool enabled = true)
        {
            var rules = GameBalance.CreateDefault();
            rules.Mechanics.FieldModernization = enabled;
            rules.Mechanics.MaximumModernizationRank = maximumRank;
            rules.StartingInk = 10000; rules.PlanningSeconds = 3600; rules.PassiveInkPerSecond = 0;
            return new GameSession(rules, starterDefenses: false);
        }

        static StructureState Build(GameSession session, bool complete = true)
        {
            var entry = TowerCatalog.ResolveEntry(Entry, session.Balance).Value;
            var cell = new Cell(4, 4);
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            if (complete) session.Tick(entry.BuildSeconds + .1f);
            return session.StructureAt(cell);
        }

        static StructureView View(GameSession session, StructureState tower) => new RunReadModel(session).StructureAt(tower.Cell).Value;
        static int[] Colors(GameSession session) => (int[])typeof(GameSession).GetField("coloredInk", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        static void SetInk(GameSession session, int ink) => typeof(GameSession).GetProperty("Ink").SetValue(session, ink);
        static void Buy(GameSession session, StructureState tower, ModernizationDirection direction)
        {
            var quote = session.QuoteModernization(tower.Id, direction);
            Assert.That(session.TryModernize(quote, out var reason), Is.True, reason);
            session.Tick(quote.WorkSeconds + .1f);
        }

        static TowerDevelopmentState AssertMatchesQuotes(GameSession session, StructureState tower)
        {
            var run = new RunReadModel(session); var view = View(session, tower);
            var result = TowerDevelopmentState.Evaluate(run, view);
            bool ordinary = new[] { ModernizationDirection.Power, ModernizationDirection.Mechanics, ModernizationDirection.Range, ModernizationDirection.Resilience }
                .Any(direction => run.QuoteModernization(tower.Id, direction).Valid);
            var colors = tower.Color == InkColor.None ? new[] { InkColor.Red, InkColor.Blue, InkColor.Green } : new[] { tower.Color };
            bool color = colors.Any(candidate => run.QuoteColorModernization(tower.Id, candidate).Valid);
            Assert.That(result.HasOrdinaryUpgrade, Is.EqualTo(ordinary));
            Assert.That(result.HasColorUpgrade, Is.EqualTo(color));
            Assert.That(result.CanImprove, Is.EqualTo(ordinary || color));
            Assert.That(result.StepsUsed, Is.EqualTo(view.Modernization.StepsUsed));
            Assert.That(result.Color, Is.EqualTo(view.Color));
            Assert.That(result.ColorRank, Is.EqualTo(view.Modernization.ColorRank));
            Assert.That(result.IsWorking, Is.EqualTo(view.Modernization.IsWorking));
            Assert.That(result.IsComplete, Is.EqualTo(view.IsComplete));
            return result;
        }

        [TestCase(1000, 0, true, false)]
        [TestCase(0, 1000, false, true)]
        [TestCase(0, 0, false, false)]
        [TestCase(1000, 1000, true, true)]
        public void OrdinaryAndColorAvailabilityFollowActualIndependentResourceQuotes(int ink, int coloredInk, bool ordinary, bool color)
        {
            var session = Session(); var tower = Build(session);
            SetInk(session, ink); Colors(session)[(int)InkColor.Blue] = coloredInk;
            var result = AssertMatchesQuotes(session, tower);
            Assert.That(result.HasOrdinaryUpgrade, Is.EqualTo(ordinary));
            Assert.That(result.HasColorUpgrade, Is.EqualTo(color));
        }

        [Test] public void ClosedTechnologyDoesNotAdvertiseAnotherOrdinaryRank()
        {
            var session = Session(1); var tower = Build(session);
            foreach (var direction in new[] { ModernizationDirection.Power, ModernizationDirection.Mechanics, ModernizationDirection.Range, ModernizationDirection.Resilience }) Buy(session, tower, direction);
            var result = AssertMatchesQuotes(session, tower);
            Assert.That(result.StepsUsed, Is.EqualTo(4));
            Assert.That(result.CanImprove, Is.False);
        }

        [Test] public void SixOrdinaryStepsStillAllowAnAffordableFirstColor()
        {
            var session = Session(); var tower = Build(session);
            foreach (var direction in new[] { ModernizationDirection.Power, ModernizationDirection.Power, ModernizationDirection.Power,
                ModernizationDirection.Mechanics, ModernizationDirection.Mechanics, ModernizationDirection.Range }) Buy(session, tower, direction);
            Colors(session)[(int)InkColor.Red] = 1000;
            var result = AssertMatchesQuotes(session, tower);
            Assert.That(result.StepsUsed, Is.EqualTo(6));
            Assert.That(result.HasOrdinaryUpgrade, Is.False);
            Assert.That(result.HasColorUpgrade, Is.True);
        }

        [Test] public void FixedColorNeedsItsOwnStockAndRetainsItsCompletedRank()
        {
            var session = Session(); var tower = Build(session);
            Colors(session)[(int)InkColor.Red] = 1000;
            var quote = session.QuoteColorModernization(tower.Id, InkColor.Red);
            Assert.That(session.TryModernize(quote, out var reason), Is.True, reason);
            session.Tick(quote.WorkSeconds + .1f);
            Colors(session)[(int)InkColor.Red] = 0; Colors(session)[(int)InkColor.Blue] = 1000;
            var result = AssertMatchesQuotes(session, tower);
            Assert.That(result.Color, Is.EqualTo(InkColor.Red));
            Assert.That(result.ColorRank, Is.EqualTo(1));
            Assert.That(result.HasColorUpgrade, Is.False, "Another color's stock cannot replace the fixed color.");
            Colors(session)[(int)InkColor.Red] = 1000;
            Assert.That(AssertMatchesQuotes(session, tower).HasColorUpgrade, Is.True);
        }

        [Test] public void BuildingAndWorkingTowersCannotAdvertiseAnotherPurchase()
        {
            var session = Session(); var tower = Build(session, complete: false);
            var building = AssertMatchesQuotes(session, tower);
            Assert.That(building.IsComplete, Is.False); Assert.That(building.CanImprove, Is.False);
            session.Tick(tower.BuildRemaining + .1f);
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id, ModernizationDirection.Power), out var reason), Is.True, reason);
            var working = AssertMatchesQuotes(session, tower);
            Assert.That(working.IsComplete, Is.True); Assert.That(working.IsWorking, Is.True);
            Assert.That(working.CanImprove, Is.False);
            session.Tick(3);
            Assert.That(AssertMatchesQuotes(session, tower).HasOrdinaryUpgrade, Is.True);
        }

        [Test] public void DeadAndRemovedTowersCannotAdvertisePurchasesEvenFromAnOlderView()
        {
            var session = Session(); var tower = Build(session); var before = View(session, tower);
            tower.Health = 0;
            Assert.That(AssertMatchesQuotes(session, tower).CanImprove, Is.False);
            tower.Health = tower.MaxHealth;
            Assert.That(session.TryErase(tower.Cell, out _), Is.True);
            Assert.That(TowerDevelopmentState.Evaluate(new RunReadModel(session), before).CanImprove, Is.False);
        }

        [TestCase(RunPhase.Victory)] [TestCase(RunPhase.Defeat)]
        public void FinishedAttemptsNeverAdvertisePurchases(RunPhase phase)
        {
            var session = Session(); var tower = Build(session);
            typeof(GameSession).GetProperty("Phase").SetValue(session, phase);
            Assert.That(AssertMatchesQuotes(session, tower).CanImprove, Is.False);
        }

        [Test] public void DisabledFieldDevelopmentAndLegacySupportsNeverAdvertiseTowerPurchases()
        {
            var session = Session(enabled: false); var tower = Build(session);
            Assert.That(AssertMatchesQuotes(session, tower).CanImprove, Is.False);
            var supportSession = Session(); var cell = new Cell(4, 4);
            Assert.That(supportSession.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            supportSession.Tick(10);
            Assert.That(AssertMatchesQuotes(supportSession, supportSession.StructureAt(cell)).CanImprove, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void RepeatedEvaluationCannotSpendAdvanceOrChangeTheTower(bool working)
        {
            var session = Session(); var tower = Build(session);
            Colors(session)[(int)InkColor.Red] = 1000;
            if (working) Assert.That(session.TryModernize(session.QuoteModernization(tower.Id, ModernizationDirection.Power), out _), Is.True);
            var run = new RunReadModel(session); var before = View(session, tower);
            int ink = session.Ink, visualVersion = run.StructureVisualVersion;
            var coloredInk = Colors(session).ToArray(); long tick = session.TickIndex, events = session.EventBuffer.LatestSequence;
            for (int sample = 0; sample < 20; sample++) TowerDevelopmentState.Evaluate(run, before);
            var after = View(session, tower);
            Assert.That(session.Ink, Is.EqualTo(ink)); Assert.That(Colors(session), Is.EqualTo(coloredInk));
            Assert.That(session.TickIndex, Is.EqualTo(tick)); Assert.That(session.EventBuffer.LatestSequence, Is.EqualTo(events));
            Assert.That(run.StructureVisualVersion, Is.EqualTo(visualVersion));
            Assert.That(after.ResolvedEntry, Is.SameAs(before.ResolvedEntry));
            Assert.That(after.Health, Is.EqualTo(before.Health)); Assert.That(after.Color, Is.EqualTo(before.Color));
            Assert.That(after.Modernization.Revision, Is.EqualTo(before.Modernization.Revision));
            Assert.That(after.Modernization.WorkRemaining, Is.EqualTo(before.Modernization.WorkRemaining));
            Assert.That(after.Modernization.StepsUsed, Is.EqualTo(before.Modernization.StepsUsed));
        }
    }
}
#endif
