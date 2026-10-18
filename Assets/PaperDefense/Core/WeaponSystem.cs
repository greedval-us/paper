using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>
    /// Target selection and weapon geometry live here, independently of wave and construction rules.
    /// Add new weapon effects in this system; all queries share its reusable spatial buckets.
    /// </summary>
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
        private readonly List<EnemyState> splashTargets = new List<EnemyState>(64);
        private readonly List<MortarProjectile> projectiles = new List<MortarProjectile>(32);
        private readonly HashSet<int> ricochetVictims = new HashSet<int>();
        private readonly EnemyDistanceComparer targetOrder = new EnemyDistanceComparer();
        private int nextProjectileId = 1;
        private double clockSeconds;
        private long nextActionId, visualTick, visualActionId;
        private int visualSourceStructureId, visualMountSlotIndex = -1;
        private SimulationEventBuffer visualEvents;

        internal IReadOnlyList<MortarProjectile> Projectiles => projectiles;
        internal bool HasPendingProjectiles => projectiles.Count != 0;

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
        }

        internal void Fire(List<StructureState> structures, List<EnemyState> enemies, List<ShotEvent> shots,
            float fireMultiplier, float damageMultiplier, float delta, long tick,
            RunStatistics statistics, SimulationEventBuffer events, double? timeSeconds = null)
        {
            clockSeconds = timeSeconds ?? clockSeconds + delta;
            visualEvents=events;visualTick=tick;
            spatial.Rebuild(enemies);
            // Composite actions retain their moments inside the step. Historical shells and
            // weapon actions retain their original cadence at the end of the simulation tick.
            FireComposites(structures, shots, fireMultiplier, damageMultiplier, delta, tick, statistics, events);
            AdvanceProjectiles(delta, tick, statistics, events, legacyOnly: true);
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
                ColorEffectPayload payload = mechanics.ColorCartridges ? ColorPayload(structure.Color) : default;
                TowerColorBudget colorBudget = null;
                if (balance.Effects.LimitTowerColor && payload.Color != InkColor.None)
                {
                    if (structure.ColorBudget == null)
                        structure.ColorBudget = new TowerColorBudget(balance.Effects.ColorWindowSeconds, balance.Effects.ColorTargetsPerWindow);
                    colorBudget = structure.ColorBudget;
                }
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
            AdvanceProjectiles(delta,tick,statistics,events,legacyOnly:true);
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
            => structure.Health > 0 && structure.IsComplete && structure.BuildRemaining <= 0 && (structure.ResolvedEntry == null || structure.ResolvedEntry.Weapons.Count > 0) && structure.Kind != StructureKind.Wall && structure.Kind != StructureKind.TarPit && structure.Kind != StructureKind.RepairPost
                && (structure.Blueprint==null || (Assembly(structure).FreeformDesign?.IsUsable ?? true));
        ColorEffectPayload ColorPayload(InkColor color)=>color==InkColor.Red?redPayload:color==InkColor.Blue?bluePayload:color==InkColor.Green?greenPayload:default;

        // Production is shared by unfinished reloads and acquired working mounts. A ready idle
        // mount releases its share. This preview never mutates tracking: each barrel revalidates
        // its actual target after the previous barrel has dealt damage or cleaned a blot.
        private bool ConsumesProduction(StructureState structure, Point2 origin, WeaponDefinition weapon, int mount, float delta)
        {
            var state=structure.GetMountState(mount);
            if (state.ActionRemaining > 0) return true;
            float range = MountRange(structure, weapon, mount);
            if(weapon.Attack==AttackKind.Cleanup) return TryFindBlot(origin,weapon.MinimumRange,range,out _);
            spatial.Query(origin, range, targets);
            FilterRange(origin,weapon.MinimumRange,range,targets);
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
            StructureKind kind = weapon.LegacyKind ?? (StructureKind)(-1);
            var state=structure.GetMountState(mount);
            float range = MountRange(structure, weapon, mount);
            float interval = TowerCatalog.ActionInterval(Assembly(structure),weapon,mount,balance,fireMultiplier);
            if (dynamic) AdvanceReload(structure, mount, interval, delta);
            if(weapon.Attack==AttackKind.Cleanup)
            {
                FireBlotter(structure,origin,range,weapon,mount,shots,interval,dynamic,delta,tick,statistics,events);
                return;
            }
            spatial.Query(origin, range, targets);
            FilterRange(origin,weapon.MinimumRange,range,targets);
            EnemyState target = PickTarget(structure, origin, targets);
            bool acquired = TrackTarget(structure, target, mount, delta);
            if(target!=null)state.Aim=target.Position;
            if (target == null) { statistics.RecordIdle(weapon.Id, delta); return; }
            if (!acquired) return;
            if(!TryBeginAction(structure,mount,interval,dynamic)) return;
            BeginVisualAction(structure,mount,origin,target.Position,weapon,tick,events);
            float damage = TowerCatalog.FiniteStat(Math.Max(0,definition.Damage*balance.Material(structure.Tier).DamageMultiplier*damageMultiplier
                *(Assembly(structure)?.FreeformDesign?.GetBarrel(mount)?.DamageMultiplier??1)),"damage");
            structure.Aim = target.Position;
            statistics.RecordLaunch(weapon.Id);
            switch (weapon.Attack)
            {
                case AttackKind.Line:
                    FirePiercer(origin, target.Position, range, Math.Max(weapon.MinimumLineRadius, definition.Radius),
                        Math.Max(1, definition.MaxPierce), damage, weapon, payload, colorBudget, statistics);
                    break;
                case AttackKind.AreaProjectile:
                    LaunchMortar(origin, target.Position, weapon, damage, payload, colorBudget,
                        structure.Blueprint == null ? MortarFootprint.Circle : structure.Blueprint.GetMortarFootprint(mount), tick, events);
                    return;
                case AttackKind.Cone:
                    var volley=structure.Blueprint?.GetWeaponMount(mount)?.Volley;
                    if(volley!=null)FireVolley(structure,origin,target.Position,range,volley,weapon,damage,payload,colorBudget,shots,tick,statistics,events);
                    else FireFan(structure, origin, target.Position, weapon, damage, payload, colorBudget, shots, tick, statistics, events);
                    return;
                case AttackKind.Ricochet:
                    FireRicochet(structure, origin, target, weapon, damage, payload, colorBudget, shots, tick, statistics, events);
                    return;
                case AttackKind.Radial:
                    if (colorBudget != null) SortTargets(origin, targets);
                    foreach (var victim in targets) ApplyDamage(victim, damage, weapon, payload, colorBudget, statistics);
                    break;
                default:
                    ApplyDamage(target, damage, weapon, payload, colorBudget, statistics);
                    break;
            }
            Point2 shotEnd = weapon.Attack == AttackKind.Line ? RayEnd(origin, structure.Aim, range) : structure.Aim;
            PublishShot(origin, shotEnd, weapon, definition.Radius, structure.Id, shots, tick, events);
        }

        ResolvedTowerAssembly Assembly(StructureState structure)
        {
            if (structure.Blueprint == null) return null;
            if (structure.Assembly == null) structure.Assembly = TowerCatalog.Resolve(structure.Blueprint,catalog);
            return structure.Assembly;
        }
        float MountRange(StructureState structure,WeaponDefinition weapon,int mount)
            => TowerCatalog.WeaponRange(Assembly(structure),weapon,mount);
        void BeginVisualAction(StructureState structure,int mount,Point2 from,Point2 to,WeaponDefinition weapon,long tick,SimulationEventBuffer events)
        {
            visualSourceStructureId=structure.Id;visualMountSlotIndex=mount;visualActionId=++nextActionId;
            events.Publish(tick,SimulationEventKind.WeaponActionStarted,from,to,weapon.LegacyKind??(StructureKind)(-1),
                entityId:structure.Id,weaponId:weapon.Id,sourceStructureId:structure.Id,mountSlotIndex:mount,
                actionId:visualActionId,simulationSeconds:clockSeconds,attack:weapon.Attack);
        }

        void PublishShot(Point2 from,Point2 to,WeaponDefinition weapon,float radius,int structureId,List<ShotEvent> shots,long tick,SimulationEventBuffer events)
        {
            StructureKind legacy = weapon.LegacyKind ?? (StructureKind)(-1);
            shots.Add(new ShotEvent(from,to,legacy,radius,weapon.Id,structureId,visualMountSlotIndex,visualActionId,clockSeconds));
            events.Publish(tick,SimulationEventKind.Shot,from,to,legacy,radius,structureId,weaponId:weapon.Id,
                sourceStructureId:structureId,mountSlotIndex:visualMountSlotIndex,actionId:visualActionId,simulationSeconds:clockSeconds,attack:weapon.Attack);
        }

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
                    AdvanceReload(structure,mount,TowerCatalog.ActionInterval(assembly,weapon,mount,balance,production),delta);
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
            if(!TryFindBlot(origin,weapon.MinimumRange,range,out Cell cell))
            {
                state.TargetId=0;state.AcquisitionRemaining=0;
                statistics.RecordIdle(weapon.Id,delta); return;
            }
            Point2 target=new Point2(cell.X+.5f,cell.Y+.5f);
            int targetId=-(cell.Y*balance.Width+cell.X+1);
            state.TargetId=targetId;state.Aim=target;
            if(!TryBeginAction(structure,mount,interval,dynamic)) return;
            float radius=Math.Max(0,definition.Radius);
            int cleared=blotContext.Clear(cell,radius);
            if(cleared<=0) return;
            BeginVisualAction(structure,mount,origin,target,weapon,tick,events);
            structure.Aim=target;
            statistics.RecordBlotsCleared(cleared); statistics.RecordLaunch(weapon.Id);
            PublishShot(origin, target, weapon, radius, structure.Id, shots, tick, events);
        }

        bool TryFindBlot(Point2 origin,float minimumRange,float maximumRange,out Cell cell)
        {
            cell=default;if(blotContext==null||maximumRange<=0)return false;
            if(blotContext is IBlotRangeContext ranged)return ranged.TryFindNearest(origin,minimumRange,maximumRange,out cell);
            if(!blotContext.TryFindNearest(origin,maximumRange,out cell))return false;
            var target=new Point2(cell.X+.5f,cell.Y+.5f);return DistanceSquared(origin,target)>=minimumRange*minimumRange;
        }

        private void LaunchMortar(Point2 from, Point2 to, WeaponDefinition weapon, float damage,
            ColorEffectPayload payload, TowerColorBudget colorBudget, MortarFootprint footprint,
            long tick, SimulationEventBuffer events, bool compositeCycle = false)
        {
            var definition=weapon.Numeric;
            float duration = Math.Max(0, definition.FlightSeconds);
            var projectile = new MortarProjectile
            {
                Id = nextProjectileId++, From = from, To = to, Duration = duration,
                Remaining = duration, Radius = Math.Max(0, definition.Radius),
                Damage = damage, Weapon = weapon.LegacyKind ?? (StructureKind)(-1), WeaponId = weapon.Id, Definition=weapon, Color = payload.Color, CompositeCycle=compositeCycle,
                EffectPayload = payload, ColorBudget = colorBudget,
                SourceStructureId=visualSourceStructureId,MountSlotIndex=visualMountSlotIndex,ActionId=visualActionId,
                ImpactFootprint = FootprintGeometry.Resolve(footprint, definition.Radius, from, to)
            };
            projectiles.Add(projectile);
            events.Publish(tick, SimulationEventKind.ProjectileLaunched, from, to, projectile.Weapon,
                projectile.Radius, projectile.Id, duration, projectile.ImpactFootprint, projectile.WeaponId,
                projectile.SourceStructureId,projectile.MountSlotIndex,projectile.ActionId,clockSeconds,attack:weapon.Attack);
        }

        private void FireVolley(StructureState structure,Point2 origin,Point2 aim,float range,FanVolley volley,WeaponDefinition weapon,
            float damage,ColorEffectPayload payload,TowerColorBudget colorBudget,List<ShotEvent> shots,long tick,RunStatistics statistics,SimulationEventBuffer events)
        {
            volley.Validate(weapon);var counts=volley.Allocate(weapon.VolleyPellets);
            var victims=new EnemyState[counts.Length];var ends=new Point2[counts.Length];
            // Resolve the entire volley against the same live targets before applying damage.
            for(int guide=0;guide<counts.Length;guide++)
            {
                var direction=volley.Direction(guide,origin,aim);double best=range;
                foreach(var enemy in targets)
                {
                    if(enemy.Health<=0)continue;
                    double x=(double)enemy.Position.X-origin.X,y=(double)enemy.Position.Y-origin.Y;
                    double along=x*direction.X+y*direction.Y,side=x*direction.Y-y*direction.X;
                    double radius=weapon.VolleyHitRadius,inside=radius*radius-side*side;
                    if(inside<0||along<0)continue;
                    double distance=Math.Max(0,along-Math.Sqrt(inside));
                    if(distance>best||distance==best&&victims[guide]!=null&&enemy.Id>=victims[guide].Id)continue;
                    best=distance;victims[guide]=enemy;
                }
                ends[guide]=new Point2(origin.X+direction.X*(float)best,origin.Y+direction.Y*(float)best);
            }
            for(int guide=0;guide<counts.Length;guide++)for(int pellet=0;pellet<counts[guide];pellet++)
            {
                if(victims[guide]!=null)ApplyDamage(victims[guide],damage,weapon,payload,colorBudget,statistics);
                PublishShot(origin,ends[guide],weapon,weapon.VolleyHitRadius,structure.Id,shots,tick,events);
            }
        }

        private void FireFan(StructureState structure, Point2 origin, Point2 aim, WeaponDefinition weapon,
            float damage, ColorEffectPayload payload, TowerColorBudget colorBudget, List<ShotEvent> shots,
            long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            var definition=weapon.Numeric;
            float dx = aim.X - origin.X, dy = aim.Y - origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= .00001f) { dx = 1; dy = 0; }
            else { dx /= length; dy /= length; }
            float cosine = (float)Math.Cos(Math.Max(0, Math.Min(360, definition.ConeAngleDegrees)) * Math.PI / 360);
            SortTargets(origin, targets);
            int hit = 0, limit = Math.Max(0, definition.MaxTargets);
            foreach (EnemyState victim in targets)
            {
                if (hit >= limit) break;
                if (victim.Health <= 0) continue;
                float vx = victim.Position.X - origin.X, vy = victim.Position.Y - origin.Y;
                float distance = (float)Math.Sqrt(vx * vx + vy * vy);
                if (distance > .00001f && (vx * dx + vy * dy) / distance + .000001f < cosine) continue;
                ApplyDamage(victim, damage, weapon, payload, colorBudget, statistics);
                PublishShot(origin, victim.Position, weapon, 0, structure.Id, shots, tick, events);
                hit++;
            }
        }

        private void FireRicochet(StructureState structure, Point2 origin, EnemyState victim, WeaponDefinition weapon,
            float damage, ColorEffectPayload payload, TowerColorBudget colorBudget, List<ShotEvent> shots,
            long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            var definition=weapon.Numeric;
            ricochetVictims.Clear();
            Point2 from=origin;
            int limit=Math.Max(0,definition.MaxTargets);
            float jumpRange=Math.Max(0,definition.JumpRange);
            for(int hit=0;victim!=null && hit<limit;hit++)
            {
                ricochetVictims.Add(victim.Id);
                Point2 impact=victim.Position;
                ApplyDamage(victim,damage,weapon,payload,colorBudget,statistics);
                PublishShot(from, impact, weapon, 0, structure.Id, shots, tick, events);
                if(hit+1>=limit) break;
                spatial.Query(impact,jumpRange,splashTargets);
                EnemyState next=null; float nearest=float.MaxValue;
                foreach(var candidate in splashTargets)
                {
                    if(candidate.Health<=0 || ricochetVictims.Contains(candidate.Id)) continue;
                    float distance=DistanceSquared(impact,candidate.Position);
                    if(next==null || distance<nearest || (distance==nearest && candidate.Id<next.Id))
                    {next=candidate;nearest=distance;}
                }
                from=impact; victim=next;
            }
        }

        private void AdvanceProjectiles(float delta, long tick, RunStatistics statistics, SimulationEventBuffer events, bool legacyOnly = false)
        {
            int write = 0;
            for (int read = 0; read < projectiles.Count; read++)
            {
                var projectile = projectiles[read];
                if (legacyOnly && projectile.CompositeCycle) { projectiles[write++] = projectile; continue; }
                projectile.Remaining = Math.Max(0, projectile.Remaining - delta);
                if (projectile.Remaining > 0.000001f) { projectiles[write++] = projectile; continue; }
                ImpactProjectile(projectile,tick,statistics,events);
            }
            if (write < projectiles.Count) projectiles.RemoveRange(write, projectiles.Count - write);
        }

        // Both historical and composite shells use this one impact operation.
        void ImpactProjectile(MortarProjectile projectile,long tick,RunStatistics statistics,SimulationEventBuffer events)
        {
            visualSourceStructureId=projectile.SourceStructureId;visualMountSlotIndex=projectile.MountSlotIndex;visualActionId=projectile.ActionId;
            FootprintGeometry? geometry = projectile.ImpactFootprint;
            bool shaped = geometry.HasValue && !geometry.Value.IsCircle;
            // Contains includes a small normalized boundary tolerance. Keep the broad phase
            // conservative under world-coordinate rounding, without changing circle queries.
            float queryRadius = shaped
                ? (float)Math.Min(float.MaxValue, (double)geometry.Value.BoundingRadius * 1.000001)
                : projectile.Radius;
            spatial.Query(projectile.To, queryRadius, splashTargets);
            if (projectile.ColorBudget != null) SortTargets(projectile.To, splashTargets);
            foreach (var victim in splashTargets)
            {
                if (shaped && !geometry.Value.Contains(projectile.To, victim.Position)) continue;
                ApplyDamage(victim, projectile.Damage, projectile.Definition, projectile.EffectPayload, projectile.ColorBudget, statistics);
            }
            events.Publish(tick, SimulationEventKind.ProjectileImpact, projectile.From, projectile.To,
                projectile.Weapon, projectile.Radius, projectile.Id, impactFootprint: projectile.ImpactFootprint,weaponId:projectile.WeaponId,
                sourceStructureId:projectile.SourceStructureId,mountSlotIndex:projectile.MountSlotIndex,actionId:projectile.ActionId,simulationSeconds:clockSeconds,attack:AttackKind.AreaProjectile);
        }

        private void ApplyDamage(EnemyState victim, float damage, WeaponDefinition weapon, ColorEffectPayload payload,
            TowerColorBudget colorBudget, RunStatistics statistics)
        {
            if (victim.Health <= 0 || damage < 0) return;
            // An intrinsic support effect is independent of direct damage (the old stapler also slowed at zero damage).
            if (damage == 0) { ApplyIntrinsicEffects(victim,weapon,statistics); return; }
            if (mechanics.EnemyDrawingTiers || mechanics.CorpseBlots || payload.Color != InkColor.None)
            {
                if (payload.Color != InkColor.None && colorBudget != null && !colorBudget.TryApply(victim.Id, clockSeconds)) payload = default;
                EnemyEffects.ApplyHit(victim, damage, payload, statistics, weapon.LegacyKind ?? (StructureKind)(-1), effects, mechanics.CorpseBlots ? balance.BlotReduction : 0,weapon.Id,
                    visualEvents,visualTick,clockSeconds,visualSourceStructureId,visualMountSlotIndex,visualActionId);
                ApplyIntrinsicEffects(victim,weapon,statistics); return;
            }
            float actualDamage = Math.Min(victim.Health, damage);
            victim.Health -= actualDamage;
            statistics.RecordDamage(weapon.Id, actualDamage);
            EnemyEffects.PublishDamage(victim,victim.Health+actualDamage,visualEvents,visualTick,clockSeconds,
                weapon.LegacyKind??(StructureKind)(-1),weapon.Id,visualSourceStructureId,visualMountSlotIndex,visualActionId);
            ApplyIntrinsicEffects(victim,weapon,statistics);
        }

        void ApplyIntrinsicEffects(EnemyState victim,WeaponDefinition weapon,RunStatistics statistics)
        {
            if (weapon.Effects==null) return;
            foreach(var effect in weapon.Effects)
            {
                if(effect.Kind==EffectKind.Slow) EnemyEffects.ApplySlow(victim,effect.Strength,effect.Duration);
                else EnemyEffects.ApplyHit(victim,0,ColorEffectPayload.Capture(effect),statistics,weapon.LegacyKind ?? (StructureKind)(-1),effects,mechanics.CorpseBlots?balance.BlotReduction:0,weapon.Id);
            }
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

        static void FilterRange(Point2 origin,float minimum,float maximum,List<EnemyState> candidates)
        {
            if(maximum<=0){candidates.Clear();return;}
            float minSquared=minimum*minimum,maxSquared=maximum*maximum;
            int write=0;
            for(int read=0;read<candidates.Count;read++)
            {
                var enemy=candidates[read];float distance=DistanceSquared(origin,enemy.Position);
                if(distance<minSquared||distance>maxSquared)continue;
                candidates[write++]=enemy;
            }
            if(write<candidates.Count)candidates.RemoveRange(write,candidates.Count-write);
        }

        private void FirePiercer(Point2 origin, Point2 target, float range, float radius, int maxPierce,
            float damage, WeaponDefinition weapon, ColorEffectPayload payload, TowerColorBudget colorBudget, RunStatistics statistics)
        {
            float dx = target.X - origin.X, dy = target.Y - origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0.00001f) return;
            dx /= length; dy /= length;
            splashTargets.Clear();
            foreach (var enemy in targets)
            {
                if (enemy.Health <= 0) continue;
                float ex = enemy.Position.X - origin.X, ey = enemy.Position.Y - origin.Y;
                float along = ex * dx + ey * dy;
                float across = Math.Abs(ex * dy - ey * dx);
                if (along >= 0 && along <= range && across <= radius) splashTargets.Add(enemy);
            }
            SortTargets(origin, splashTargets);
            for (int i = 0; i < splashTargets.Count && i < maxPierce; i++)
                ApplyDamage(splashTargets[i], damage, weapon, payload, colorBudget, statistics);
        }

        private void SortTargets(Point2 origin, List<EnemyState> list)
        { targetOrder.Origin = origin; list.Sort(targetOrder); }

        private sealed class EnemyDistanceComparer : IComparer<EnemyState>
        {
            internal Point2 Origin;
            public int Compare(EnemyState a, EnemyState b)
            {
                int result = DistanceSquared(Origin, a.Position).CompareTo(DistanceSquared(Origin, b.Position));
                return result != 0 ? result : a.Id.CompareTo(b.Id);
            }
        }

        private static float DistanceSquared(Point2 a, Point2 b)
        { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

        private static Point2 RayEnd(Point2 from, Point2 to, float range)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            return length <= 0.00001f ? to : new Point2(from.X + dx / length * range, from.Y + dy / length * range);
        }
    }
}
