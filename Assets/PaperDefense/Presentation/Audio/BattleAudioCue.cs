namespace PaperDefense.Presentation
{
    public enum BattleAudioCue
    {
        ShotSingle, ShotLine, ShotMortar, ShotCone, ShotRicochet, ShotRadial, ShotStapler,
        MortarImpact, BaseHit, StructureDestroyed, WaveStarted, Victory, Defeat,
        StructureCompleted, ModernizationCompleted, CastleAlarm
    }

    public readonly struct BattleAudioRequest
    {
        public BattleAudioCue Cue { get; }
        public float Pan { get; }
        public int Priority => BattleAudioPolicy.Priority(Cue);
        public float Gain => BattleAudioPolicy.Gain(Cue);
        internal BattleAudioRequest(BattleAudioCue cue, float pan) { Cue = cue; Pan = pan; }
    }
}
