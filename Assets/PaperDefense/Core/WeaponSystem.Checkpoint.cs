using System;

namespace PaperDefense.Core
{
    internal sealed partial class WeaponSystem
    {
        internal long CheckpointActionId => attacks.CheckpointActionId;
        internal void RestoreCheckpointClock(double elapsed, long actionId)
        { clockSeconds = elapsed; attacks.RestoreCheckpointActionId(actionId); }
    }
    internal sealed partial class WeaponAttackResolver
    {
        internal long CheckpointActionId => nextActionId;
        internal void RestoreCheckpointActionId(long value)
        {
            if (value < 0 || value == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            nextActionId = value;
        }
    }
}
