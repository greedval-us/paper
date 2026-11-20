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
        public bool WaveInkFlasks;
        public int InkPerFlask = 12;
        public bool ColorCartridges = true;
        public bool RunUpgrades = true;
        public bool FieldModernization;
        public int MaximumModernizationRank = 1;

        public MechanicsConfiguration CreateSnapshot() => (MechanicsConfiguration)MemberwiseClone();
    }

    /// <summary>Read-only capabilities, including the legacy configuration gates.</summary>
    public readonly struct MechanicsAvailability
    {
        public bool EnemyDrawingTiers { get; }
        public bool CorpseBlots { get; }
        public bool RangedEnemies { get; }
        public bool InkWells { get; }
        public bool WaveInkFlasks { get; }
        public int InkPerFlask { get; }
        public bool ColorCartridges { get; }
        public bool RunUpgrades { get; }
        public bool FieldModernization { get; }
        public int MaximumModernizationRank { get; }

        MechanicsAvailability(GameBalance balance)
        {
            var configuration = balance.Mechanics;
            EnemyDrawingTiers = balance.EnableEnemyMechanics && configuration.EnemyDrawingTiers;
            CorpseBlots = balance.EnableEnemyMechanics && configuration.CorpseBlots;
            RangedEnemies = balance.EnableEnemyMechanics && configuration.RangedEnemies;
            ColorCartridges = configuration.ColorCartridges;
            WaveInkFlasks = configuration.WaveInkFlasks && ColorCartridges;
            InkPerFlask = configuration.InkPerFlask;
            if (WaveInkFlasks && InkPerFlask <= 0) throw new ArgumentOutOfRangeException(nameof(configuration.InkPerFlask));
            InkWells = balance.EnableInkWells && configuration.InkWells && !configuration.WaveInkFlasks;
            FieldModernization = configuration.FieldModernization;
            MaximumModernizationRank = configuration.MaximumModernizationRank;
            if (MaximumModernizationRank < 1 || MaximumModernizationRank > 3) throw new ArgumentOutOfRangeException(nameof(configuration.MaximumModernizationRank));
            RunUpgrades = configuration.RunUpgrades && !FieldModernization;
        }

        public static MechanicsAvailability Resolve(GameBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (balance.Mechanics == null) throw new ArgumentException(UserText.Message("copy.d5a038c4bfd1895a"), nameof(balance));
            return new MechanicsAvailability(balance);
        }
    }
}
