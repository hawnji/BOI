using System.Collections.Generic;
using Hollowcrawl.Audio;
using Hollowcrawl.Bootstrap;
using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Plays sound effects, interface sounds and music from Resources/Audio/&lt;key&gt;. Missing clips are silent,
    /// so the game runs before any audio exists. Volumes come from the settings (music, effects, interface;
    /// master is applied to the AudioListener by SettingsApplier). Track choice lives in Core's MusicDirector.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        private const int SfxVoices = 16;
        private readonly AudioSource[] _sfx = new AudioSource[SfxVoices];
        private int _nextVoice;
        private AudioSource _musicA, _musicB;
        private bool _aIsCurrent = true;
        private float _fade = 1f;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _missing = new HashSet<string>();
        private readonly SfxLimiter _limiter = new SfxLimiter();
        public readonly MusicDirector Music = new MusicDirector();

        public static AudioService Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioService>();
            Instance.Build();
            return Instance;
        }

        private void Build()
        {
            for (int i = 0; i < SfxVoices; i++) _sfx[i] = NewSource(false);
            _musicA = NewSource(true);
            _musicB = NewSource(true);
        }

        private AudioSource NewSource(bool loop)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            return src;
        }

        private AudioClip Clip(string key)
        {
            if (string.IsNullOrEmpty(key) || _missing.Contains(key)) return null;
            if (_clips.TryGetValue(key, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + key);
            if (c == null) { _missing.Add(key); return null; }
            _clips[key] = c;
            return c;
        }

        public void PlaySfx(string key, float volume = 1f, float pitchJitter = 0.05f)
        {
            var clip = Clip(key);
            if (clip == null || !_limiter.TryPlay(key, Time.unscaledTime)) return;
            var src = _sfx[_nextVoice];
            _nextVoice = (_nextVoice + 1) % SfxVoices;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume * (GameServices.Settings?.SfxVolume ?? 1f));
        }

        public void PlayUi(string key)
        {
            var clip = Clip(key);
            if (clip == null) return;
            var src = _sfx[_nextVoice];
            _nextVoice = (_nextVoice + 1) % SfxVoices;
            src.pitch = 1f;
            src.PlayOneShot(clip, GameServices.Settings?.UiVolume ?? 1f);
        }

        public void PlayEvent(in SimEvent e)
        {
            string key = e.AudioKey ?? DefaultKey(e.Kind);
            if (key != null) PlaySfx(key, e.Kind == SimEventKind.ShotFired ? 0.5f : 1f);
        }

        public void UpdateMusic(in MusicContext context)
        {
            if (Music.Update(context))
            {
                _aIsCurrent = !_aIsCurrent;
                var next = _aIsCurrent ? _musicA : _musicB;
                next.clip = Clip(Music.Current);
                if (next.clip != null) next.Play(); else next.Stop();
                _fade = 0f;
            }
        }

        private void Update()
        {
            float musicVol = (GameServices.Settings?.MusicVolume ?? 0.7f) * Music.Duck;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, Music.CrossfadeSeconds));
            var cur = _aIsCurrent ? _musicA : _musicB;
            var old = _aIsCurrent ? _musicB : _musicA;
            cur.volume = musicVol * _fade;
            old.volume = musicVol * (1f - _fade);
            if (_fade >= 1f && old.isPlaying) old.Stop();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (GameServices.Settings != null && GameServices.Settings.MuteWhenUnfocused) AudioListener.pause = !focused;
        }

        public static string DefaultKey(SimEventKind k)
        {
            switch (k)
            {
                case SimEventKind.ShotFired: return "sfx_shot";
                case SimEventKind.EnemyHit: return "sfx_hit";
                case SimEventKind.EnemyKilled: return "sfx_enemy_death";
                case SimEventKind.PlayerHurt: return "sfx_player_hurt";
                case SimEventKind.Explosion: return "sfx_explosion";
                case SimEventKind.PickupCollected: return "sfx_pickup";
                case SimEventKind.ItemCollected: return "sfx_item";
                case SimEventKind.DoorChanged: return "sfx_door";
                case SimEventKind.SecretRevealed: return "sfx_secret";
                case SimEventKind.RoomCleared: return "sfx_room_clear";
                case SimEventKind.BossPhaseStarted: return "sfx_boss_phase";
                case SimEventKind.ActiveUsed: return "sfx_active";
                case SimEventKind.ConsumableUsed: return "sfx_consumable";
                case SimEventKind.ShrineUsed: return "sfx_shrine";
                case SimEventKind.HexApplied: return "sfx_hex";
                case SimEventKind.RoomModifierApplied: return "sfx_room_modifier";
                case SimEventKind.PurchaseFailed: return "sfx_purchase_fail";
                default: return null;
            }
        }
    }
}
