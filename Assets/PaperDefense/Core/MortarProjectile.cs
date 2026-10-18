namespace PaperDefense.Core
{
    /// <summary>
    /// A shell owns its launch parameters until impact, independently of its source tower.
    /// Its destination is fixed at launch; collision uses enemy positions when flight ends.
    /// </summary>
    public sealed class MortarProjectile
    {
        public int Id { get; internal set; }
        public Point2 From { get; internal set; }
        public Point2 To { get; internal set; }
        public float Remaining { get; internal set; }
        public float Duration { get; internal set; }
        public float Radius { get; internal set; }
        public FootprintGeometry? ImpactFootprint { get; internal set; }
        public float Damage { get; internal set; }
        public StructureKind Weapon { get; internal set; }
        public string WeaponId { get; internal set; }
        public int SourceStructureId { get; internal set; }
        public int MountSlotIndex { get; internal set; } = -1;
        public long ActionId { get; internal set; }
        internal WeaponDefinition Definition;
        internal bool CompositeCycle;
        public InkColor Color { get; internal set; }
        internal ColorEffectPayload EffectPayload;
        internal TowerColorBudget ColorBudget;
    }
}
