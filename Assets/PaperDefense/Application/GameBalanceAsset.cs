using System;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Compatibility/Legacy Balance Adapter", fileName = "LegacyBalance")]
    public sealed class GameBalanceAsset : ScriptableObject
    {
        [Tooltip("Источник подготовленного каталога и правил. Старые поля ниже используются только ассетами без ссылки, для миграции v1–v4 и legacy fixtures.")]
        public BalancePresetAsset dataPreset;
        [Tooltip("Явный адаптер старых fixtures/миграции. Для рабочих мигрированных ассетов выключен: потерянная ссылка на пресет — ошибка.")]
        public bool legacyAuthoringAdapter = true;
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
        [Header("Механики · конфигурация разработчика")]
        public MechanicsConfiguration mechanics = new MechanicsConfiguration();
        [Min(64)] public int workshopWidth = GameBalancePresets.DefaultWorkshopWidth;
        [Min(40)] public int workshopHeight = GameBalancePresets.DefaultWorkshopHeight;
        [Min(0)] public int workshopStartingInk = GameBalancePresets.DefaultWorkshopStartingInk;
        public bool useTargetWorkshopPreset;
        public TargetPresetSettings targetSettings = new TargetPresetSettings();
        [Min(0)] public int wellActivationCost = 45, wellInkPerWave = 12, waveGold = 8, victoryGold = 30;
        [Min(1)] public float wellHealth = 160;
        [Min(0)] public int blotterCost = 8;
        [Min(.1f)] public float blotterRadius = 3, blotterCooldown = 3, blotLifetime = 8, blotBuffSeconds = 4;
        [Range(0,.5f)] public float blotSpeedBonus = .15f, blotReduction = .15f;
        [Header("Качество рисунка · стартовая гипотеза")]
        [Range(.01f,1)] public float drawingMinimumRate = .85f;
        [Range(.01f,1)] public float drawingMaximumRate = 1;
        [Header("Экономика")]
        [Min(0)] public float passiveInkPerSecond = 1.8f;
        [Min(0)] public int waveInkGrant = 75;
        [Min(0)] public float earlyLaunchInkPerSecond = .8f;
        [Min(0)] public int maxEarlyLaunchInk = 30;
        [Header("Материалы, оружие, враги и волны")]
        public TowerConstructionRules construction = new TowerConstructionRules();
        public EffectsRules effects = new EffectsRules();
        public ProgressionRules progression = new ProgressionRules();
        public MaterialSettings[] materials = Array.Empty<MaterialSettings>();
        public WeaponSettings[] weapons = Array.Empty<WeaponSettings>();
        public EnemySettings[] enemies = Array.Empty<EnemySettings>();
        public WaveSettings[] waves = Array.Empty<WaveSettings>();

        public GameBalance CreateBalance()
        {
            if (dataPreset != null) return dataPreset.CreateBalance();
            if (!legacyAuthoringAdapter) throw new ArgumentException(UserText.Message("copy.7a045f879970f558", name));
            Finite(baseHealth, nameof(baseHealth)); Finite(planningSeconds, nameof(planningSeconds)); Finite(intermissionSeconds, nameof(intermissionSeconds));
            Finite(passiveInkPerSecond, nameof(passiveInkPerSecond)); Finite(earlyLaunchInkPerSecond, nameof(earlyLaunchInkPerSecond));
            Finite(drawingMinimumRate, nameof(drawingMinimumRate)); Finite(drawingMaximumRate, nameof(drawingMaximumRate));
            var result = GameBalance.CreateDefault();
            result.Mechanics = (mechanics ?? new MechanicsConfiguration()).CreateSnapshot();
            result.Construction = (construction ?? new TowerConstructionRules()).CreateSnapshot();
            result.Effects = (effects ?? new EffectsRules()).CreateSnapshot();
            result.Progression = (progression ?? new ProgressionRules()).CreateSnapshot();
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
            result.DrawingMaximumRate = Mathf.Clamp(drawingMaximumRate, .01f, 1);
            result.DrawingMinimumRate = Mathf.Clamp(drawingMinimumRate, .01f, result.DrawingMaximumRate);
            foreach (var item in materials ?? Array.Empty<MaterialSettings>())
            {
                if (item == null || !Enum.IsDefined(typeof(MaterialTier), item.tier)) throw new ArgumentException(UserText.Message("copy.81ef3bc14a14f203"), nameof(materials));
                Finite(item.cost, nameof(item.cost)); Finite(item.health, nameof(item.health)); Finite(item.damage, nameof(item.damage));
                Finite(item.refund, nameof(item.refund)); Finite(item.buildTime, nameof(item.buildTime));
                var target = result.Material(item.tier);
                target.CostMultiplier = Mathf.Max(.1f, item.cost);
                target.HealthMultiplier = Mathf.Max(.1f, item.health);
                target.DamageMultiplier = Mathf.Max(.1f, item.damage);
                target.RefundFraction = Mathf.Clamp01(item.refund);
                target.BuildTimeMultiplier = Mathf.Max(.1f, item.buildTime);
            }
            foreach (var item in weapons ?? Array.Empty<WeaponSettings>())
            {
                if (item == null || !Enum.IsDefined(typeof(StructureKind), item.kind)) throw new ArgumentException(UserText.Message("copy.f4e402cf5887131a"), nameof(weapons));
                Finite(item.health, nameof(item.health)); Finite(item.buildSeconds, nameof(item.buildSeconds)); Finite(item.range, nameof(item.range));
                Finite(item.damage, nameof(item.damage)); Finite(item.shotInterval, nameof(item.shotInterval)); Finite(item.radius, nameof(item.radius));
                Finite(item.flightSeconds, nameof(item.flightSeconds)); Finite(item.coneAngleDegrees, nameof(item.coneAngleDegrees));
                Finite(item.jumpRange, nameof(item.jumpRange)); Finite(item.slowSpeedMultiplier, nameof(item.slowSpeedMultiplier)); Finite(item.slowDuration, nameof(item.slowDuration));
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
                target.ConeAngleDegrees = Mathf.Clamp(item.coneAngleDegrees, 1, 360); target.MaxTargets = Mathf.Max(1,item.maxTargets);
                target.JumpRange = Mathf.Max(0, item.jumpRange);
                target.SlowSpeedMultiplier = Mathf.Clamp(item.slowSpeedMultiplier, .01f, 1);
                target.SlowDuration = Mathf.Max(0, item.slowDuration);
            }
            foreach (var item in enemies ?? Array.Empty<EnemySettings>())
            {
                if (item == null || !Enum.IsDefined(typeof(EnemyKind), item.kind)) throw new ArgumentException(UserText.Message("copy.43725e3a4690712a"), nameof(enemies));
                Finite(item.health, nameof(item.health)); Finite(item.speed, nameof(item.speed)); Finite(item.damage, nameof(item.damage));
                Finite(item.attackInterval, nameof(item.attackInterval)); Finite(item.structureDamageMultiplier, nameof(item.structureDamageMultiplier));
                Finite(item.range, nameof(item.range)); Finite(item.windupSeconds, nameof(item.windupSeconds)); Finite(item.projectileSeconds, nameof(item.projectileSeconds));
                var target = result.Enemy(item.kind);
                target.MaxHealth = Mathf.Max(1, item.health);
                target.Speed = Mathf.Max(.1f, item.speed);
                target.Damage = Mathf.Max(.1f, item.damage);
                target.AttackInterval = Mathf.Max(.1f, item.attackInterval);
                target.StructureDamageMultiplier = Mathf.Max(0, item.structureDamageMultiplier);
                target.Reward = Mathf.Max(0, item.reward);
                target.Range = Mathf.Max(0,item.range);
                target.WindupSeconds = Mathf.Max(.1f,item.windupSeconds);
                target.ProjectileSeconds = Mathf.Max(.1f,item.projectileSeconds);
            }
            if (waves != null && waves.Length > 0)
            {
                result.Waves.Clear();
                foreach (var item in waves)
                {
                    if (item == null) throw new ArgumentException(UserText.Message("copy.74b59870962157a2"), nameof(waves));
                    Finite(item.spawnInterval, nameof(item.spawnInterval)); Finite(item.runners, nameof(item.runners)); Finite(item.gnawers, nameof(item.gnawers));
                    Finite(item.spitters, nameof(item.spitters)); Finite(item.outlined, nameof(item.outlined)); Finite(item.hatched, nameof(item.hatched));
                    result.Waves.Add(new WaveDefinition { Title = item.title, Count = Mathf.Max(1, item.count),
                        InkGrant = Mathf.Max(-1, item.inkGrant),
                        SpawnInterval = Mathf.Max(.005f, item.spawnInterval), RunnerFraction = Mathf.Clamp01(item.runners),
                        GnawerFraction = Mathf.Clamp(item.gnawers, 0, 1 - Mathf.Clamp01(item.runners)),
                        SpitterFraction=Mathf.Clamp(item.spitters,0,1-Mathf.Clamp01(item.runners)-Mathf.Clamp(item.gnawers,0,1-Mathf.Clamp01(item.runners))),
                        OutlinedFraction=Mathf.Clamp01(item.outlined),HatchedFraction=Mathf.Clamp(item.hatched,0,1-Mathf.Clamp01(item.outlined)),
                        Sides = item.sides == 0 ? 15 : item.sides });
                }
            }
            BalanceValidation.Validate(result);
            return result;
        }

        public GameBalance CreateWorkshopBalance()
        {
            if (dataPreset != null) return dataPreset.CreateBalance();
            if (!legacyAuthoringAdapter) throw new ArgumentException(UserText.Message("copy.7a045f879970f558", name));
            Finite(wellHealth, nameof(wellHealth)); Finite(blotterRadius, nameof(blotterRadius)); Finite(blotterCooldown, nameof(blotterCooldown));
            Finite(blotLifetime, nameof(blotLifetime)); Finite(blotBuffSeconds, nameof(blotBuffSeconds));
            Finite(blotSpeedBonus, nameof(blotSpeedBonus)); Finite(blotReduction, nameof(blotReduction));
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
            if (useTargetWorkshopPreset) result = GameBalancePresets.ApplyTargetWorkshop(result,targetSettings);
            BalanceValidation.Validate(result);
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
            dataPreset = null;
            legacyAuthoringAdapter = true;
            var b = GameBalance.CreateDefault();
            mechanics = b.Mechanics.CreateSnapshot();
            useTargetWorkshopPreset = false; targetSettings = new TargetPresetSettings();
            construction = b.Construction.CreateSnapshot(); effects = b.Effects.CreateSnapshot(); progression = b.Progression.CreateSnapshot();
            width = b.Width; height = b.Height; startingInk = b.StartingInk; baseHealth = b.BaseHealth;
            planningSeconds = b.PlanningSeconds; intermissionSeconds = b.IntermissionSeconds;
            maxEnemiesPerCell = b.MaxEnemiesPerCell;
            passiveInkPerSecond=b.PassiveInkPerSecond;waveInkGrant=b.WaveInkGrant;
            earlyLaunchInkPerSecond=b.EarlyLaunchInkPerSecond;maxEarlyLaunchInk=b.MaxEarlyLaunchInk;
            drawingMinimumRate=b.DrawingMinimumRate;drawingMaximumRate=b.DrawingMaximumRate;
            materials = new MaterialSettings[b.MaterialDefinitions.Count];
            for (int i = 0; i < materials.Length; i++)
            { var d = b.Material((MaterialTier)i); materials[i] = new MaterialSettings { tier = (MaterialTier)i, cost = d.CostMultiplier,
                health = d.HealthMultiplier, damage = d.DamageMultiplier, refund = d.RefundFraction, buildTime = d.BuildTimeMultiplier }; }
            weapons = new WeaponSettings[b.StructureDefinitions.Count];
            for (int i = 0; i < weapons.Length; i++)
            { var d = b.StructureDefinitions[i]; weapons[i] = new WeaponSettings {kind = d.Kind, cost = d.Cost,
                health = d.MaxHealth, buildSeconds = d.BuildSeconds, range = d.Range, damage = d.Damage,
                shotInterval = d.ShotInterval, radius = d.Radius, pierce = d.MaxPierce, flightSeconds = d.FlightSeconds,
                coneAngleDegrees=d.ConeAngleDegrees,maxTargets=d.MaxTargets,jumpRange=d.JumpRange,
                slowSpeedMultiplier=d.SlowSpeedMultiplier,slowDuration=d.SlowDuration }; }
            enemies = new EnemySettings[b.EnemyDefinitions.Count];
            for (int i = 0; i < enemies.Length; i++)
            { var d = b.EnemyDefinitions[i]; enemies[i] = new EnemySettings {kind = d.Kind, health = d.MaxHealth,
                speed = d.Speed, damage = d.Damage, attackInterval = d.AttackInterval, reward = d.Reward,
                range=d.Range,windupSeconds=d.WindupSeconds,projectileSeconds=d.ProjectileSeconds,
                structureDamageMultiplier=d.StructureDamageMultiplier}; }
            waves = new WaveSettings[b.Waves.Count];
            for (int i = 0; i < waves.Length; i++)
            {var d = b.Waves[i]; waves[i] = new WaveSettings {title = d.Title, count = d.Count, spawnInterval = d.SpawnInterval, inkGrant=d.InkGrant,
                runners = d.RunnerFraction, gnawers = d.GnawerFraction, spitters=d.SpitterFraction,
                outlined=d.OutlinedFraction,hatched=d.HatchedFraction,sides = d.Sides};}
        }

        // Reject nonfinite source values before clamps could disguise an invalid Inspector configuration.
        static void Finite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }

        [Serializable] public sealed class MaterialSettings
        { public MaterialTier tier; public float cost = 1, health = 1, damage = 1; [Range(0,1)] public float refund = .8f; public float buildTime = 1; }
        [Serializable] public sealed class WeaponSettings
        { public StructureKind kind; public int cost = 20; public float health = 30, buildSeconds = 1, range = 8, damage = 10, shotInterval = 1, radius = 2, flightSeconds = .8f;
            public float coneAngleDegrees = 90, jumpRange, slowSpeedMultiplier = 1, slowDuration; public int maxTargets = 8; public int pierce = 4; }
        [Serializable] public sealed class EnemySettings
        {public EnemyKind kind; public float health = 15, speed = 2, damage = 4, attackInterval = 1,range=12.5f,windupSeconds=.8f,projectileSeconds=.65f;
            public float structureDamageMultiplier = 1; public int reward = 1;}
        [Serializable] public sealed class WaveSettings
        {public string title; public int count = 100; public float spawnInterval = .08f; [Range(0,1)] public float runners, gnawers,spitters,outlined,hatched;
            public int inkGrant = -1;
            [Tooltip("Запад = 1, восток = 2, север = 4, юг = 8. Суммируйте направления.")] public int sides = 1; }
    }
}
