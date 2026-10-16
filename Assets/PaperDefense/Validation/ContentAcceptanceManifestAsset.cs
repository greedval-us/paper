using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Validation/Content Acceptance Manifest", fileName = "ContentAcceptanceManifest")]
    public sealed class ContentAcceptanceManifestAsset : ScriptableObject
    {
        public BalancePresetAsset preset;
        public string newWeaponId, variantWeaponId, newModuleId, newChassisId, existingWeaponId, changedEffectId;
        public string baselineChassisId = "chassis.standard";
        [Header("Expected values from the acceptance data diff")]
        public int expectedWeaponCost, expectedWeaponLoad, expectedWeaponUnlockGold, expectedAssemblyCost;
        public float expectedWeaponRange, expectedWeaponInterval;
        public int expectedModuleUnlockGold, expectedModuleAssemblyCost;
        public float expectedModuleRange, expectedModuleInterval;
        public int expectedChassisCapacity, expectedChassisBaseCost, expectedChassisAssemblyCost;
        public float expectedChassisHealth;
        public int expectedExistingWeaponCost;
        public float expectedExistingWeaponDamage, expectedEffectDuration;
        public int expectedWaveInkGrant;
        [Header("Finite verification conditions; these do not edit the preset")]
        public int startingGold, trialBudget, seed, probeCellX, probeCellY, probeMaximumTicks, battleMaximumTicks;
        public TrialScenario trialScenario;
        public InkColor effectColor = InkColor.Blue;
        public float probeStepSeconds, probeTargetHealth, variantAspectRatio = 1;
        public Vector2 probeTargetOffset;
        public Vector2[] variantTargets;
        public bool[] variantExpectedHits;
        public Vector2Int battleCellOffset;
    }
}
