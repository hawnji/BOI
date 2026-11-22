using Hollowcrawl.Save;

namespace Hollowcrawl.Platform
{
    /// <summary>
    /// Everything that differs per platform/storefront. The Windows build uses the local
    /// implementation; Linux/macOS/Steam Deck reuse it, and a storefront (achievement sync, cloud
    /// saves) can be added by implementing this interface without touching gameplay code.
    /// </summary>
    public interface IPlatformServices
    {
        string PlatformName { get; }
        ISaveStorage SaveStorage { get; }
        IAchievementBackend Achievements { get; }
        /// <summary>Absolute path of the read-only content folder (JSON data).</summary>
        string ContentRoot { get; }
    }

    /// <summary>Mirrors achievement unlocks to a platform (no-op locally; progression is saved in the profile).</summary>
    public interface IAchievementBackend
    {
        void Unlock(string achievementId);
        void SetProgress(string achievementId, int current, int target);
    }

    public sealed class LocalAchievementBackend : IAchievementBackend
    {
        public void Unlock(string achievementId) { }
        public void SetProgress(string achievementId, int current, int target) { }
    }
}
