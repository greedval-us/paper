using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Immutable initial conditions, separate from commands made after a run starts.</summary>
    public readonly struct PreparedEnemy
    {
        public Point2 Position { get; }
        public float Health { get; }
        public EnemyKind Kind { get; }
        public DrawingTier DrawingTier { get; }
        public bool Stationary { get; }
        public PreparedEnemy(Point2 position, float health, EnemyKind kind = EnemyKind.Doodle,
            DrawingTier drawingTier = DrawingTier.Sketch, bool stationary = true)
        { Position = position; Health = health; Kind = kind; DrawingTier = drawingTier; Stationary = stationary; }
    }

    public readonly struct PreparedBlot
    {
        public Cell Cell { get; }
        public float Lifetime { get; }
        public PreparedBlot(Cell cell, float lifetime) { Cell = cell; Lifetime = lifetime; }
    }

    public sealed partial class GameSession
    {
        /// <summary>Creates owned, bounded fixtures. Subsequent combat uses the ordinary session rules.</summary>
        public static GameSession CreatePrepared(GameBalance balance, int seed,
            IReadOnlyList<PreparedEnemy> targets, IReadOnlyList<PreparedBlot> blots = null,
            int redInk = 0, int blueInk = 0, int greenInk = 0)
        {
            BalanceValidation.Validate(balance);
            if (balance.Waves.Count == 0) throw new ArgumentException("preparation: an initial wave definition is required.");
            int count = targets?.Count ?? 0, blotCount = blots?.Count ?? 0;
            if (count > 20000 || blotCount > balance.Width * balance.Height)
                throw new ArgumentException("preparation: fixture capacity exceeded.");
            foreach (int amount in new[] { redInk, blueInk, greenInk })
                if (amount < 0 || amount > TrialRules.MaximumBudget)
                    throw new ArgumentOutOfRangeException(nameof(redInk), "preparation: invalid finite color stock.");
            var session = new GameSession(balance, seed, false);
            if ((redInk > 0 || blueInk > 0 || greenInk > 0) && !session.Mechanics.ColorCartridges)
                throw new ArgumentException("preparation: color stock requires color cartridges.");
            if (blotCount > 0 && !session.Mechanics.CorpseBlots)
                throw new ArgumentException("preparation: blot fixtures require corpse blots.");
            var admitted = new Dictionary<Cell, int>();
            for (int i = 0; i < count; i++)
            {
                var target = targets[i];
                if (!Finite(target.Position.X) || !Finite(target.Position.Y) || !Finite(target.Health) ||
                    target.Health <= 0 || target.Health > 1000000 || !Enum.IsDefined(typeof(EnemyKind), target.Kind) ||
                    !Enum.IsDefined(typeof(DrawingTier), target.DrawingTier))
                    throw new ArgumentException("preparation.targets[" + i + "]: invalid target.");
                var cell = CellOf(target.Position);
                if (!session.Inside(cell)) throw new ArgumentException("preparation: target outside map.");
                admitted.TryGetValue(cell, out int occupancy);
                if (occupancy >= balance.MaxEnemiesPerCell) throw new ArgumentException("preparation: target cell full.");
                admitted[cell] = occupancy + 1;
                if (target.Kind == EnemyKind.Spitter && !session.Mechanics.RangedEnemies)
                    throw new ArgumentException("preparation: spitter requires ranged enemies.");
                if (target.DrawingTier != DrawingTier.Sketch && !session.Mechanics.EnemyDrawingTiers)
                    throw new ArgumentException("preparation: drawing tiers disabled.");
                var definition = session.rules.Enemy(target.Kind);
                session.enemies.Add(new EnemyState {
                    Id = session.nextEnemyId++, Position = target.Position, Health = target.Health, MaxHealth = target.Health,
                    Kind = target.Kind, DrawingTier = target.DrawingTier, SpeedScale = target.Stationary ? 0 : 1,
                    RewardBudget = Math.Max(0, definition.Reward), ObjectiveIndex = -1
                });
            }
            for (int i = 0; i < blotCount; i++)
            {
                var blot = blots[i];
                if (!session.Inside(blot.Cell) || !Finite(blot.Lifetime) || blot.Lifetime <= 0 || blot.Lifetime > 3600)
                    throw new ArgumentException("preparation.blots[" + i + "]: invalid blot.");
                session.GetCorpseBlots().Add(blot.Cell, blot.Lifetime);
            }
            session.coloredInk[(int)InkColor.Red] = redInk;
            session.coloredInk[(int)InkColor.Blue] = blueInk;
            session.coloredInk[(int)InkColor.Green] = greenInk;
            session.Statistics.ObserveEnemies(count);
            session.crowd.Rebuild(session.enemies);
            // The fixture occupies the first real wave; no random spawn is reserved.
            session.WaveIndex = 1; session.RemainingToSpawn = 0; session.Countdown = 0; session.Phase = RunPhase.Assault;
            return session;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
