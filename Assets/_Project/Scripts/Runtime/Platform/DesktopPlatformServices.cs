using System.IO;
using Hollowcrawl.Save;
using UnityEngine;

namespace Hollowcrawl.Platform
{
    /// <summary>Windows / Linux / macOS / Steam Deck: files under persistentDataPath, content in StreamingAssets.</summary>
    public sealed class DesktopPlatformServices : IPlatformServices
    {
        public string PlatformName => Application.platform.ToString();
        public ISaveStorage SaveStorage { get; }
        public IAchievementBackend Achievements { get; } = new LocalAchievementBackend();
        public string ContentRoot { get; }

        public DesktopPlatformServices()
        {
            SaveStorage = new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
            ContentRoot = Path.Combine(Application.streamingAssetsPath, "Content");
        }
    }
}
