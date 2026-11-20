using System;

namespace PaperDefense.Core
{
    /// <summary>Admits an enemy through an available entrance and initializes its wave-specific stats.</summary>
    internal sealed class EnemyWaveSpawner
    {
        readonly GameBalance rules;
        readonly MechanicsAvailability mechanics;
        readonly BattlefieldView map;
        readonly EnemyCrowdGrid occupancy;
        readonly EnemyCrowdMotion motion;
        readonly Random random;
        readonly Func<Cell, bool> isBlocked;
        readonly Func<Cell, int> chooseObjective;
        WaveDefinition wave;
        WaveComposition composition;
        int waveNumber;

        internal EnemyWaveSpawner(GameBalance rules, MechanicsAvailability mechanics, BattlefieldView map,
            EnemyCrowdGrid occupancy, EnemyCrowdMotion motion, Random random,
            Func<Cell, bool> isBlocked, Func<Cell, int> chooseObjective)
        {
            this.rules = rules;
            this.mechanics = mechanics;
            this.map = map;
            this.occupancy = occupancy;
            this.motion = motion;
            this.random = random;
            this.isBlocked = isBlocked;
            this.chooseObjective = chooseObjective;
        }

        internal void BeginWave(WaveDefinition wave, int number)
        {
            this.wave = wave;
            waveNumber = number;
            composition = wave.ResolveComposition(mechanics);
        }

        internal bool TrySpawn(int id, int ordinal, out EnemyState enemy)
        {
            var kind = composition.SelectKind(random.NextDouble());
            var definition = rules.Enemy(kind);
            float bodyScale = wave.IsHorde ? definition.VisualScale * wave.Horde.BodyScale : definition.VisualScale;
            if (!TrySelectEntrance(bodyScale, out var cell))
            {
                enemy = null;
                return false;
            }

            Point2 position = occupancy.Add(cell);
            if (motion != null) position = Center(cell);
            var tier = mechanics.EnemyDrawingTiers ? composition.SelectTier(random.NextDouble()) : DrawingTier.Sketch;
            float health = StartingHealth(definition, tier);
            enemy = new EnemyState {
                Id = id, Kind = kind, Position = position, VisualScale = bodyScale,
                Health = health, MaxHealth = health, DrawingTier = tier,
                ObjectiveDamageScale = wave.IsHorde ? wave.Horde.ObjectiveDamageScale : 1,
                SpeedScale = rules.Progression.EnemySpeedScaleMinimum +
                    (float)random.NextDouble() * rules.Progression.EnemySpeedScaleVariation,
                ObjectiveIndex = chooseObjective(cell),
                RewardBudget = wave.IsHorde ? wave.Horde.RewardForSpawn(ordinal, wave.Count) : Math.Max(0, definition.Reward)
            };
            return true;
        }

        float StartingHealth(EnemyDefinition definition, DrawingTier tier)
        {
            float scale = rules.EnableEnemyMechanics
                ? EnemyEffects.DrawingHealthMultiplier(tier, rules.Effects)
                : 1 + Math.Max(0, waveNumber - rules.Progression.LegacyHealthGrowthStartsAfterWave) *
                    rules.Progression.LegacyHealthGrowthPerWave;
            scale *= 1 + Math.Max(0, waveNumber - 1) * rules.WaveHealthGrowth;
            return Math.Max(1, definition.MaxHealth * scale * (wave.IsHorde ? wave.Horde.HealthScale : 1));
        }

        int ChooseSide()
        {
            int count = 0;
            for (int bit = 1; bit <= 8; bit <<= 1)
                if ((wave.Sides & bit) != 0) count++;
            int index = random.Next(Math.Max(1, count));
            for (int bit = 1; bit <= 8; bit <<= 1)
                if ((wave.Sides & bit) != 0 && index-- == 0) return bit;
            return 1;
        }

        bool TrySelectEntrance(float bodyScale, out Cell cell)
        {
            cell = default;
            int side = ChooseSide();
            if (motion != null)
            {
                bool admitted = TryCrowdEntrance(side, bodyScale, out cell);
                for (int alternate = 1; alternate <= 8 && !admitted; alternate <<= 1)
                    if (alternate != side && (wave.Sides & alternate) != 0)
                        admitted = TryCrowdEntrance(alternate, bodyScale, out cell);
                if (!admitted) return false;
            }
            else if (map != null && !TryMapEntrance(side, out cell)) return false;

            int extent = side <= 2 ? rules.Height : rules.Width;
            // Authored entrances also consumed this roll historically. Keep subsequent rolls in sync.
            int offset = random.Next(2, extent - 2);
            if (motion != null || map != null) return true;
            int count = extent - 4;
            for (int i = 0; i < count; i++)
            {
                cell = EdgeCell(side, 2 + (offset - 2 + i) % count);
                if (occupancy.CanEnter(cell)) return true;
            }
            return false;
        }

        bool TryMapEntrance(int side, out Cell cell)
        {
            int start = random.Next(map.Entrances.Count);
            for (int i = 0; i < map.Entrances.Count; i++)
            {
                int gate = (start + i) % map.Entrances.Count;
                cell = map.Entrances[gate];
                if (map.IsWaveEntrance(gate, side, wave.EntranceBands) && occupancy.CanEnter(cell)) return true;
            }
            cell = default;
            return false;
        }

        bool TryCrowdEntrance(int side, float bodyScale, out Cell cell)
        {
            int entrances = map == null ? 1 : map.Entrances.Count;
            int start = random.Next(entrances);
            int extent = side <= 2 ? rules.Height : rules.Width;
            for (int i = 0; i < entrances; i++)
            {
                int gate = (start + i) % entrances;
                if (map != null && !map.IsWaveEntrance(gate, side, wave.EntranceBands)) continue;
                var entrance = map == null ? default : map.Entrances[gate];
                int center = side <= 2 ? entrance.Y : entrance.X;
                int half = map != null && !map.IsOpenTerrain ? 0 : rules.Crowd.EntranceHalfWidth;
                int low = map == null ? 1 : Math.Max(1, center - half);
                int high = map == null ? extent - 2 : Math.Min(extent - 2, center + half);
                int count = high - low + 1;
                if (count <= 0) continue;
                int offset = random.Next(count);
                for (int j = 0; j < count; j++)
                {
                    cell = EdgeCell(side, low + (offset + j) % count);
                    if (occupancy.CanEnter(cell) && (map == null || map.IsWalkable(cell)) &&
                        !isBlocked(cell) && motion.CanSpawn(Center(cell), bodyScale)) return true;
                }
            }
            cell = default;
            return false;
        }

        Cell EdgeCell(int side, int offset)
        {
            switch (side)
            {
                case 1: return new Cell(0, offset);
                case 2: return new Cell(rules.Width - 1, offset);
                case 4: return new Cell(offset, rules.Height - 1);
                default: return new Cell(offset, 0);
            }
        }

        static Point2 Center(Cell cell) => new Point2(cell.X + .5f, cell.Y + .5f);
    }
}
