using System.IO;
using Hollowcrawl.Content;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using UnityEditor;
using UnityEngine;

namespace Hollowcrawl.EditorTools
{
    /// <summary>Shared content access and menu commands for editor tools.</summary>
    public static class EditorContent
    {
        public static string ContentRoot => Path.Combine(Application.streamingAssetsPath, "Content");

        public static ContentDatabase Load(out RoomTemplateLibrary rooms, out ValidationReport roomReport)
        {
            var db = ContentDirectoryLoader.LoadAll(ContentRoot);
            rooms = db.BuildRoomLibrary(out roomReport);
            return db;
        }

        private static GUIStyle _mono;
        public static GUIStyle Mono
        {
            get
            {
                if (_mono != null) return _mono;
                _mono = new GUIStyle(EditorStyles.label) { richText = false, wordWrap = false, fontSize = 12 };
                _mono.font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, 12);
                return _mono;
            }
        }

        [MenuItem("Hollowcrawl/Validate All Content", priority = 1)]
        public static void ValidateAll()
        {
            var db = Load(out var rooms, out var roomReport);
            var report = ContentValidator.Validate(db, EffectRegistry.CreateDefault(), new ConditionRegistry());
            report.Merge(roomReport);
            foreach (var e in report.Errors) Debug.LogError("[Content] " + e);
            foreach (var w in report.Warnings) Debug.LogWarning("[Content] " + w);
            Debug.Log($"[Content] {db.Items.Count} items ({db.CountItems(ItemKind.Passive)} passive, {db.CountItems(ItemKind.Active)} active, {db.CountItems(ItemKind.Trinket)} trinkets), " +
                      $"{db.Synergies.Count} synergies, {db.Transformations.Count} transformations, {rooms.Count}/{db.RoomTemplates.Count} valid room templates, {db.Floors.Count} floors. " +
                      $"{report.Errors.Count} errors, {report.Warnings.Count} warnings.");
        }

        [MenuItem("Hollowcrawl/Export Asset Manifest", priority = 2)]
        public static void ExportAssetManifest()
        {
            var db = Load(out _, out _);
            var path = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "docs", "ASSET_MANIFEST.md");
            System.IO.File.WriteAllText(path, AssetManifest.ToMarkdown(AssetManifest.Build(db)));
            Debug.Log("[Content] Asset manifest written to " + path);
        }

        [MenuItem("Hollowcrawl/Open Save Folder", priority = 50)]
        public static void OpenSaveFolder()
        {
            string path = Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Hollowcrawl/Open Content Folder", priority = 51)]
        public static void OpenContentFolder() => EditorUtility.RevealInFinder(ContentRoot);
    }
}
