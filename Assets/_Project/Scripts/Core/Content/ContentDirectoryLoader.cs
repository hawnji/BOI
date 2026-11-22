using System;
using System.IO;

namespace Hollowcrawl.Content
{
    /// <summary>Loads every *.json under a content root, in ordinal path order (deterministic).</summary>
    public static class ContentDirectoryLoader
    {
        public static ContentDatabase LoadAll(string root)
        {
            var db = new ContentDatabase();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                db.LoadErrors.Add("Content folder not found: " + root);
                return db;
            }
            var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var f in files)
            {
                string rel = f.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                db.LoadJson(rel, File.ReadAllText(f));
            }
            return db;
        }
    }
}
