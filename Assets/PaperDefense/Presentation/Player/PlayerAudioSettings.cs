using System;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public interface IPlayerAudioPreferenceStore
    {
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>Local sound preferences; never part of the campaign profile or simulation.</summary>
    public sealed class PlayerAudioSettings
    {
        public const float DefaultSfxVolume = .65f;
        const string VolumeKey = "PaperDefense.Audio.SfxVolume";
        const string MuteKey = "PaperDefense.Audio.Muted";
        readonly IPlayerAudioPreferenceStore store;

        public float SfxVolume { get; private set; } = DefaultSfxVolume;
        public bool Muted { get; private set; }
        public float EffectiveVolume => Muted ? 0 : SfxVolume;
        public event Action Changed;

        public PlayerAudioSettings(IPlayerAudioPreferenceStore store = null, bool memoryOnly = false)
        {
            this.store = memoryOnly ? null : store;
            if (this.store == null) return;
            float volume = this.store.GetFloat(VolumeKey, DefaultSfxVolume);
            SfxVolume = IsFinite(volume) ? Mathf.Clamp01(volume) : DefaultSfxVolume;
            Muted = this.store.GetInt(MuteKey, 0) == 1;
        }

        public static PlayerAudioSettings Load(bool memoryOnly)
            => new PlayerAudioSettings(memoryOnly ? null : new UnityPlayerAudioPreferenceStore());

        public void SetVolume(float volume)
        {
            if (!IsFinite(volume)) return;
            volume = Mathf.Clamp01(volume);
            if (SfxVolume == volume) return;
            SfxVolume = volume;
            store?.SetFloat(VolumeKey, volume);
            store?.Save();
            Changed?.Invoke();
        }

        public void SetMuted(bool muted)
        {
            if (Muted == muted) return;
            Muted = muted;
            store?.SetInt(MuteKey, muted ? 1 : 0);
            store?.Save();
            Changed?.Invoke();
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        sealed class UnityPlayerAudioPreferenceStore : IPlayerAudioPreferenceStore
        {
            public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            public void Save() => PlayerPrefs.Save();
        }
    }
}
