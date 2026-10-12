using System;

namespace PaperDefense.Core
{
    /// <summary>Developer configuration. Resolve once when creating a run or a trial.</summary>
    [Serializable]
    public sealed class MechanicsConfiguration
    {
        public bool EnemyDrawingTiers = true;
        public bool CorpseBlots = true;
        public bool RangedEnemies = true;
        public bool InkWells = true;
        public bool ColorCartridges = true;
        public bool RunUpgrades = true;
        public bool DrawingQuality = true;

        public MechanicsConfiguration CreateSnapshot() => (MechanicsConfiguration)MemberwiseClone();
    }

    /// <summary>Read-only capabilities, including the legacy configuration gates.</summary>
    public readonly struct MechanicsAvailability
    {
        public bool EnemyDrawingTiers { get; }
        public bool CorpseBlots { get; }
        public bool RangedEnemies { get; }
        public bool InkWells { get; }
        public bool ColorCartridges { get; }
        public bool RunUpgrades { get; }
        public bool DrawingQuality { get; }

        MechanicsAvailability(GameBalance balance)
        {
            var configuration = balance.Mechanics;
            EnemyDrawingTiers = balance.EnableEnemyMechanics && configuration.EnemyDrawingTiers;
            CorpseBlots = balance.EnableEnemyMechanics && configuration.CorpseBlots;
            RangedEnemies = balance.EnableEnemyMechanics && configuration.RangedEnemies;
            InkWells = balance.EnableInkWells && configuration.InkWells;
            ColorCartridges = configuration.ColorCartridges;
            RunUpgrades = configuration.RunUpgrades;
            DrawingQuality = configuration.DrawingQuality;
        }

        public static MechanicsAvailability Resolve(GameBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (balance.Mechanics == null) throw new ArgumentException("Отсутствует конфигурация механик.", nameof(balance));
            return new MechanicsAvailability(balance);
        }
    }
}
