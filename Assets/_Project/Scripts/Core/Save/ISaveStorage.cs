using System.Collections.Generic;

namespace Hollowcrawl.Save
{
    /// <summary>
    /// Platform storage abstraction. Windows uses files under Application.persistentDataPath; other
    /// platforms (Steam Cloud, console save APIs) plug in here without touching gameplay.
    /// Implementations must make Write atomic (write-temp-then-replace).
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string key);
        byte[] Read(string key);
        void Write(string key, byte[] data);
        void Delete(string key);
        void Copy(string fromKey, string toKey);
        IEnumerable<string> List(string prefix);
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, byte[]> _data = new Dictionary<string, byte[]>();
        public bool Exists(string key) => _data.ContainsKey(key);
        public byte[] Read(string key) => _data.TryGetValue(key, out var d) ? (byte[])d.Clone() : null;
        public void Write(string key, byte[] data) => _data[key] = (byte[])data.Clone();
        public void Delete(string key) => _data.Remove(key);
        public void Copy(string fromKey, string toKey) { if (_data.TryGetValue(fromKey, out var d)) _data[toKey] = (byte[])d.Clone(); }
        public IEnumerable<string> List(string prefix) { foreach (var k in _data.Keys) if (k.StartsWith(prefix, System.StringComparison.Ordinal)) yield return k; }
        /// <summary>Test helper: corrupt a stored blob.</summary>
        public void Corrupt(string key) { if (_data.TryGetValue(key, out var d) && d.Length > 10) d[d.Length / 2] ^= 0x5A; }
    }
}
