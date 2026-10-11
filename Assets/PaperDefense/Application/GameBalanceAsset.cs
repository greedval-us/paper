using System;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Баланс забега", fileName = "NotebookBalance")]
    public sealed class GameBalanceAsset : ScriptableObject
    {
        [Header("Страница")]
        [Min(20)] public int width = 64;
        [Min(16)] public int height = 40;
        [Min(0)] public int startingInk = 420;
        [Min(1)] public float baseHealth = 100;
        [Min(0)] public float planningSeconds = 90;
        [Min(0)] public float intermissionSeconds = 24;
        [Range(1,16)] public int maxEnemiesPerCell = 6;
        [Header("Мастерская и чернильные рубежи")]
        public bool enemyMechanics = true, inkWells = true, generateNewThreats = true;
        [Min(64)] public int workshopWidth = 96;
        [Min(40)] public int workshopHeight = 64;
        [Min(0)] public int workshopStartingInk = 720;
        [Min(0)] public int wellActivationCost = 45, wellInkPerWave = 12, waveGold = 8, victoryGold = 30;
        [Min(1)] public float wellHealth = 160;
        [Min(0)] public int blotterCost = 8;
        [Min(.1f)] public float blotterRadius = 3, blotterCooldown = 3, blotLifetime = 8, blotBuffSeconds = 4;
        [Range(0,.5f)] public float blotSpeedBonus = .15f, blotReduction = .15f;
        [Header("Экономика")]
        [Min(0)] public float passiveInkPerSecond = 1.8f;
        [Min(0)] public int waveInkGrant = 75;
        [Min(0)] public float earlyLaunchInkPerSecond = .8f;
        [Min(0)] public int maxEarlyLaunchInk = 30;
        [Header("Материалы, оружие, враги и волны")]
        public MaterialSettings[] materials = Array.Empty<MaterialSettings>();
        public WeaponSettings[] weapons = Array.Empty<WeaponSettings>();
        public EnemySettings[] enemies = Array.Empty<EnemySettings>();
        public WaveSettings[] waves = Array.Empty<WaveSettings>();

        public GameBalance CreateBalance()
        {
            var result = GameBalance.CreateDefault();
            result.Width = Mathf.Clamp(width, 20, 160);
            result.Height = Mathf.Clamp(height, 16, 100);
            result.StartingInk = Mathf.Max(0, startingInk);
            result.BaseHealth = Mathf.Max(1, baseHealth);
            result.PlanningSeconds = Mathf.Max(0, planningSeconds);
            result.IntermissionSeconds = Mathf.Max(0, intermissionSeconds);
            result.MaxEnemiesPerCell = Mathf.Clamp(maxEnemiesPerCell, 1, 16);
            result.PassiveInkPerSecond = Mathf.Max(0, passiveInkPerSecond);
            result.WaveInkGrant = Mathf.Max(0, waveInkGrant);
            result.EarlyLaunchInkPerSecond = Mathf.Max(0, earlyLaunchInkPerSecond);
            result.MaxEarlyLaunchInk = Mathf.Max(0, maxEarlyLaunchInk);
            foreach (var item in materials ?? Array.Empty<MaterialSettings>())
            {
                var target = result.Material(item.tier);
                target.CostMultiplier = Mathf.Max(.1f, item.cost);
                target.HealthMultiplier = Mathf.Max(.1f, item.health);
                target.DamageMultiplier = Mathf.Max(.1f, item.damage);
                target.RefundFraction = Mathf.Clamp01(item.refund);
                target.BuildTimeMultiplier = Mathf.Max(.1f, item.buildTime);
            }
            foreach (var item in weapons ?? Array.Empty<WeaponSettings>())
            {
                var target = result.Structure(item.kind);
                target.Cost = Mathf.Max(1, item.cost);
                target.MaxHealth = Mathf.Max(1, item.health);
                target.BuildSeconds = Mathf.Max(0, item.buildSeconds);
                target.Range = Mathf.Max(0, item.range);
                target.Damage = Mathf.Max(0, item.damage);
                target.ShotInterval = Mathf.Max(.05f, item.shotInterval);
                target.Radius = Mathf.Max(.1f, item.radius);
                target.MaxPierce = Mathf.Max(1, item.pierce);
                target.FlightSeconds = Mathf.Max(.05f, item.flightSeconds);
            }
            foreach (var item in enemies ?? Array.Empty<EnemySettings>())
            {
                var target = result.Enemy(item.kind);
                target.MaxHealth = Mathf.Max(1, item.health);
                target.Speed = Mathf.Max(.1f, item.speed);
                target.Damage = Mathf.Max(.1f, item.damage);
                target.AttackInterval = Mathf.Max(.1f, item.attackInterval);
                target.Reward = Mathf.Max(0, item.reward);
                target.Range = Mathf.Max(0,item.range);
                target.WindupSeconds = Mathf.Max(.1f,item.windupSeconds);
                target.ProjectileSeconds = Mathf.Max(.1f,item.projectileSeconds);
            }
            if (waves != null && waves.Length > 0)
            {
                result.Waves.Clear();
                foreach (var item in waves)
                    result.Waves.Add(new WaveDefinition { Title = item.title, Count = Mathf.Max(1, item.count),
                        SpawnInterval = Mathf.Max(.005f, item.spawnInterval), RunnerFraction = Mathf.Clamp01(item.runners),
                        GnawerFraction = Mathf.Clamp(item.gnawers, 0, 1 - Mathf.Clamp01(item.runners)),
                        SpitterFraction=Mathf.Clamp(item.spitters,0,1-Mathf.Clamp01(item.runners)-Mathf.Clamp(item.gnawers,0,1-Mathf.Clamp01(item.runners))),
                        OutlinedFraction=Mathf.Clamp01(item.outlined),HatchedFraction=Mathf.Clamp(item.hatched,0,1-Mathf.Clamp01(item.outlined)),
                        Sides = item.sides == 0 ? 15 : item.sides });
            }
            return result;
        }

        public GameBalance CreateWorkshopBalance()
        {
            var result=CreateBalance();
            result.Width=Mathf.Clamp(workshopWidth,64,160);result.Height=Mathf.Clamp(workshopHeight,40,100);
            result.StartingInk=Mathf.Max(0,workshopStartingInk);result.EnableEnemyMechanics=enemyMechanics;result.EnableInkWells=inkWells;
            result.WellActivationCost=Mathf.Max(0,wellActivationCost);result.WellInkPerWave=Mathf.Max(0,wellInkPerWave);
            result.WellHealth=Mathf.Max(1,wellHealth);result.WaveGold=Mathf.Max(0,waveGold);result.VictoryGold=Mathf.Max(0,victoryGold);
            result.BlotterCost=Mathf.Max(0,blotterCost);result.BlotterRadius=Mathf.Max(.1f,blotterRadius);
            result.BlotterCooldown=Mathf.Max(.1f,blotterCooldown);result.BlotLifetime=Mathf.Max(.1f,blotLifetime);
            result.BlotBuffSeconds=Mathf.Max(.1f,blotBuffSeconds);result.BlotSpeedBonus=Mathf.Clamp(blotSpeedBonus,0,.5f);
            result.BlotReduction=Mathf.Clamp(blotReduction,0,.5f);
            if(generateNewThreats)AddWorkshopThreats(result);
            return result;
        }

        internal static void AddWorkshopThreats(GameBalance balance)
        {
            for(int i=0;i<balance.Waves.Count;i++)
            {
                var wave=balance.Waves[i];
                if(i>=2&&wave.SpitterFraction<=0)wave.SpitterFraction=Math.Min(.16f,.04f*(i-1));
                if(i>=1&&wave.OutlinedFraction<=0)wave.OutlinedFraction=Math.Min(.35f,.08f*i);
                if(i>=4&&wave.HatchedFraction<=0)wave.HatchedFraction=Math.Min(.2f,.05f*(i-3));
            }
        }

        public void ResetToDefaults()
        {
            var b = GameBalance.CreateDefault();
            width = b.Width; height = b.Height; startingInk = b.StartingInk; baseHealth = b.BaseHealth;
            planningSeconds = b.PlanningSeconds; intermissionSeconds = b.IntermissionSeconds;
            maxEnemiesPerCell = b.MaxEnemiesPerCell;
            passiveInkPerSecond=b.PassiveInkPerSecond;waveInkGrant=b.WaveInkGrant;
            earlyLaunchInkPerSecond=b.EarlyLaunchInkPerSecond;maxEarlyLaunchInk=b.MaxEarlyLaunchInk;
            materials = new MaterialSettings[3];
            for (int i = 0; i < materials.Length; i++)
            { var d = b.Material((MaterialTier)i); materials[i] = new MaterialSettings { tier = (MaterialTier)i, cost = d.CostMultiplier,
                health = d.HealthMultiplier, damage = d.DamageMultiplier, refund = d.RefundFraction, buildTime = d.BuildTimeMultiplier }; }
            weapons = new WeaponSettings[5];
            for (int i = 0; i < weapons.Length; i++)
            { var d = b.Structure((StructureKind)i); weapons[i] = new WeaponSettings {kind = (StructureKind)i, cost = d.Cost,
                health = d.MaxHealth, buildSeconds = d.BuildSeconds, range = d.Range, damage = d.Damage,
                shotInterval = d.ShotInterval, radius = d.Radius, pierce = d.MaxPierce, flightSeconds = d.FlightSeconds }; }
            enemies = new EnemySettings[4];
            for (int i = 0; i < enemies.Length; i++)
            { var d = b.Enemy((EnemyKind)i); enemies[i] = new EnemySettings {kind = (EnemyKind)i, health = d.MaxHealth,
                speed = d.Speed, damage = d.Damage, attackInterval = d.AttackInterval, reward = d.Reward,
                range=d.Range,windupSeconds=d.WindupSeconds,projectileSeconds=d.ProjectileSeconds}; }
            waves = new WaveSettings[b.Waves.Count];
            for (int i = 0; i < waves.Length; i++)
            {var d = b.Waves[i]; waves[i] = new WaveSettings {title = d.Title, count = d.Count, spawnInterval = d.SpawnInterval,
                runners = d.RunnerFraction, gnawers = d.GnawerFraction, spitters=d.SpitterFraction,
                outlined=d.OutlinedFraction,hatched=d.HatchedFraction,sides = d.Sides};}
        }

        [Serializable] public sealed class MaterialSettings
        { public MaterialTier tier; public float cost = 1, health = 1, damage = 1; [Range(0,1)] public float refund = .8f; public float buildTime = 1; }
        [Serializable] public sealed class WeaponSettings
        { public StructureKind kind; public int cost = 20; public float health = 30, buildSeconds = 1, range = 8, damage = 10, shotInterval = 1, radius = 2, flightSeconds = .8f; public int pierce = 4; }
        [Serializable] public sealed class EnemySettings
        {public EnemyKind kind; public float health = 15, speed = 2, damage = 4, attackInterval = 1,range=12.5f,windupSeconds=.8f,projectileSeconds=.65f; public int reward = 1;}
        [Serializable] public sealed class WaveSettings
        {public string title; public int count = 100; public float spawnInterval = .08f; [Range(0,1)] public float runners, gnawers,spitters,outlined,hatched;
            [Tooltip("Запад = 1, восток = 2, север = 4, юг = 8. Суммируйте направления.")] public int sides = 1; }
    }
}
