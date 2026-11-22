using System;
using System.Collections.Generic;

namespace Hollowcrawl.Save
{
    /// <summary>Permanent progression for one profile slot. Bump <see cref="CurrentSchema"/> and add a migration when changing shape.</summary>
    public sealed class ProfileData
    {
        public const int CurrentSchema = 1;

        public string ProfileName = "Wanderer";
        public long CreatedUtcTicks = DateTime.UtcNow.Ticks;
        public HashSet<string> Unlocks = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Achievement id -> unlock time (UTC ticks).</summary>
        public Dictionary<string, long> Achievements = new Dictionary<string, long>(StringComparer.Ordinal);
        /// <summary>Character id -> completion mark ids earned with that character.</summary>
        public Dictionary<string, HashSet<string>> CompletionMarks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        public HashSet<string> DiscoveredItems = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> DiscoveredSynergies = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> DiscoveredTransformations = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> SeenEnemies = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> ChallengesCompleted = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> EndingsSeen = new HashSet<string>(StringComparer.Ordinal);
        public LifetimeStatistics Stats = new LifetimeStatistics();
        /// <summary>Last completed daily run date (yyyy-MM-dd) and best score, for the offline daily mode.</summary>
        public Dictionary<string, int> DailyResults = new Dictionary<string, int>(StringComparer.Ordinal);

        public bool IsUnlocked(string id) => string.IsNullOrEmpty(id) || Unlocks.Contains(id);

        public bool AddMark(string characterId, string markId)
        {
            if (!CompletionMarks.TryGetValue(characterId, out var set))
                CompletionMarks[characterId] = set = new HashSet<string>(StringComparer.Ordinal);
            return set.Add(markId);
        }
    }

    public sealed class LifetimeStatistics
    {
        public long TotalRuns, Wins, Deaths, EnemiesDefeated, BossesDefeated, ItemsCollected;
        public long FloorsCompleted, SecretsFound, ChallengeCompletions, BombsUsed, CoinsCollected;
        public double DamageDealt, DamageReceived, SecondsPlayed;
        public int BestStreak, CurrentStreak, DeepestFloor;
        public Dictionary<string, long> RunsByCharacter = new Dictionary<string, long>(StringComparer.Ordinal);
        public Dictionary<string, long> WinsByCharacter = new Dictionary<string, long>(StringComparer.Ordinal);
        public Dictionary<string, long> BossKills = new Dictionary<string, long>(StringComparer.Ordinal);
        /// <summary>Open-ended counters for achievements ("pickups_coins", "synergies_triggered", ...).</summary>
        public Dictionary<string, long> Counters = new Dictionary<string, long>(StringComparer.Ordinal);

        public long Increment(string counter, long by = 1)
        {
            Counters.TryGetValue(counter, out long v);
            v += by;
            Counters[counter] = v;
            return v;
        }
    }

    public enum ShootMode { HoldToFire = 0, ToggleAutoFire = 1, AimToFire = 2 }
    public enum WindowMode { Fullscreen = 0, Borderless = 1, Windowed = 2 }

    /// <summary>Device-wide settings (not per profile).</summary>
    public sealed class SettingsData
    {
        public const int CurrentSchema = 1;

        // Audio (0..1)
        public float MasterVolume = 0.8f, MusicVolume = 0.7f, SfxVolume = 0.8f, UiVolume = 0.8f;
        public bool MuteWhenUnfocused = true;

        // Graphics
        public int ResolutionWidth, ResolutionHeight; // 0 = native
        public WindowMode WindowMode = WindowMode.Borderless;
        public bool VSync = true;
        public int TargetFrameRate = 0; // 0 = unlimited / vsync
        public float UiScale = 1f;

        // Accessibility
        public float ScreenShake = 1f;      // 0..1
        public bool ReduceFlashing;
        public float TextScale = 1f;
        public string ColorPalette = "default"; // "deuteranopia", "protanopia", "tritanopia", "high_contrast"
        public bool ShowDamageNumbers = true;
        public float AimAssist = 0.25f;     // controller only

        // Gameplay & input
        public ShootMode GamepadShootMode = ShootMode.AimToFire;
        public ShootMode MouseShootMode = ShootMode.HoldToFire;
        public float StickDeadzone = 0.2f;
        public bool VibrationEnabled = true;
        /// <summary>Unity Input System binding overrides JSON (InputActionAsset.SaveBindingOverridesAsJson).</summary>
        public string BindingOverridesJson = "";
        public string Language = "en";
        public int LastProfileSlot;
    }
}
