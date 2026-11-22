using System;

namespace Hollowcrawl.Audio
{
    /// <summary>What the game is doing right now, as far as music cares.</summary>
    public struct MusicContext
    {
        public bool InMenu;
        public bool InRun;
        public bool Paused;
        public string FloorMusicKey;
        /// <summary>Music key of a boss that is alive in the current room (null when none).</summary>
        public string BossMusicKey;
        public bool RunWon;
        public bool RunLost;
        public string EndingMusicKey;
    }

    /// <summary>
    /// Pure music state machine: picks the track for the current context and says when to crossfade.
    /// The Unity AudioService plays whatever <see cref="Current"/> names (a missing clip is simply silent).
    /// </summary>
    public sealed class MusicDirector
    {
        public const string MenuTrack = "music_menu";
        public const string DefeatTrack = "music_defeat";
        public const string DefaultVictoryTrack = "music_victory";
        public const string DefaultFloorTrack = "music_floor";

        public string Current { get; private set; }
        /// <summary>Volume multiplier for the music bus (ducked while paused).</summary>
        public float Duck { get; private set; } = 1f;
        public float CrossfadeSeconds = 1.2f;

        /// <summary>Returns true when the track changed this call (start a crossfade to <see cref="Current"/>).</summary>
        public bool Update(in MusicContext c)
        {
            string want;
            if (c.InMenu || !c.InRun) want = MenuTrack;
            else if (c.RunWon) want = c.EndingMusicKey ?? DefaultVictoryTrack;
            else if (c.RunLost) want = DefeatTrack;
            else if (c.BossMusicKey != null) want = c.BossMusicKey;
            else want = string.IsNullOrEmpty(c.FloorMusicKey) ? DefaultFloorTrack : c.FloorMusicKey;

            Duck = c.Paused ? 0.4f : 1f;
            if (string.Equals(want, Current, StringComparison.Ordinal)) return false;
            Current = want;
            return true;
        }
    }

    /// <summary>
    /// Keeps busy rooms from turning into noise: each sound key has a minimum gap between plays and the
    /// whole mix has a cap on new voices per frame. Pure, unit-tested.
    /// </summary>
    public sealed class SfxLimiter
    {
        public float MinInterval = 0.045f;
        public int MaxNewVoicesPerFrame = 6;
        private readonly System.Collections.Generic.Dictionary<string, float> _lastPlayed = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal);
        private int _frameVoices;
        private float _frameTime = -1f;

        public bool TryPlay(string key, float now)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (now != _frameTime) { _frameTime = now; _frameVoices = 0; }
            if (_frameVoices >= MaxNewVoicesPerFrame) return false;
            if (_lastPlayed.TryGetValue(key, out float last) && now - last < MinInterval) return false;
            _lastPlayed[key] = now;
            _frameVoices++;
            return true;
        }
    }
}
