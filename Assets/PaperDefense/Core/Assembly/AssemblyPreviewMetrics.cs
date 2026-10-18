namespace PaperDefense.Core
{
    /// <summary>Read-only workshop figures. This type cannot be supplied to construction or combat.</summary>
    public sealed class AssemblyPreviewMetrics
    {
        public int Cost { get; }
        public int Load { get; }
        public int Capacity { get; }
        public float Health { get; }
        public float BuildSeconds { get; }
        public bool Overloaded => Load > Capacity;

        internal AssemblyPreviewMetrics(ResolvedEntry calculated)
        {
            Cost = calculated.Cost; Load = calculated.Load; Capacity = calculated.Capacity;
            Health = calculated.Health; BuildSeconds = calculated.BuildSeconds;
        }
    }
}
