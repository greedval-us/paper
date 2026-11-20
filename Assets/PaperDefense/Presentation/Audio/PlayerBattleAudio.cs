using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>One bounded audio owner for the player shell; input is the controller's live event stream.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerBattleAudio : MonoBehaviour
    {
        const int VoiceLimit = 6;
        readonly BattleAudioPolicy policy = new BattleAudioPolicy();
        readonly AudioSource[] voices = new AudioSource[VoiceLimit];
        readonly double[] endsAt = new double[VoiceLimit];
        readonly int[] priorities = new int[VoiceLimit];
        readonly float[] gains = new float[VoiceLimit];
        readonly AudioClip[] clips = new AudioClip[BattleAudioPolicy.MaximumPending];
        PlayerGameRoot owner;
        RunController run;
        PlayerAudioSettings settings;
        Camera worldCamera;
        bool subscribed, focused = true, applicationPaused, observedBattle, alarmObserved, terminalPlayback;
        Guid runId;
        long lastSequence;
        int wave;
        RunPhase phase;
        float initialCastleHealth;
        public int BoundedVoiceCount { get; private set; }
        public int QueuedCueCount => policy.PendingCount;
        public int PlayedCueCount { get; private set; }
        public BattleAudioCue? LastCue { get; private set; }
        public int ActiveVoiceCount
        {
            get { int count = 0; double now = Now; for (int i = 0; i < VoiceLimit; i++) if (endsAt[i] > now) count++; return count; }
        }
        static double Now => Time.realtimeSinceStartupAsDouble;
        float Volume => settings?.EffectiveVolume ?? 0;
        bool Audible => isActiveAndEnabled && focused && !applicationPaused && Volume > 0;
        bool BattleActive => owner != null && run != null && !run.InWorkshop && !run.Paused &&
            owner.Page == PlayerPage.Battle && !Terminal(run.Session.Phase);

        public void Initialize(PlayerGameRoot player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (owner == player && run == player.Run) { if (isActiveAndEnabled) Subscribe(); return; }
            // A second bootstrap/test attachment must not create a second mix or listener.
            foreach (var other in player.GetComponentsInChildren<PlayerBattleAudio>(true))
                if (other != this && other.owner == player && other.isActiveAndEnabled)
                { enabled = false; return; }
            Unsubscribe(); StopVoices();
            owner = player; run = player.Run; settings = player.AudioSettings; worldCamera = player.World.WorldCamera;
            focused = UnityEngine.Application.isFocused;
            EnsureVoices(); EnsureListener(); Bind(run.Session);
            if (isActiveAndEnabled) Subscribe();
        }

        void EnsureVoices()
        {
            if (BoundedVoiceCount != 0) return;
            for (int i = 0; i < VoiceLimit; i++)
            {
                var voice = new GameObject("Battle audio voice " + (i + 1)).AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0; voice.dopplerLevel = 0;
                voice.pitch = 1; voice.volume = 0; voices[i] = voice;
            }
            BoundedVoiceCount = VoiceLimit;
            for (int i = 0; i < clips.Length; i++) clips[i] = Resources.Load<AudioClip>("PaperDefenseAudio/" + (BattleAudioCue)i);
        }

        void EnsureListener()
        {
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) return;
            if (worldCamera == null) return;
            var cameraListener = worldCamera.GetComponent<AudioListener>();
            if (cameraListener == null) worldCamera.gameObject.AddComponent<AudioListener>();
            else cameraListener.enabled = true;
        }

        void Subscribe()
        {
            if (subscribed || run == null) return;
            run.SimulationEventReceived += Receive;
            run.SessionChanged += Bind;
            if (settings != null) settings.Changed += SettingsChanged;
            subscribed = true;
        }
        void Unsubscribe()
        {
            if (!subscribed) return;
            if (run != null) { run.SimulationEventReceived -= Receive; run.SessionChanged -= Bind; }
            if (settings != null) settings.Changed -= SettingsChanged;
            subscribed = false;
        }
        void OnEnable() { if (run != null) { Bind(run.Session); Subscribe(); } }
        void OnDisable() { Unsubscribe(); StopVoices(); }
        void OnDestroy()
        {
            Unsubscribe(); StopVoices();
            for (int i = 0; i < VoiceLimit; i++) if (voices[i] != null) Destroy(voices[i].gameObject);
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value) StopVoices(); }
        void OnApplicationPause(bool value) { applicationPaused = value; if (value) StopVoices(); }

        void Bind(IRunReadModel session)
        {
            StopVoices(); policy.Reset(); terminalPlayback = false; lastSequence = 0;
            runId = session.RunId; wave = session.WaveIndex; phase = session.Phase;
            initialCastleHealth = Math.Max(1, session.Balance.BaseHealth);
            alarmObserved = LowCastleHealth(session);
            observedBattle = owner != null && owner.Page == PlayerPage.Battle;
        }

        void SettingsChanged()
        {
            if (Volume <= 0) { StopVoices(); return; }
            for (int i = 0; i < VoiceLimit; i++) if (voices[i] != null) voices[i].volume = gains[i] * Volume;
        }

        void Receive(SimulationEvent item)
        {
            if (run == null || run.Session == null) return;
            if (run.Session.RunId != runId) Bind(run.Session);
            if (item.Sequence <= lastSequence) return;
            lastSequence = item.Sequence;
            observedBattle |= owner.Page == PlayerPage.Battle;
            if (!Audible || !BattleActive || !BattleAudioPolicy.TryClassify(item, out var cue)) return;
            float pan = 0;
            if (BattleAudioPolicy.Priority(cue) < 60 && worldCamera != null)
            {
                var point = cue == BattleAudioCue.MortarImpact ? item.To : item.From;
                var viewport = worldCamera.WorldToViewportPoint(new Vector3(point.X, point.Y, 0));
                pan = (viewport.x - .5f) * 1.3f;
            }
            policy.Queue(cue, Now, pan);
        }

        void LateUpdate()
        {
            if (run == null || owner == null || run.Session == null) return;
            var session = run.Session;
            if (session.RunId != runId) Bind(session);
            observedBattle |= owner.Page == PlayerPage.Battle;
            bool terminal = Terminal(session.Phase);
            bool ending = terminal && !Terminal(phase) && observedBattle;
            bool resultPage = owner.Page == PlayerPage.Result || owner.Page == PlayerPage.Battle;
            if (ending)
            {
                // Keep the short result sound clear of the last frame's impacts and weapon tails.
                StopVoices(); terminalPlayback = Audible && resultPage;
                if (terminalPlayback) policy.Queue(session.Phase == RunPhase.Defeat ? BattleAudioCue.Defeat : BattleAudioCue.Victory, Now);
            }
            else if (!Audible || (!BattleActive && !(terminalPlayback && terminal && resultPage))) StopVoices();

            if (!terminal && Audible && BattleActive && session.Phase == RunPhase.Assault && session.WaveIndex > wave)
                policy.Queue(BattleAudioCue.WaveStarted, Now);
            bool low = LowCastleHealth(session);
            if (low && !alarmObserved)
            {
                alarmObserved = true;
                if (Audible && BattleActive) policy.Queue(BattleAudioCue.CastleAlarm, Now);
            }
            // Observe even while silent: unmuting or returning from the desktop never replays a transition.
            wave = session.WaveIndex; phase = session.Phase;
            if (!Audible || (!BattleActive && !(terminalPlayback && terminal && resultPage))) return;
            double now = Now;
            policy.BeginFrame();
            while (policy.TryTake(now, out var request)) Play(request, now);
            policy.Clear(); // Requests that did not fit this frame are discarded, never delayed.
        }

        bool LowCastleHealth(IRunReadModel session) => session.BaseHealth > 0 && session.BaseHealth <= initialCastleHealth * .30f;
        static bool Terminal(RunPhase value) => value == RunPhase.Victory || value == RunPhase.Defeat;

        void Play(BattleAudioRequest request, double now)
        {
            var clip = clips[(int)request.Cue];
            if (clip == null) return;
            // Ordinary battle sounds may use five voices; the sixth is reserved for a warning.
            int limit = request.Priority >= 80 ? VoiceLimit : VoiceLimit - 1;
            int slot = -1;
            for (int i = 0; i < limit; i++) if (endsAt[i] <= now) { slot = i; break; }
            if (slot < 0)
                for (int i = 0; i < limit; i++)
                    if (priorities[i] < request.Priority && (slot < 0 || priorities[i] < priorities[slot] ||
                        priorities[i] == priorities[slot] && endsAt[i] < endsAt[slot])) slot = i;
            if (slot < 0) return;
            var voice = voices[slot]; voice.Stop(); voice.clip = clip;
            voice.panStereo = request.Priority >= 60 ? 0 : request.Pan;
            voice.priority = Math.Max(0, 200 - request.Priority); voice.pitch = 1;
            voice.volume = request.Gain * Volume; gains[slot] = request.Gain;
            priorities[slot] = request.Priority; endsAt[slot] = now + clip.length;
            voice.Play();
            if (PlayedCueCount < int.MaxValue) PlayedCueCount++;
            LastCue = request.Cue;
        }

        void StopVoices()
        {
            policy.Clear();
            for (int i = 0; i < VoiceLimit; i++)
            {
                if (voices[i] != null) voices[i].Stop();
                endsAt[i] = 0;
            }
        }

    }
}
