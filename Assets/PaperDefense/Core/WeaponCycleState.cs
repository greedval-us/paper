namespace PaperDefense.Core
{
    /// <summary>Owned progress through one mount's interval or reload, independent of its current feed.</summary>
    public sealed class WeaponCycleState
    {
        public bool Initialized { get; }
        public int RoundsRemaining { get; }
        public bool IsReloading { get; }
        public double RemainingWork { get; }

        public WeaponCycleState() { }

        internal WeaponCycleState(int roundsRemaining, bool isReloading, double remainingWork)
        {
            Initialized = true;
            RoundsRemaining = roundsRemaining;
            IsReloading = isReloading;
            RemainingWork = remainingWork;
        }
    }
}
