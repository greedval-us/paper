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
                    fireMultiplier *= rules.Progression.RapidFireMultiplier;
                    break;
                case RunUpgrade.DeepInk:
                    damageMultiplier *= rules.Progression.DeepInkDamageMultiplier;
                    healthMultiplier *= rules.Progression.DeepInkHealthMultiplier;
                    foreach (var structure in structures)
                    {
                        structure.Health *= rules.Progression.DeepInkHealthMultiplier;
                        structure.MaxHealth *= rules.Progression.DeepInkHealthMultiplier;
                    }
                    flowDirty = true;
                    StructureVisualVersion++;
                    break;
                case RunUpgrade.Salvage:
                    salvageBonus += rules.Progression.SalvageRefundBonus;
                    supplyMultiplier *= rules.Progression.SalvageSupplyMultiplier;
                    waveRewardMultiplier *= rules.Progression.SalvageWaveRewardMultiplier;
                    CreditInk(rules.Progression.SalvageImmediateInk);
                    break;
            }
        }

        private void CompleteWave()
        {
            CreditInk(generatorLedger.CompleteWave(RunId, WaveIndex, structures));
            CompletedWaves = WaveIndex;
            AwardWellResources();
            AwardGold();
            int configuredGrant = rules.Waves[WaveIndex - 1].InkGrant;
            int grant = configuredGrant >= 0 ? configuredGrant : rules.WaveInkGrant;
            CreditInk((int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, grant) * (double)waveRewardMultiplier)));
            if (WaveIndex >= rules.Waves.Count) { Phase = RunPhase.Victory; Countdown = 0; return; }
            Phase = RunPhase.Intermission;
            Countdown = Math.Max(0, rules.IntermissionSeconds);
            PendingUpgrade = Mechanics.RunUpgrades && rules.Progression.UpgradeEveryWaves > 0 && WaveIndex <= rules.Progression.LastUpgradeWave &&
                WaveIndex % rules.Progression.UpgradeEveryWaves == 0;
        }

        // Profile rewards do not depend on wells, color income or run upgrade selection.
        private void AwardGold()
        {
            GoldEarned = (int)Math.Min(int.MaxValue, (long)GoldEarned + Math.Max(0, rules.WaveGold));
            if (WaveIndex >= rules.Waves.Count)
                GoldEarned = (int)Math.Min(int.MaxValue, (long)GoldEarned + Math.Max(0, rules.VictoryGold));
        }
    }
}
