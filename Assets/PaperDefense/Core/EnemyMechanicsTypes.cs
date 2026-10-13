namespace PaperDefense.Core
{
    public sealed class BlotState
    {
        public Cell Cell { get; internal set; }
        public float Remaining { get; internal set; }
    }

    public sealed class EnemyGlob
    {
        public int Id { get; internal set; }
        public Point2 From { get; internal set; }
        public Point2 To { get; internal set; }
        public float Remaining { get; internal set; }
        public float Duration { get; internal set; }
        public float Damage { get; internal set; }
        public int ObjectiveIndex { get; internal set; }
        public int SourceEnemyId { get; internal set; }
        public EnemyKind SourceEnemyKind { get; internal set; }
        public DrawingTier SourceEnemyDrawingTier { get; internal set; }
        public bool TargetsStructure => ObjectiveIndex < -1;
    }
}
