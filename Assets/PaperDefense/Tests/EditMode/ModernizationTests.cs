using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ModernizationTests
    {
        [TestCase(ModernizationDirection.Power, 8,12,16)]
        [TestCase(ModernizationDirection.Mechanics, 6,9,12)]
        [TestCase(ModernizationDirection.Range, 8,13,18)]
        [TestCase(ModernizationDirection.Resilience, 10,16,24)]
        public void CostsUseOriginalPencilPrice(ModernizationDirection direction,int first,int second,int third)
        {
            Assert.That(ModernizationRules.OrdinaryCost(40,direction,1),Is.EqualTo(first));
            Assert.That(ModernizationRules.OrdinaryCost(40,direction,2),Is.EqualTo(second));
            Assert.That(ModernizationRules.OrdinaryCost(40,direction,3),Is.EqualTo(third));
        }
        [Test] public void RankAllocationRejectsSeventhStepAndSecondThirdRank()
        {
            Assert.Throws<ArgumentException>(()=>new ModernizationRanks(3,3));
            Assert.Throws<ArgumentException>(()=>new ModernizationRanks(2,2,2,1));
            Assert.That(new ModernizationRanks(3,2,1).StepsUsed,Is.EqualTo(6));
        }
        static GameSession Session(int maximum=3)
        {
            var rules=GameBalance.CreateDefault();rules.Mechanics.FieldModernization=true;rules.Mechanics.MaximumModernizationRank=maximum;
            rules.StartingInk=10000;rules.PlanningSeconds=1000;rules.PassiveInkPerSecond=0;
            return new GameSession(rules,starterDefenses:false);
        }
        static StructureState Build(GameSession session, string core="gun")
        {
            var entry=AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(core)));
            Assert.That(session.TryBuildEntry(new[]{new Cell(5,5)},entry,MaterialTier.Pencil,InkColor.None,out var reason),Is.True,reason);
            session.Tick(entry.BuildSeconds+.1f);return session.StructureAt(new Cell(5,5));
        }
        [Test] public void QuoteIsReadOnlyAndPurchaseIsAtomicAndRevisionBound()
        {
            var session=Session();var tower=Build(session);int money=session.Ink;long tick=session.TickIndex;long events=session.EventBuffer.LatestSequence;
            var quote=session.QuoteModernization(tower.Id,ModernizationDirection.Power);
            Assert.That(quote.Valid,Is.True,quote.Reason);Assert.That(session.Ink,Is.EqualTo(money));Assert.That(session.TickIndex,Is.EqualTo(tick));Assert.That(session.EventBuffer.LatestSequence,Is.EqualTo(events));
            Assert.That(session.TryModernize(quote,out _),Is.True);Assert.That(session.Ink,Is.EqualTo(money-quote.Cost));
            Assert.That(session.TryModernize(quote,out _),Is.False);Assert.That(session.Ink,Is.EqualTo(money-quote.Cost));
            session.Tick(2.1f);Assert.That(tower.Modernization.Rank(ModernizationDirection.Power),Is.EqualTo(1));
            Assert.That(session.TryModernize(quote,out _),Is.False);
        }
        [Test] public void CompletedResiliencePreservesDamageTakenAndRepairDuringWork()
        {
            var session=Session();var tower=Build(session);var baseline=tower.ResolvedEntry;
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Resilience),out _),Is.True);
            tower.Health=tower.MaxHealth*.6f;session.Tick(1);tower.Health=tower.MaxHealth*.8f;session.Tick(1.1f);
            Assert.That(tower.MaxHealth,Is.EqualTo(baseline.Health*1.35f).Within(.001));
            Assert.That(tower.Health/tower.MaxHealth,Is.EqualTo(.8f).Within(.001));Assert.That(tower.ResolvedEntry.Snapshot,Is.SameAs(baseline.Snapshot));
        }
        [Test] public void SoldAndRebuiltTowerRejectsOldQuote()
        {
            var session=Session();var tower=Build(session);var quote=session.QuoteModernization(tower.Id,ModernizationDirection.Power);
            Assert.That(session.TryErase(tower.Cell,out _),Is.True);var rebuilt=Build(session);int money=session.Ink;
            Assert.That(rebuilt.Id,Is.Not.EqualTo(tower.Id));Assert.That(session.TryModernize(quote,out _),Is.False);Assert.That(session.Ink,Is.EqualTo(money));
        }
        [Test] public void TechnologyAndPureCleanupRejectUselessPurchases()
        {
            var session=Session(1);var tower=Build(session,"blotter");
            Assert.That(session.QuoteModernization(tower.Id,ModernizationDirection.Power).Valid,Is.False);
            Assert.That(session.QuoteColorModernization(tower.Id,InkColor.Red).Valid,Is.False);
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Mechanics),out _),Is.True);session.Tick(2.1f);
            Assert.That(session.QuoteModernization(tower.Id,ModernizationDirection.Mechanics).Valid,Is.False);
        }
        [Test] public void LockedTechnologyQuoteStillDescribesExactNextPurchase()
        {
            var session=Session(1);var tower=Build(session);
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Power),out _),Is.True);session.Tick(2.1f);
            var quote=session.QuoteModernization(tower.Id,ModernizationDirection.Power);
            Assert.That(quote.Valid,Is.False);Assert.That(quote.CurrentRank,Is.EqualTo(1));Assert.That(quote.NextRank,Is.EqualTo(2));
            Assert.That(quote.Cost,Is.EqualTo(ModernizationRules.OrdinaryCost(tower.ResolvedEntry.Cost,ModernizationDirection.Power,2)));
            Assert.That(quote.WorkSeconds,Is.EqualTo(3));Assert.That(quote.After.Weapons[0].Damage,Is.GreaterThan(quote.Before.Weapons[0].Damage));
        }
        [Test] public void MaterialAndPaintCannotBypassFieldRules()
        {
            var session=Session();var tower=Build(session);int money=session.Ink;
            Assert.That(session.TryUpgrade(tower.Cell,out _),Is.False);Assert.That(session.TryPaint(tower.Cell,InkColor.Red,out _),Is.False);
            Assert.That(session.TryPaint(tower.Cell,InkColor.None,out _),Is.True);Assert.That(session.Ink,Is.EqualTo(money));
        }
        [Test] public void WorkPreservesCycleAndAcquisitionDuringPlanning()
        {
            var session=Session();var tower=Build(session);var mount=tower.GetMountState(0);mount.AcquisitionRemaining=.3f;mount.TargetId=21;
            var cycle=mount.Cycle;
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Mechanics),out _),Is.True);
            session.Tick(1);Assert.That(mount.Cycle,Is.SameAs(cycle));Assert.That(mount.AcquisitionRemaining,Is.EqualTo(.3f));Assert.That(mount.TargetId,Is.EqualTo(21));
            session.Tick(0);Assert.That(tower.Modernization.WorkRemaining,Is.EqualTo(1).Within(.001));
        }
        [Test] public void StartedWorkIsRefundableAtTheExistingDamagedHealthRate()
        {
            var session=Session();var tower=Build(session);var quote=session.QuoteModernization(tower.Id,ModernizationDirection.Power);
            Assert.That(session.TryModernize(quote,out _),Is.True);tower.Health=tower.MaxHealth*.5f;
            Assert.That(session.TryErase(tower.Cell,out int refund),Is.True);
            Assert.That(refund,Is.EqualTo((int)Math.Floor((tower.ResolvedEntry.Cost+quote.Cost)*.5f*tower.ResolvedEntry.RefundFraction+.00001f)));
            session.Tick(10);Assert.That(session.StructureAt(tower.Cell),Is.Null);
        }
        [Test] public void QuoteCannotBeAppliedToAnotherRunWithMatchingIdentity()
        {
            var first=Session();var firstTower=Build(first);var second=Session();var secondTower=Build(second);int money=second.Ink;
            Assert.That(firstTower.Id,Is.EqualTo(secondTower.Id));
            Assert.That(second.TryModernize(first.QuoteModernization(firstTower.Id,ModernizationDirection.Power),out _),Is.False);
            Assert.That(second.Ink,Is.EqualTo(money));
        }
        [Test] public void SixStepsAndOneThirdRankAreEnforcedByTheRealCommand()
        {
            var session=Session();var tower=Build(session);
            foreach(var direction in new[]{ModernizationDirection.Power,ModernizationDirection.Power,ModernizationDirection.Power,ModernizationDirection.Mechanics,ModernizationDirection.Mechanics})
            {Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,direction),out _),Is.True);session.Tick(4.1f);}
            int money=session.Ink;
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Mechanics),out _),Is.False);
            Assert.That(session.Ink,Is.EqualTo(money));
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Range),out _),Is.True);session.Tick(2.1f);money=session.Ink;
            Assert.That(tower.Modernization.StepsUsed,Is.EqualTo(6));Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Resilience),out _),Is.False);Assert.That(session.Ink,Is.EqualTo(money));
        }
        [Test] public void DestroyedTowerCannotCompleteOrResurrectAndResilienceInvalidatesNavigation()
        {
            var session=Session();var tower=Build(session);int topology=session.TopologyVersion;
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Resilience),out _),Is.True);session.Tick(2.01f);
            Assert.That(session.TopologyVersion,Is.GreaterThan(topology));
            var another=Session();var doomed=Build(another);Assert.That(another.TryModernize(another.QuoteModernization(doomed.Id,ModernizationDirection.Resilience),out _),Is.True);
            doomed.Health=0;another.Tick(4);Assert.That(doomed.Health,Is.Zero);Assert.That(doomed.Modernization.Rank(ModernizationDirection.Resilience),Is.Zero);
            Assert.That(another.EventBuffer.Count(e=>e.Kind==SimulationEventKind.ModernizationCompleted),Is.Zero);
        }
        [Test] public void LegacyMaterialAndSupportContractsRemainExplicitlyAvailable()
        {
            var rules=GameBalance.CreateDefault();rules.StartingInk=1000;rules.PlanningSeconds=1000;
            var legacy=new GameSession(rules,starterDefenses:false);var cell=new Cell(5,5);
            Assert.That(legacy.TryBuild(new[]{cell},StructureKind.Gun,MaterialTier.Pencil,out _),Is.True);legacy.Tick(2);
            Assert.That(legacy.TryUpgrade(cell,out _),Is.True);
            rules.Mechanics.FieldModernization=true;var field=new GameSession(rules,starterDefenses:false);
            Assert.That(field.TryBuild(new[]{cell},StructureKind.Wall,MaterialTier.Pencil,out _),Is.True);field.Tick(1);
            Assert.That(field.TryUpgrade(cell,out _),Is.True);
        }
        [Test] public void FieldModeSuppressesGlobalUpgradeChoicesAndFreeDeepInk()
        {
            var rules=GameBalance.CreateDefault();rules.Mechanics.FieldModernization=true;rules.StartingInk=1000;rules.PlanningSeconds=1000;rules.IntermissionSeconds=1000;
            rules.Waves[0].Count=0;rules.Waves[1].Count=0;var session=new GameSession(rules,starterDefenses:false);var tower=Build(session);
            session.StartNextWave();session.Tick(.1f);session.StartNextWave();session.Tick(.1f);
            Assert.That(session.PendingUpgrade,Is.False);float health=tower.MaxHealth;session.ChooseUpgrade(RunUpgrade.DeepInk);Assert.That(tower.MaxHealth,Is.EqualTo(health));
        }
        [Test] public void ColorInstallationLocksImmediatelyAndRepeatedPaintCannotSpendOrReplace()
        {
            var rules=GameBalance.CreateDefault();rules.Mechanics.FieldModernization=true;rules.Mechanics.MaximumModernizationRank=3;rules.EnableInkWells=true;
            rules.StartingInk=10000;rules.PlanningSeconds=1000;rules.IntermissionSeconds=1000;rules.WellInkPerWave=40;rules.Waves[0].Count=0;
            var session=new GameSession(rules,starterDefenses:false);var tower=Build(session);
            int index=0;while(session.Wells[index].Color!=InkColor.Red)index++;
            Assert.That(session.TryActivateWell(index,out _),Is.True);session.StartNextWave();session.Tick(.1f);
            int colored=session.ColoredInk(InkColor.Red),ordinary=session.Ink;
            var quote=session.QuoteColorModernization(tower.Id,InkColor.Red);Assert.That(quote.Valid,Is.True,quote.Reason);
            Assert.That(session.TryModernize(quote,out _),Is.True);Assert.That(tower.Color,Is.EqualTo(InkColor.Red));Assert.That(tower.Modernization.Ranks.Color,Is.EqualTo(InkColor.Red));Assert.That(tower.Modernization.ColorRank,Is.Zero);
            Assert.That(session.ColoredInk(InkColor.Red),Is.EqualTo(colored-quote.Cost));Assert.That(session.Ink,Is.EqualTo(ordinary));
            Assert.That(session.TryPaint(tower.Cell,InkColor.Red,out _),Is.True);Assert.That(session.TryPaint(tower.Cell,InkColor.None,out _),Is.False);Assert.That(session.TryPaint(tower.Cell,InkColor.Blue,out _),Is.False);
            session.Tick(2.1f);Assert.That(tower.Modernization.ColorRank,Is.EqualTo(1));
            Assert.That(session.QuoteColorModernization(tower.Id,InkColor.Blue).Valid,Is.False);
            var second=session.QuoteColorModernization(tower.Id,InkColor.Red);Assert.That(second.Cost,Is.EqualTo(2*quote.Cost));Assert.That(session.TryModernize(second,out _),Is.True);
            session.Tick(3.1f);Assert.That(tower.Modernization.ColorRank,Is.EqualTo(2));Assert.That(tower.Modernization.StepsUsed,Is.Zero);
        }
        [Test] public void RepairPostContinuesRepairingWhileTowerWorks()
        {
            var session=Session();var tower=Build(session);
            Assert.That(session.TryBuild(new[]{new Cell(6,5)},StructureKind.RepairPost,MaterialTier.Pencil,out _),Is.True);session.Tick(2.1f);
            tower.Health=tower.MaxHealth*.5f;float before=tower.Health;
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Resilience),out _),Is.True);session.Tick(1);
            Assert.That(tower.Modernization.IsWorking,Is.True);Assert.That(tower.Health,Is.EqualTo(before+8).Within(.001));
        }
        [Test] public void UnaffordableQuoteCannotStartWorkOrPublishAnEvent()
        {
            var entry=AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower());var rules=GameBalance.CreateDefault();rules.Mechanics.FieldModernization=true;rules.StartingInk=entry.Cost;rules.PassiveInkPerSecond=0;rules.PlanningSeconds=1000;
            var session=new GameSession(rules,starterDefenses:false);var tower=Build(session);long events=session.EventBuffer.LatestSequence;
            var quote=session.QuoteModernization(tower.Id,ModernizationDirection.Power);Assert.That(quote.Valid,Is.False);Assert.That(quote.Cost,Is.GreaterThan(0));
            Assert.That(session.TryModernize(quote,out _),Is.False);Assert.That(session.Ink,Is.Zero);Assert.That(tower.Modernization.IsWorking,Is.False);Assert.That(session.EventBuffer.LatestSequence,Is.EqualTo(events));
        }
        [Test] public void MechanicsAndTechnologyAreFrozenInTheSessionSnapshot()
        {
            var rules=GameBalance.CreateDefault();rules.Mechanics.FieldModernization=true;rules.Mechanics.MaximumModernizationRank=1;rules.StartingInk=10000;rules.PlanningSeconds=1000;
            var session=new GameSession(rules,starterDefenses:false);rules.Mechanics.FieldModernization=false;rules.Mechanics.MaximumModernizationRank=3;
            var tower=Build(session);Assert.That(session.Mechanics.FieldModernization,Is.True);Assert.That(new RunReadModel(session).Balance.Mechanics.MaximumModernizationRank,Is.EqualTo(1));
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Power),out _),Is.True);session.Tick(2.1f);Assert.That(session.QuoteModernization(tower.Id,ModernizationDirection.Power).Valid,Is.False);
            rules.Mechanics.MaximumModernizationRank=4;Assert.Throws<ArgumentOutOfRangeException>(()=>new GameSession(rules,starterDefenses:false));
        }
        [Test] public void EffectiveBattleEntryCannotBeRebuiltAsAFreeRankZeroTower()
        {
            var session=Session();var tower=Build(session);var quote=session.QuoteModernization(tower.Id,ModernizationDirection.Power);
            int money=session.Ink;int count=session.Structures.Count;long events=session.EventBuffer.LatestSequence;
            var cells=new[]{new Cell(6,5)};
            Assert.That(session.QuoteEntry(cells,quote.After,MaterialTier.Pencil,InkColor.None).Valid,Is.False);
            Assert.That(session.TryBuildEntry(cells,quote.After,MaterialTier.Pencil,InkColor.None,out _),Is.False);
            Assert.That(session.Ink,Is.EqualTo(money));Assert.That(session.Structures.Count,Is.EqualTo(count));Assert.That(session.EventBuffer.LatestSequence,Is.EqualTo(events));
            Assert.That(session.TryModernize(quote,out _),Is.True);session.Tick(2.1f);
            Assert.That(session.TryBuildEntry(cells,tower.ResolvedEntry,MaterialTier.Pencil,InkColor.None,out _),Is.False);
            Assert.That(session.TryBuildEntry(cells,quote.Before,MaterialTier.Pencil,InkColor.None,out _),Is.True,"The saved baseline recipe remains buildable.");
        }
        [Test] public void ReadModelSnapshotsAndNeighborCopiesKeepTheirOriginalRanksAndRecipe()
        {
            var session=Session();var first=Build(session);var baseline=first.ResolvedEntry;
            Assert.That(session.TryBuildEntry(new[]{new Cell(6,5)},baseline,MaterialTier.Pencil,InkColor.None,out _),Is.True);session.Tick(baseline.BuildSeconds+.1f);
            var read=new RunReadModel(session);var oldView=read.StructureAt(first.Cell).Value;var second=session.StructureAt(new Cell(6,5));
            var quote=read.QuoteModernization(first.Id,ModernizationDirection.Power);
            Assert.That(session.TryModernize(quote,out _),Is.True);session.Tick(2.1f);
            Assert.That(oldView.Modernization.StepsUsed,Is.Zero);Assert.That(oldView.ResolvedEntry,Is.SameAs(baseline));
            Assert.That(read.StructureAt(first.Cell).Value.Modernization.StepsUsed,Is.EqualTo(1));
            Assert.That(second.Modernization.StepsUsed,Is.Zero);Assert.That(second.ResolvedEntry,Is.SameAs(baseline));
            Assert.That(first.ResolvedEntry.Snapshot,Is.SameAs(baseline.Snapshot));Assert.That(first.ResolvedEntry.Weapons[0].Damage,Is.EqualTo(baseline.Weapons[0].Damage*1.3f).Within(.001));
        }
        [Test] public void CompletedMechanicsResumesTheSameReloadFractionAtTheNewSpeed()
        {
            var session=Session();var tower=Build(session);var baseline=tower.ResolvedEntry;var mount=tower.GetMountState(0);
            var reload=new WeaponCycleState(0,true,.5);mount.Cycle=reload;
            Assert.That(session.TryModernize(session.QuoteModernization(tower.Id,ModernizationDirection.Mechanics),out _),Is.True);session.Tick(2);
            Assert.That(mount.Cycle,Is.SameAs(reload));Assert.That(mount.Cycle.RoundsRemaining,Is.Zero);
            session.Tick(.01f);float feed=tower.ResolvedEntry.FeedForActiveMounts(1);
            double expected=.5-.01/(baseline.Weapons[0].ReloadSeconds/1.2/feed);
            Assert.That(mount.Cycle.IsReloading,Is.True);Assert.That(mount.Cycle.RoundsRemaining,Is.Zero);Assert.That(mount.Cycle.RemainingWork,Is.EqualTo(expected).Within(.00001));
        }
        [TestCase(InkColor.Red)] [TestCase(InkColor.Blue)] [TestCase(InkColor.Green)]
        public void ColorWithoutAnEffectiveDefinitionCannotBePurchased(InkColor color)
        {
            var rules=GameBalance.CreateDefault();rules.Mechanics.FieldModernization=true;rules.StartingInk=10000;rules.PlanningSeconds=1000;rules.EnableInkWells=true;rules.WellInkPerWave=40;rules.Waves[0].Count=0;
            rules.Effects.RedDamagePerTick=0;rules.Effects.BlueSpeedMultiplier=1;rules.Effects.GreenDuration=0;
            var session=new GameSession(rules,starterDefenses:false);var tower=Build(session);
            int well=0;while(session.Wells[well].Color!=color)well++;Assert.That(session.TryActivateWell(well,out _),Is.True);session.StartNextWave();session.Tick(.1f);
            var quote=session.QuoteColorModernization(tower.Id,color);int money=session.Ink;long events=session.EventBuffer.LatestSequence;
            Assert.That(quote.Valid,Is.False);Assert.That(quote.Reason,Does.Contain("эффект"));
            Assert.That(session.TryModernize(quote,out _),Is.False);Assert.That(tower.Color,Is.EqualTo(InkColor.None));Assert.That(session.Ink,Is.EqualTo(money));Assert.That(session.EventBuffer.LatestSequence,Is.EqualTo(events));
        }
        [Test] public void RulesUseAbsoluteMultipliersWithoutChangingGeometryOrFeed()
        {
            var entry=AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower());var effective=ModernizationRules.Resolve(entry,new ModernizationRanks(3,2,1));
            Assert.That(effective.Weapons[0].Damage,Is.EqualTo(entry.Weapons[0].Damage*2.10f).Within(.001));
            Assert.That(effective.Weapons[0].Interval,Is.EqualTo(entry.Weapons[0].Interval/1.45f).Within(.001));
            Assert.That(effective.Weapons[0].ReloadSeconds,Is.EqualTo(entry.Weapons[0].ReloadSeconds/1.45f).Within(.001));
            Assert.That(effective.Weapons[0].Range,Is.EqualTo(entry.Weapons[0].Range*1.10f).Within(.001));
            Assert.That(effective.FeedCapacity,Is.EqualTo(entry.FeedCapacity));Assert.That(effective.Weapons[0].MagazineSize,Is.EqualTo(entry.Weapons[0].MagazineSize));
        }
    }
}

