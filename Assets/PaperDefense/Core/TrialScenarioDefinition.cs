namespace PaperDefense.Core
{
    /// <summary>Conditions of the training sheet, deliberately separate from normal run tuning.</summary>
    internal readonly struct TrialScenarioDefinition
    {
        internal const float StepSeconds = 1f / 30;
        internal const int MaximumTicks = 1800, MaximumBudget = 20000, RecordingStrideTicks = 10, MaximumFrameShots = 1024;
        internal const float CounterfireRange = 12.5f, CounterfireDamage = 10, CounterfireInterval = 2.4f, ExitX = 48;
        internal readonly int Count;
        internal readonly float SpawnInterval, Speed, Health;
        internal readonly EnemyKind EnemyKind;
        internal readonly DrawingTier DrawingTier;

        TrialScenarioDefinition(int count, float interval, float speed, float health, EnemyKind enemy, DrawingTier drawing)
        { Count = count; SpawnInterval = interval; Speed = speed; Health = health; EnemyKind = enemy; DrawingTier = drawing; }

        internal static TrialScenarioDefinition For(TrialScenario scenario)
        {
            switch (scenario)
            {
                case TrialScenario.Crowd: return new TrialScenarioDefinition(240, .1f, 2.3f, 22, EnemyKind.Doodle, DrawingTier.Sketch);
                case TrialScenario.Fast: return new TrialScenarioDefinition(160, .15f, 5, 14, EnemyKind.Runner, DrawingTier.Sketch);
                case TrialScenario.Armored: return new TrialScenarioDefinition(80, .3f, 1.5f, 140, EnemyKind.Gnawer, DrawingTier.Hatched);
                // Empty enemy stream; the separate seeded blot context owns its finite marks.
                case TrialScenario.Blots: return new TrialScenarioDefinition(0, .3f, 2.3f, 22, EnemyKind.Doodle, DrawingTier.Sketch);
                default: throw new System.ArgumentOutOfRangeException(nameof(scenario));
            }
        }
    }
}
