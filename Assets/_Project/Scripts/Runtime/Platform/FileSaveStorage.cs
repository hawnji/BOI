using System;
using System.Collections.Generic;
using System.IO;
using Hollowcrawl.Save;

namespace Hollowcrawl.Platform
{
    /// <summary>
    /// Local file storage for saves. Writes are atomic: data goes to "name.tmp", is flushed to disk,
    /// then replaces the target, so a crash or power loss mid-save never leaves a half-written file.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _root;

        public FileSaveStorage(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            Directory.CreateDirectory(_root);
        }

        public string Root => _root;

        private string PathFor(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.Contains(".."))
                throw new ArgumentException("Invalid save key: " + key);
            return Path.Combine(_root, key);
        }

        public bool Exists(string key) => File.Exists(PathFor(key));

        public byte[] Read(string key)
        {
            var p = PathFor(key);
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }

        public void Write(string key, byte[] data)
        {
            string path = PathFor(key);
            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); }
                catch (PlatformNotSupportedException) { File.Delete(path); File.Move(tmp, path); }
                catch (IOException) { File.Delete(path); File.Move(tmp, path); }
            }
            else File.Move(tmp, path);
        }

        public void Delete(string key)
        {
            var p = PathFor(key);
            if (File.Exists(p)) File.Delete(p);
        }

        public void Copy(string fromKey, string toKey)
        {
            var from = PathFor(fromKey);
            if (File.Exists(from)) Write(toKey, File.ReadAllBytes(from));
        }

        public IEnumerable<string> List(string prefix)
        {
            foreach (var f in Directory.GetFiles(_root))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith(prefix, StringComparison.Ordinal) && !name.EndsWith(".tmp", StringComparison.Ordinal)) yield return name;
            }
        }
    }
}
