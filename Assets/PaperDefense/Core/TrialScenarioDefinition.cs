using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable]
    public sealed class TrialScenarioDefinition
    {
        public TrialScenario Scenario;
        public string Name;
        public int Count;
        public float SpawnInterval, Speed, Health;
        public EnemyKind EnemyKind;
        public DrawingTier DrawingTier;
        public TrialScenarioDefinition CreateSnapshot() => (TrialScenarioDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class TrialRules
    {
        // Playback/storage bounds are technical contracts rather than battle tuning.
        public const int MaximumBudget = 20000, MaximumSimulationTicks = 180000, MaximumScenarioEnemies = 20000;
        public const int RecordingStrideTicks = 10, MaximumFrameShots = 1024;
        public float StepSeconds = 1f / 30;
        public int MaximumTicks = 1800, DefaultBudget = 400;
        public List<int> BudgetChoices = new List<int> { 200, 400, 800 };
        public string BaselineChassisId = "chassis.standard", BaselineWeaponId = "weapon.gun";
        public float CounterfireRange = 12.5f, CounterfireDamage = 10, CounterfireInterval = 2.4f, ExitX = 48;
        public float SpawnX = 10, SpawnY = 10.5f, SpawnYVariation = 8;
        public int CloseTowerCount = 48, CloseTowerX = 32, CloseTowerY = 10, CloseTowerColumns = 4, CloseTowerSpacing = 2;
        public int OverflowTowerX = 1, OverflowTowerY = 1, OverflowTowerColumns = 60;
        public int BlotCount = 48, FirstBlotX = 33, FirstBlotY = 10, BlotMinX = 29, BlotMaxX = 43, BlotMinY = 8, BlotMaxY = 28;
        public float BlotLifetime = 60;
        public List<TrialScenarioDefinition> Scenarios = new List<TrialScenarioDefinition>
        {
            new TrialScenarioDefinition { Scenario = TrialScenario.Crowd, Name = "Толпа", Count = 240, SpawnInterval = .1f, Speed = 2.3f, Health = 22, EnemyKind = EnemyKind.Doodle, DrawingTier = DrawingTier.Sketch },
            new TrialScenarioDefinition { Scenario = TrialScenario.Fast, Name = "Быстрые", Count = 160, SpawnInterval = .15f, Speed = 5, Health = 14, EnemyKind = EnemyKind.Runner, DrawingTier = DrawingTier.Sketch },
            new TrialScenarioDefinition { Scenario = TrialScenario.Armored, Name = "Защищённые", Count = 80, SpawnInterval = .3f, Speed = 1.5f, Health = 140, EnemyKind = EnemyKind.Gnawer, DrawingTier = DrawingTier.Hatched },
            new TrialScenarioDefinition { Scenario = TrialScenario.Blots, Name = "Кляксы", Count = 0, SpawnInterval = .3f, Speed = 2.3f, Health = 22, EnemyKind = EnemyKind.Doodle, DrawingTier = DrawingTier.Sketch }
        };
        public TrialScenarioDefinition For(TrialScenario scenario)
        { foreach (var item in Scenarios) if (item.Scenario == scenario) return item; throw new ArgumentException("trial.scenario: не зарегистрировано испытание " + scenario); }
        public TrialRules CreateSnapshot()
        {
            ContentValidation.Id(BaselineChassisId, "trial.baselineChassisId"); ContentValidation.Id(BaselineWeaponId, "trial.baselineWeaponId");
            CombatTuningRules.Positive(StepSeconds, "trial.stepSeconds"); CombatTuningRules.Positive(CounterfireRange, "trial.counterfireRange");
            CombatTuningRules.Positive(CounterfireInterval, "trial.counterfireInterval"); CombatTuningRules.Positive(BlotLifetime, "trial.blotLifetime");
            if (MaximumTicks <= 0 || MaximumTicks > MaximumSimulationTicks || DefaultBudget < 0 || DefaultBudget > MaximumBudget || CounterfireDamage < 0 || float.IsNaN(CounterfireDamage) || float.IsInfinity(CounterfireDamage) ||
                CloseTowerCount < 0 || CloseTowerColumns <= 0 || CloseTowerSpacing <= 0 || OverflowTowerColumns <= 0 || BlotCount < 0 || BlotMinX >= BlotMaxX || BlotMinY >= BlotMaxY ||
                BlotCount > (BlotMaxX - BlotMinX) * (BlotMaxY - BlotMinY) || FirstBlotX < BlotMinX || FirstBlotX >= BlotMaxX || FirstBlotY < BlotMinY || FirstBlotY >= BlotMaxY ||
                SpawnYVariation < 0 || float.IsNaN(SpawnYVariation) || float.IsInfinity(SpawnYVariation)) throw new ArgumentException("trial: неверные границы испытания.");
            foreach (float value in new[] { SpawnX, SpawnY, ExitX }) if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("trial.position: требуется конечное число.");
            if (Scenarios == null || Scenarios.Count != Enum.GetValues(typeof(TrialScenario)).Length) throw new ArgumentException("trial.scenarios: требуется каждое поддерживаемое испытание.");
            var seen = new HashSet<TrialScenario>();
            foreach (var item in Scenarios)
            {
                if (item == null || !seen.Add(item.Scenario) || !Enum.IsDefined(typeof(TrialScenario), item.Scenario) || item.Count < 0 || item.Count > MaximumScenarioEnemies || !Enum.IsDefined(typeof(EnemyKind), item.EnemyKind) || !Enum.IsDefined(typeof(DrawingTier), item.DrawingTier)) throw new ArgumentException("trial.scenarios: неверное или повторное определение.");
                CombatTuningRules.Positive(item.SpawnInterval, item.Scenario + ".spawnInterval"); CombatTuningRules.Positive(item.Speed, item.Scenario + ".speed"); CombatTuningRules.Positive(item.Health, item.Scenario + ".health");
            }
            if (BudgetChoices == null || BudgetChoices.Count == 0 || BudgetChoices.Exists(value => value < 0 || value > MaximumBudget) || new HashSet<int>(BudgetChoices).Count != BudgetChoices.Count || !BudgetChoices.Contains(DefaultBudget))
                throw new ArgumentException("trial.budgetChoices: уникальные бюджеты должны включать бюджет по умолчанию.");
            var copy = (TrialRules)MemberwiseClone(); copy.Scenarios = Scenarios.ConvertAll(item => item.CreateSnapshot()); copy.BudgetChoices = new List<int>(BudgetChoices); return copy;
        }
    }
}
