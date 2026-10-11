using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public class GoldPayoutTests
    {
        GameObject root;GameBalanceAsset balance;RunController run;
        [SetUp] public void SetUp()
        {
            root=new GameObject("Gold payout commands");root.SetActive(false);
            run=root.AddComponent<RunController>();balance=ScriptableObject.CreateInstance<GameBalanceAsset>();
            balance.passiveInkPerSecond=0;balance.earlyLaunchInkPerSecond=0;balance.planningSeconds=1000;
            balance.baseHealth=1;balance.workshopStartingInk=1000;balance.waveGold=8;balance.victoryGold=30;
            balance.weapons=new[]{new GameBalanceAsset.WeaponSettings{kind=StructureKind.Gun,cost=40,
                health=55,buildSeconds=0,range=200,damage=999,shotInterval=.05f}};
            balance.enemies=new[]{new GameBalanceAsset.EnemySettings{kind=EnemyKind.Doodle,
                health=1,speed=80,damage=10,attackInterval=.1f,reward=0}};
            balance.waves=new[]{new GameBalanceAsset.WaveSettings{count=1,sides=1},new GameBalanceAsset.WaveSettings{count=1,sides=1}};
            run.balanceAsset=balance;run.InitializeWorkshop(false);
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(balance);}
        void Frame(int count){for(int i=0;i<count;i++)run.AdvanceFrame(1f/30,false,1);}
        void WinFirstWave()
        {
            Assert.That(run.StartRun(out _),Is.True);
            Assert.That(run.BuildBlueprint(new[]{new Cell(48,24)},0,MaterialTier.Pencil,InkColor.None,out _),Is.True);
            run.NextWave();Frame(15);
            Assert.That(run.Session.Phase,Is.EqualTo(RunPhase.Intermission));
            Assert.That(run.Workshop.Gold,Is.EqualTo(8));
        }
        [Test] public void CompletedWaveGoldIsBankedOnceAndSurvivesDefeatAndNewWorkshop()
        {
            WinFirstWave();Frame(20);Assert.That(run.Workshop.Gold,Is.EqualTo(8));
            Assert.That(run.Erase(new Cell(48,24),out _),Is.True);run.NextWave();Frame(300);
            Assert.That(run.Session.Phase,Is.EqualTo(RunPhase.Defeat));Assert.That(run.Workshop.Gold,Is.EqualTo(8));
            run.OpenWorkshop();Assert.That(run.Workshop.Gold,Is.EqualTo(8));
            Assert.That(run.StartRun(out _),Is.True);Frame(5);Assert.That(run.Workshop.Gold,Is.EqualTo(8));
        }
        [Test] public void VictoryAddsOnlyCompletedWaveAndOneVictoryBonus()
        {
            WinFirstWave();run.NextWave();Frame(10);
            Assert.That(run.Session.Phase,Is.EqualTo(RunPhase.Victory));Assert.That(run.Workshop.Gold,Is.EqualTo(46));
            Frame(100);Assert.That(run.Workshop.Gold,Is.EqualTo(46));
            run.Restart();Assert.That(run.InWorkshop,Is.True);Assert.That(run.Workshop.Gold,Is.EqualTo(46));
        }
    }
}
