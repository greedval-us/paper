using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Content Balance Preset", fileName = "BalancePreset")]
    public sealed class BalancePresetAsset : ScriptableObject
    {
        public ContentDatabaseAsset database;
        [Tooltip("Глобальные правила. Параметры конкретных деталей находятся в database. Старые поля адаптера скрыты редактором.")]
        public GlobalBalanceSettings rules = GlobalBalanceSettings.From(GameBalance.CreateDefault());
        [Tooltip("Явная полная замена записи базы по тому же ID. Отсутствие записи наследует базу; 0/false — значения.")]
        public ContentOverrides overrides = new ContentOverrides();
        public GameBalance CreateBalance()
        {
            if (database == null) throw new ArgumentException(UserText.Message("copy.de1a8a9825d89d36", name));
            if (rules == null) throw new ArgumentException(UserText.Message("copy.2af4b2dcf19816a2", name));
            var result = rules.CreateBalance();
            result.Content = database.CreateSnapshot(overrides);
            result.Content.GetChassis(result.Trials.BaselineChassisId); result.Content.GetWeapon(result.Trials.BaselineWeaponId);
            PopulateLegacyAdapters(result);
            result.Combat.CreateSnapshot(); result.Trials.CreateSnapshot();
            var cells = new HashSet<Cell>();
            foreach (var placement in result.WellPlacements)
            {
                if (placement == null) throw new ArgumentException(UserText.Message("copy.8b3d5bd5d9478c20", name));
                placement.CreateSnapshot();
                if (!cells.Add(placement.Resolve(result.Width, result.Height))) throw new ArgumentException(UserText.Message("copy.9e9a2c80f3e49b46", name));
            }
            BalanceValidation.Validate(result);
            ValidateStartup(result);
            return result;
        }

        void ValidateStartup(GameBalance balance)
        {
            bool enabledChassis = false, enabledWeapon = false;
            foreach (var chassis in balance.Content.Chassis) enabledChassis |= chassis.Enabled;
            foreach (var weapon in balance.Content.Weapons) enabledWeapon |= weapon.Enabled;
            if (!enabledChassis) throw new ArgumentException(UserText.Message("copy.66b3d3441e520b7c", name));
            if (!enabledWeapon) throw new ArgumentException(UserText.Message("copy.5e38f420fc63b0fb", name));
            foreach (var blueprint in balance.Content.StartupBlueprints)
                if (!TowerCatalog.Validate(blueprint, balance, out string reason))
                    throw new ArgumentException(name + ".database.catalog.StartingPages." + blueprint.Id + ": " + reason);
            WorkshopProfile profile;
            try { profile = new WorkshopProfile(balance.Content, balance); }
            catch (ArgumentException error)
            { throw new ArgumentException(name + ".database.catalog.StartingPages: " + error.Message, error); }
            var loadout = profile.GetLoadoutBlueprints();
            for (int slot = 0; slot < loadout.Count; slot++)
                if (loadout[slot] != null && !profile.ValidateOwned(loadout[slot], out string reason, balance))
                    throw new ArgumentException(name + ".database.catalog.StartingPages.slot[" + slot + "]: " + reason);
        }

        // One explicit migration/compatibility projection. New content never needs a new enum or a case here.
        static void PopulateLegacyAdapters(GameBalance result)
        {
            var content = result.Content; var r = result.Construction;
            var definitions = new List<StructureDefinition>();
            foreach (var item in result.StructureDefinitions)
                if (ConstructionToolRules.IsTool(item.Kind)) definitions.Add(item);
            foreach (var weapon in content.Weapons)
                if (weapon.LegacyKind.HasValue)
                {
                    var s = weapon.Stats;
                    float slowMultiplier=s.SlowSpeedMultiplier,slowDuration=s.SlowDuration;
                    foreach(var effect in weapon.Effects)if(effect.Kind==EffectKind.Slow){slowMultiplier=effect.Strength;slowDuration=effect.Duration;break;}
                    definitions.Add(new StructureDefinition { Kind = weapon.LegacyKind.Value, Cost = s.Cost, MaxHealth = s.MaxHealth, BuildSeconds = s.BuildSeconds,
                        Range = s.Range, Damage = s.Damage, ShotInterval = s.ShotInterval, Radius = s.Radius, FlightSeconds = s.FlightSeconds, MaxPierce = s.MaxPierce,
                        ConeAngleDegrees = s.ConeAngleDegrees, MaxTargets = s.MaxTargets, JumpRange = s.JumpRange, SlowSpeedMultiplier = slowMultiplier, SlowDuration = slowDuration });
                }
            definitions.Sort((a,b) => ((int)a.Kind).CompareTo((int)b.Kind)); result.StructureDefinitions = definitions;
            if(content.TryGetChassis("chassis.standard",out var standard)) {r.StandardCapacity=standard.Capacity;r.StandardBaseCost=standard.BaseCost;r.FoundationHealth=standard.BaseHealth;r.SecondaryBuildSeconds=standard.SecondaryBuildSeconds;}
            if(content.TryGetChassis("chassis.heavy",out var heavy)) {r.HeavyCapacity=heavy.Capacity;r.HeavyBaseCost=heavy.BaseCost;r.HeavyHealthMultiplier=heavy.HealthMultiplier;r.HeavyBuildSeconds=heavy.BuildSeconds;r.HeavyUnlockGold=heavy.UnlockGold;}
            if(content.TryGetWeapon("weapon.gun",out var gun)){r.GunLoad=gun.Load;r.WeaponAssemblyCredit=gun.AssemblyCredit;}
            if(content.TryGetWeapon("weapon.piercer",out var piercer))r.PiercerLoad=piercer.Load;
            if(content.TryGetWeapon("weapon.mortar",out var mortar))r.MortarLoad=mortar.Load;
            if(content.TryGetWeapon("weapon.fan",out var fan)){r.FanLoad=fan.Load;r.FanUnlockGold=fan.UnlockGold;}
            if(content.TryGetWeapon("weapon.ricochet",out var ricochet)){r.RicochetLoad=ricochet.Load;r.RicochetUnlockGold=ricochet.UnlockGold;}
            if(content.TryGetWeapon("weapon.stapler",out var stapler)){r.StaplerLoad=stapler.Load;r.StaplerUnlockGold=stapler.UnlockGold;}
            if(content.TryGetWeapon("weapon.blotter",out var blotter)){r.BlotterLoad=blotter.Load;r.BlotterUnlockGold=blotter.UnlockGold;}
            if(content.TryGetModule("module.armor",out var armor))
            {r.ArmorLoad=armor.Load;r.ArmorCost=armor.Cost;r.ArmorBuildSeconds=armor.BuildSeconds;foreach(var m in armor.Modifiers){if(m.Stat==ModifierStat.FoundationHealth)r.ArmorHealthMultiplier=m.Value;if(m.Stat==ModifierStat.FoundationDefense)r.ArmorDefense=m.Value;}}
            if(content.TryGetModule("module.feed",out var feed))
            {r.FeedLoad=feed.Load;r.FeedCost=feed.Cost;r.FeedBuildSeconds=feed.BuildSeconds;r.FeedUnlockGold=feed.UnlockGold;foreach(var m in feed.Modifiers)if(m.Stat==ModifierStat.FeedMultiplier)r.FeedModuleMultiplier=m.Value;}
            if(content.TryGetModule("module.tracking",out var tracking))
            {r.TrackingLoad=tracking.Load;r.TrackingCost=tracking.Cost;r.TrackingBuildSeconds=tracking.BuildSeconds;r.TrackingUnlockGold=tracking.UnlockGold;foreach(var m in tracking.Modifiers)if(m.Stat==ModifierStat.AcquisitionSeconds)r.TrackingAcquisitionSeconds=m.Value;}
            if(content.TryGetModule("module.rail",out var rail))
            {r.RailLoad=rail.Load;r.RailCost=rail.Cost;r.RailBuildSeconds=rail.BuildSeconds;r.RailUnlockGold=rail.UnlockGold;foreach(var m in rail.Modifiers){if(m.Stat==ModifierStat.WeaponRange)r.RailRangeMultiplier=m.Value;if(m.Stat==ModifierStat.ActionInterval)r.RailIntervalMultiplier=m.Value;}}
            var red=content.GetEffect(content.RedEffectId);var blue=content.GetEffect(content.BlueEffectId);var green=content.GetEffect(content.GreenEffectId);
            result.Effects.RedDuration=red.Duration;result.Effects.RedDamagePerTick=red.Strength;result.Effects.RedTickSeconds=red.TickSeconds;result.Effects.RedIgnoresDefense=red.IgnoresDefense;
            result.Effects.BlueDuration=blue.Duration;result.Effects.BlueSpeedMultiplier=blue.Strength;result.Effects.GreenDuration=green.Duration;result.Effects.GreenDefenseReduction=green.Strength;
        }
    }
}
