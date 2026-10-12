using System;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        public void ChooseUpgrade(RunUpgrade upgrade)
        {
            if (!Mechanics.RunUpgrades || !PendingUpgrade || !Enum.IsDefined(typeof(RunUpgrade), upgrade)) return;
            PendingUpgrade = false;
            Statistics.RecordUpgradeChoice(upgrade);
            switch (upgrade)
            {
                case RunUpgrade.RapidFire:
                    fireMultiplier *= Balance.Progression.RapidFireMultiplier;
                    break;
                case RunUpgrade.DeepInk:
                    damageMultiplier *= Balance.Progression.DeepInkDamageMultiplier;
                    healthMultiplier *= Balance.Progression.DeepInkHealthMultiplier;
                    foreach (var structure in structures)
                    {
                        structure.Health *= Balance.Progression.DeepInkHealthMultiplier;
                        structure.MaxHealth *= Balance.Progression.DeepInkHealthMultiplier;
                    }
                    flowDirty = true;
                    StructureVisualVersion++;
                    break;
                case RunUpgrade.Salvage:
                    salvageBonus += Balance.Progression.SalvageRefundBonus;
                    supplyMultiplier *= Balance.Progression.SalvageSupplyMultiplier;
                    waveRewardMultiplier *= Balance.Progression.SalvageWaveRewardMultiplier;
                    CreditInk(Balance.Progression.SalvageImmediateInk);
                    break;
            }
        }

        private void CompleteWave()
        {
            AwardWellResources();
            AwardGold();
            int configuredGrant = Balance.Waves[WaveIndex - 1].InkGrant;
            int grant = configuredGrant >= 0 ? configuredGrant : Balance.WaveInkGrant;
            CreditInk((int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, grant) * (double)waveRewardMultiplier)));
            if (WaveIndex >= Balance.Waves.Count) { Phase = RunPhase.Victory; Countdown = 0; return; }
            Phase = RunPhase.Intermission;
            Countdown = Math.Max(0, Balance.IntermissionSeconds);
            PendingUpgrade = Mechanics.RunUpgrades && Balance.Progression.UpgradeEveryWaves > 0 && WaveIndex <= Balance.Progression.LastUpgradeWave &&
                WaveIndex % Balance.Progression.UpgradeEveryWaves == 0;
        }

        // Profile rewards do not depend on wells, color income or run upgrade selection.
        private void AwardGold()
        {
            GoldEarned = (int)Math.Min(int.MaxValue, (long)GoldEarned + Math.Max(0, Balance.WaveGold));
            if (WaveIndex >= Balance.Waves.Count)
                GoldEarned = (int)Math.Min(int.MaxValue, (long)GoldEarned + Math.Max(0, Balance.VictoryGold));
        }
    }
}
