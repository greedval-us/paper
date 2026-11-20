using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Schedules mount acquisition, feed, reloads and firing for each tower in the simulation step.</summary>
    internal sealed partial class WeaponSystem
    {
        private readonly GameBalance balance;
        private readonly ContentCatalogSnapshot catalog;
        private readonly Dictionary<StructureKind,WeaponDefinition> directWeapons = new Dictionary<StructureKind,WeaponDefinition>();
        private readonly MechanicsAvailability mechanics;
        private readonly EffectsRules effects;
        private readonly ColorEffectPayload redPayload,bluePayload,greenPayload;
        private readonly IBlotContext blotContext;
        private readonly EnemySpatialIndex spatial;
        private readonly List<EnemyState> targets = new List<EnemyState>(128);
        private readonly WeaponAttackResolver attacks;
        private double clockSeconds;
        private long visualTick;
        private SimulationEventBuffer visualEvents;

        internal IReadOnlyList<MortarProjectile> Projectiles => attacks.Projectiles;
        internal bool HasPendingProjectiles => attacks.Projectiles.Count != 0;
        internal bool TryGetDirectWeapon(StructureKind kind, out WeaponDefinition weapon) => directWeapons.TryGetValue(kind, out weapon);

        internal WeaponSystem(GameBalance balance, IBlotContext blotContext = null)
        {
            balance=balance.CreateSnapshot();
            this.balance = balance;
            catalog = balance.ResolveContent();
            foreach (var stats in balance.StructureDefinitions)
            {
                string id = LegacyContentIds.Weapon(stats.Kind);
                if (catalog.TryGetWeapon(id,out var known)) directWeapons.Add(stats.Kind,known);
                else directWeapons.Add(stats.Kind,new WeaponDefinition(new WeaponDefinitionInput { Id=id,Name=stats.Kind.ToString(),Stats=stats,Attack=LegacyContentCatalog.Attack(stats.Kind),LegacyKind=stats.Kind,MinimumRadialRange=balance.Combat.MinimumRadialRange }));
            }
            mechanics = MechanicsAvailability.Resolve(balance);
            effects = balance.Effects.ForEnemyDrawingTiers(mechanics.EnemyDrawingTiers);
            redPayload=ColorEffectPayload.Capture(InkColor.Red,catalog);bluePayload=ColorEffectPayload.Capture(InkColor.Blue,catalog);greenPayload=ColorEffectPayload.Capture(InkColor.Green,catalog);
            // An injected fixture may provide marks without campaign corpse generation.
            this.blotContext = balance.Mechanics.CorpseBlots ? blotContext : null;
            spatial = new EnemySpatialIndex(balance.Width, balance.Height);
            attacks = new WeaponAttackResolver(spatial, targets, mechanics, effects, balance.BlotReduction);
        }

        internal void Fire(List<StructureState> structures, List<EnemyState> enemies, List<ShotEvent> shots,
            float fireMultiplier, float damageMultiplier, float delta, long tick,
            RunStatistics statistics, SimulationEventBuffer events, double? timeSeconds = null)
        {
            clockSeconds = timeSeconds ?? clockSeconds + delta;
            visualEvents=events;visualTick=tick;
            spatial.Rebuild(enemies);
            // One exposure measurement per operable attacking tower, independent of its mount count.
            foreach (var structure in structures)
                if (CanOperate(structure) && HasDamagingWeapon(structure))
                    statistics.RecordPositionActive(structure.Id, structure.Cell, structure.ContentId, delta);
            // Composite actions retain their moments inside the step. Historical shells and
            // weapon actions retain their original cadence at the end of the simulation tick.
            FireComposites(structures, shots, fireMultiplier, damageMultiplier, delta, tick, statistics, events);
            attacks.AdvanceLegacyProjectiles(delta, tick, clockSeconds, statistics, events);
            foreach (var structure in structures)
            {
                if (!CanOperate(structure)) continue;
                if (structure.ResolvedEntry != null) continue;
                Point2 origin = new Point2(structure.Cell.X + 0.5f, structure.Cell.Y + 0.5f);
                TowerBlueprint blueprint = structure.Blueprint;
                ResolvedTowerAssembly assembly = Assembly(structure);
                bool dynamic = balance.Construction.DynamicFeed;
                int activeMounts = blueprint?.WeaponMounts.Count ?? 1;
                if (dynamic && blueprint != null)
                {
                    activeMounts=0;
                    for(int mount=0;mount<assembly.Weapons.Length;mount++)
                        if(assembly.Weapons[mount]!=null&&ConsumesProduction(structure,origin,assembly.Weapons[mount],mount,delta))activeMounts++;
                }
                float feed = blueprint == null ? 1 : TowerCatalog.FeedMultiplier(assembly, balance, activeMounts);
                ColorEffectPayload payload = mechanics.ColorCartridges ? ColorPayload(structure) : default;
                var colorBudget = ColorBudget(structure, payload);
                if(assembly==null)FireMount(structure,origin,directWeapons[structure.Kind],0,shots,
                        fireMultiplier * feed, dynamic, damageMultiplier, payload, colorBudget, delta, tick, statistics, events);
                else for(int mount=0;mount<assembly.Weapons.Length;mount++)if(assembly.Weapons[mount]!=null)
                    FireMount(structure,origin,assembly.Weapons[mount],mount,shots,
                        fireMultiplier*feed,dynamic,damageMultiplier,payload,colorBudget,delta,tick,statistics,events);
            }
        }

        // A geometry probe runs one existing mount, retaining its actual ID and scoped equipment.
        internal void FireIsolatedMount(StructureState structure,int mount,List<EnemyState> enemies,List<ShotEvent> shots,
            float delta,long tick,RunStatistics statistics,SimulationEventBuffer events,double timeSeconds)
        {
            clockSeconds=timeSeconds;visualEvents=events;visualTick=tick;spatial.Rebuild(enemies);
            isolatedComposite.Clear();
            if(structure?.ResolvedEntry!=null&&CanOperate(structure))isolatedComposite.Add(structure);
            // Shells own their launch parameters and keep flying after their source is removed.
            FireComposites(isolatedComposite,shots,1,1,delta,tick,statistics,events,isolatedComposite.Count==0?-1:mount);
            attacks.AdvanceLegacyProjectiles(delta, tick, clockSeconds, statistics, events);
            if(structure==null||!CanOperate(structure)||structure.ResolvedEntry!=null)return;
            var assembly=Assembly(structure);var weapon=mount>=0&&mount<assembly.Weapons.Length?assembly.Weapons[mount]:null;
            if(weapon==null)throw new ArgumentOutOfRangeException(nameof(mount));
            Point2 origin=new Point2(structure.Cell.X+.5f,structure.Cell.Y+.5f);
            bool dynamic=balance.Construction.DynamicFeed;
            float feed=dynamic?TowerCatalog.FeedMultiplier(assembly,balance,ConsumesProduction(structure,origin,weapon,mount,delta)?1:0)
                :Math.Min(balance.Construction.MaxFeedMultiplier,assembly.Modify(ModifierStat.FeedMultiplier,1));
            FireMount(structure,origin,weapon,mount,shots,feed,dynamic,1,default,null,delta,tick,statistics,events);
        }

        bool CanOperate(StructureState structure)
            => !structure.Modernization.IsWorking && !structure.ModernizationBlockedThisStep && structure.Health > 0 && structure.IsComplete && structure.BuildRemaining <= 0 && (structure.ResolvedEntry == null || structure.ResolvedEntry.Weapons.Count > 0) && structure.Kind != StructureKind.Wall && structure.Kind != StructureKind.TarPit && structure.Kind != StructureKind.RepairPost
                && (structure.Blueprint==null || (Assembly(structure).FreeformDesign?.IsUsable ?? true));
        bool HasDamagingWeapon(StructureState structure)
        {
            if (structure.ResolvedEntry != null) return ModernizationRules.HasAttack(structure.ResolvedEntry);
            if (structure.Blueprint == null && ConstructionToolRules.IsTool(structure.Kind)) return false;
            var assembly = Assembly(structure);
            if (assembly != null)
            {
                foreach (var weapon in assembly.Weapons)
                    if (weapon != null && weapon.Attack != AttackKind.Cleanup && weapon.Numeric.Damage > 0) return true;
                return false;
            }
            return directWeapons.TryGetValue(structure.Kind, out var direct) && direct.Attack != AttackKind.Cleanup && direct.Numeric.Damage > 0;
        }
        ColorEffectPayload ColorPayload(StructureState tower) => mechanics.FieldModernization ? ModernizationRules.ResolveColor(ColorPayload(tower.Color),tower.Modernization.ColorRank) : ColorPayload(tower.Color);
        ColorEffectPayload ColorPayload(InkColor color)=>color==InkColor.Red?redPayload:color==InkColor.Blue?bluePayload:color==InkColor.Green?greenPayload:default;

        TowerColorBudget ColorBudget(StructureState tower,ColorEffectPayload payload)
        {
            if (payload.Color == InkColor.None || (!mechanics.FieldModernization && !balance.Effects.LimitTowerColor)) return null;
            if (tower.ColorBudget == null)
                tower.ColorBudget = new TowerColorBudget(mechanics.FieldModernization ? 1 : balance.Effects.ColorWindowSeconds,
                    mechanics.FieldModernization ? 3 + Math.Max(0,tower.Modernization.ColorRank-1) : balance.Effects.ColorTargetsPerWindow);
            return tower.ColorBudget;
        }

        // Production is shared by unfinished reloads and acquired working mounts. A ready idle
        // mount releases its share. This preview never mutates tracking: each barrel revalidates
        // its actual target after the previous barrel has dealt damage or cleaned a blot.
        private bool ConsumesProduction(StructureState structure, Point2 origin, WeaponDefinition weapon, int mount, float delta)
        {
            var state=structure.GetMountState(mount);
            if (state.ActionRemaining > 0) return true;
            float range = MountRange(structure, weapon, mount);
            var sector = SectorFor(structure, weapon);
            if(weapon.Attack==AttackKind.Cleanup) return TryFindBlot(origin,weapon.MinimumRange,range,sector,out _);
            spatial.Query(origin, range, targets);
            FilterRange(origin,weapon.MinimumRange,range,sector,targets);
            EnemyState target = PickTarget(structure, origin, targets);
            if (target == null) return false;
            int previousId = state.TargetId;
            float remaining = previousId == target.Id
                ? state.AcquisitionRemaining
                : TowerCatalog.AcquisitionSeconds(Assembly(structure), balance, mount);
            return remaining <= delta + .000001f;
        }

        private void FireMount(StructureState structure, Point2 origin, WeaponDefinition weapon, int mount,
            List<ShotEvent> shots, float fireMultiplier, bool dynamic, float damageMultiplier,
            ColorEffectPayload payload, TowerColorBudget colorBudget, float delta, long tick,
            RunStatistics statistics, SimulationEventBuffer events)
        {
            var definition = weapon.Numeric;
            var state=structure.GetMountState(mount);
            float range = MountRange(structure, weapon, mount);
            float interval = MountInterval(structure,weapon,mount,fireMultiplier);
            if (dynamic) AdvanceReload(structure, mount, interval, delta);
            if(weapon.Attack==AttackKind.Cleanup)
            {
                FireBlotter(structure,origin,range,weapon,mount,shots,interval,dynamic,delta,tick,statistics,events);
                return;
            }
            spatial.Query(origin, range, targets);
            FilterRange(origin,weapon.MinimumRange,range,SectorFor(structure, weapon),targets);
            EnemyState target = PickTarget(structure, origin, targets);
            bool acquired = TrackTarget(structure, target, mount, delta);
            state.Aim = target == null ? structure.RestAim : target.Position;
            if (target == null) { statistics.RecordIdle(weapon.Id, delta); return; }
            if (!acquired) return;
            if(!TryBeginAction(structure,mount,interval,dynamic)) return;
            float damage = TowerCatalog.FiniteStat(Math.Max(0,definition.Damage*balance.Material(structure.Tier).DamageMultiplier*damageMultiplier
                *(Assembly(structure)?.FreeformDesign?.GetBarrel(mount)?.DamageMultiplier??1)),"damage");
            if (structure.ModernizationState != null) damage *= ModernizationRules.DamageMultiplier(structure.ModernizationState.Ranks);
            structure.Aim = target.Position;
            var shot = new WeaponAttack {
                Source = structure, Mount = mount, Weapon = weapon, Origin = origin, Aim = target.Position,
                Target = target, Range = range, Damage = damage, Payload = payload, ColorBudget = colorBudget,
                PierceLimit = Math.Max(1, definition.MaxPierce),
                Footprint = weapon.SupportsFootprint ? structure.Blueprint?.GetMortarFootprint(mount) : null,
                Volley = weapon.SupportsVolley ? structure.Blueprint?.GetWeaponMount(mount)?.Volley : null
            };
            attacks.Fire(shot, shots, tick, clockSeconds, statistics, events);
        }

        ResolvedTowerAssembly Assembly(StructureState structure)
        {
            if (structure.Blueprint == null) return null;
            if (structure.Assembly == null) structure.Assembly = TowerCatalog.Resolve(structure.Blueprint,catalog);
            return structure.Assembly;
        }
        float MountRange(StructureState structure,WeaponDefinition weapon,int mount)
        {
            float range=TowerCatalog.WeaponRange(Assembly(structure),weapon,mount);
            return structure.ModernizationState==null?range:range*ModernizationRules.RangeMultiplier(structure.ModernizationState.Ranks);
        }
        float MountInterval(StructureState structure,WeaponDefinition weapon,int mount,float production)
        {
            float interval=TowerCatalog.ActionInterval(Assembly(structure),weapon,mount,balance,production);
            return structure.ModernizationState==null?interval:ModernizationRules.WorkInterval(interval,structure.ModernizationState.Ranks,balance.Combat.MinimumActionInterval);
        }
        static FiringSector SectorFor(StructureState structure, WeaponDefinition weapon)
            => weapon.Attack == AttackKind.Radial ? default : new FiringSector(structure, weapon.FiringArcDegrees);
        private bool TrackTarget(StructureState structure, EnemyState target, int mount, float delta)
        {
            var state=structure.GetMountState(mount);
            int previousId = state.TargetId;
            float remaining = state.AcquisitionRemaining;
            int id = target == null ? 0 : target.Id;
            if (target == null || structure.Blueprint == null) remaining = 0;
            else if (previousId != id) remaining = TowerCatalog.AcquisitionSeconds(Assembly(structure), balance, mount);
            remaining = Math.Max(0, remaining - delta);
            state.TargetId=id;state.AcquisitionRemaining=remaining;
            return remaining <= .000001f;
        }

        private static void AdvanceReload(StructureState structure, int mount, float interval, float delta)
        {
            var state=structure.GetMountState(mount);
            float remaining = Math.Max(0, state.ActionRemaining - delta / interval);
            if (remaining <= .000001f) remaining = 0;
            state.ActionRemaining=remaining;state.Cooldown=remaining*interval;
        }

        // Preparation advances readiness without running target searches, attacks or cleanup.
        // Assault uses Fire instead, so a simulation step never advances a reload twice.
        internal void ReloadWithoutTargets(List<StructureState> structures, float fireMultiplier, float delta)
        {
            foreach (var structure in structures)
            {
                if (!CanOperate(structure)) continue;
                if (structure.ResolvedEntry != null)
                {
                    ReloadComposite(structure,fireMultiplier,delta);
                    continue;
                }
                if (!balance.Construction.DynamicFeed) continue;
                TowerBlueprint blueprint = structure.Blueprint;
                ResolvedTowerAssembly assembly = Assembly(structure);
                int pending=0;for(int mount=0;mount<(assembly==null?1:assembly.Weapons.Length);mount++)
                    if((assembly==null||assembly.Weapons[mount]!=null)&&structure.GetMountState(mount).ActionRemaining>0)pending++;
                float production = fireMultiplier * (blueprint == null ? 1 : TowerCatalog.FeedMultiplier(assembly, balance, pending));
                for(int mount=0;mount<(assembly==null?1:assembly.Weapons.Length);mount++)
                {
                    var weapon=assembly==null?directWeapons[structure.Kind]:assembly.Weapons[mount];if(weapon==null)continue;
                    AdvanceReload(structure,mount,MountInterval(structure,weapon,mount,production),delta);
                    var state=structure.GetMountState(mount);state.TargetId=0;state.AcquisitionRemaining=0;
                }
            }
        }

        private static bool TryBeginAction(StructureState structure, int mount, float cooldown, bool dynamic)
        {
            var state=structure.GetMountState(mount);
            if(dynamic)
            {
                if(state.ActionRemaining>0) return false;
                state.ActionRemaining=1;
            }
            else if(state.Cooldown>0) return false;
            state.Cooldown=cooldown;
            return true;
        }

        private void FireBlotter(StructureState structure,Point2 origin,float range,WeaponDefinition weapon,
            int mount,List<ShotEvent> shots,float interval,bool dynamic,float delta,
            long tick,RunStatistics statistics,SimulationEventBuffer events)
        {
            var definition=weapon.Numeric;
            var state=structure.GetMountState(mount);
            if(!TryFindBlot(origin,weapon.MinimumRange,range,SectorFor(structure, weapon),out Cell cell))
            {
                state.TargetId=0;state.AcquisitionRemaining=0;state.Aim=structure.RestAim;
                statistics.RecordIdle(weapon.Id,delta); return;
            }
            Point2 target=new Point2(cell.X+.5f,cell.Y+.5f);
            int targetId=-(cell.Y*balance.Width+cell.X+1);
            state.TargetId=targetId;state.Aim=target;
            if(!TryBeginAction(structure,mount,interval,dynamic)) return;
            float radius=Math.Max(0,definition.Radius);
            int cleared=blotContext.Clear(cell,radius);
            if(cleared<=0) return;
            structure.Aim = target;
            var shot = new WeaponAttack {
                Source = structure, Mount = mount, Weapon = weapon, Origin = origin, Aim = target,
                Range = range, ClearedBlots = cleared
            };
            attacks.Fire(shot, shots, tick, clockSeconds, statistics, events);
        }

        bool TryFindBlot(Point2 origin,float minimumRange,float maximumRange,FiringSector sector,out Cell cell)
        {
            cell=default;if(blotContext==null||maximumRange<=0)return false;
            return blotContext.TryFindNearest(origin,minimumRange,maximumRange,sector,out cell);
        }

        private static EnemyState PickTarget(StructureState structure, Point2 origin, List<EnemyState> candidates)
        {
            EnemyState best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                if (candidate.Health <= 0) continue;
                float distance = DistanceSquared(origin, candidate.Position);
                bool preferred = best == null;
                if (best != null)
                {
                    if (structure.Priority == TargetPriority.Siege && (candidate.Kind == EnemyKind.Gnawer) != (best.Kind == EnemyKind.Gnawer))
                        preferred = candidate.Kind == EnemyKind.Gnawer;
                    else if (structure.Priority == TargetPriority.Strongest && candidate.Health != best.Health)
                        preferred = candidate.Health > best.Health;
                    else preferred = distance < bestDistance || (distance == bestDistance && candidate.Id < best.Id);
                }
                if (preferred) { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        static void FilterRange(Point2 origin,float minimum,float maximum,FiringSector sector,List<EnemyState> candidates)
        {
            if(maximum<=0){candidates.Clear();return;}
            float minSquared=minimum*minimum,maxSquared=maximum*maximum;
            int write=0;
            for(int read=0;read<candidates.Count;read++)
            {
                var enemy=candidates[read];float distance=DistanceSquared(origin,enemy.Position);
                if(distance<minSquared||distance>maxSquared||!sector.Contains(enemy.Position.X-origin.X,enemy.Position.Y-origin.Y))continue;
                candidates[write++]=enemy;
            }
            if(write<candidates.Count)candidates.RemoveRange(write,candidates.Count-write);
        }

        private static float DistanceSquared(Point2 a, Point2 b)
        { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

    }
}
