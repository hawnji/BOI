using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hollowcrawl.Save
{
    public enum LoadStatus { Loaded, Created, RecoveredFromBackup, ResetAfterCorruption }

    public sealed class LoadResult<T>
    {
        public T Data;
        public LoadStatus Status;
        public int LoadedSchema;
        public readonly List<string> Messages = new List<string>();
    }

    /// <summary>Upgrades a payload from schema N to N+1.</summary>
    public interface ISaveMigration
    {
        int FromSchema { get; }
        void Apply(JObject payload);
    }

    /// <summary>
    /// Versioned, checksummed, backed-up JSON saves.
    /// File layout per key: "key" (current), "key.bak1".."key.bakN" (previous good saves).
    /// Envelope: { format, kind, schema, savedUtc, checksum (SHA-256 of payload JSON), payload }.
    /// Load order: current -> backups (newest first). A corrupt current file is preserved as
    /// "key.corrupt" for diagnosis. Older schemas are migrated forward; newer schemas are refused
    /// (protects saves from being clobbered by an older build).
    /// </summary>
    public sealed class SaveManager
    {
        public const string Format = "hollowcrawl-save";
        public int BackupCount { get; set; } = 3;

        private readonly ISaveStorage _storage;
        private readonly JsonSerializerSettings _json;
        private readonly Dictionary<string, SortedDictionary<int, ISaveMigration>> _migrations = new Dictionary<string, SortedDictionary<int, ISaveMigration>>();

        public SaveManager(ISaveStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _json = Content.JsonContent.CreateSettings();
        }

        public void RegisterMigration(string kind, ISaveMigration migration)
        {
            if (!_migrations.TryGetValue(kind, out var m)) _migrations[kind] = m = new SortedDictionary<int, ISaveMigration>();
            m[migration.FromSchema] = migration;
        }

        public void Save<T>(string key, string kind, int schema, T data)
        {
            var payload = JObject.FromObject(data, JsonSerializer.Create(_json));
            string payloadText = payload.ToString(Formatting.None);
            var envelope = new JObject
            {
                ["format"] = Format,
                ["kind"] = kind,
                ["schema"] = schema,
                ["savedUtc"] = DateTime.UtcNow.ToString("o"),
                ["checksum"] = Sha256(payloadText),
                ["payload"] = payload,
            };
            byte[] bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));

            // Rotate backups only when the current file is itself valid, so a corrupt file never pushes out good backups.
            if (_storage.Exists(key) && TryReadEnvelope(_storage.Read(key), kind, out _, out _, out _))
            {
                for (int i = BackupCount - 1; i >= 1; i--)
                    if (_storage.Exists(BackupKey(key, i))) _storage.Copy(BackupKey(key, i), BackupKey(key, i + 1));
                _storage.Copy(key, BackupKey(key, 1));
            }
            _storage.Write(key, bytes);
        }

        public LoadResult<T> Load<T>(string key, string kind, int currentSchema, Func<T> createDefault) where T : class
        {
            var result = new LoadResult<T>();
            var candidates = new List<string> { key };
            for (int i = 1; i <= BackupCount; i++) candidates.Add(BackupKey(key, i));

            bool anyExisted = false;
            for (int c = 0; c < candidates.Count; c++)
            {
                string k = candidates[c];
                if (!_storage.Exists(k)) continue;
                anyExisted = true;
                if (!TryReadEnvelope(_storage.Read(k), kind, out var payload, out int schema, out string error))
                {
                    result.Messages.Add($"{k}: {error}");
                    if (c == 0) _storage.Copy(k, key + ".corrupt");
                    continue;
                }
                if (schema > currentSchema)
                {
                    result.Messages.Add($"{k}: saved by a newer version (schema {schema} > {currentSchema}); not loaded.");
                    continue;
                }
                try
                {
                    Migrate(kind, payload, schema, currentSchema, result.Messages);
                    var data = payload.ToObject<T>(JsonSerializer.Create(_json));
                    if (data == null) { result.Messages.Add($"{k}: empty payload."); continue; }
                    result.Data = data;
                    result.LoadedSchema = schema;
                    result.Status = c == 0 ? LoadStatus.Loaded : LoadStatus.RecoveredFromBackup;
                    if (c > 0) Save(key, kind, currentSchema, data); // heal the main file from the backup
                    return result;
                }
                catch (Exception ex)
                {
                    result.Messages.Add($"{k}: could not deserialize ({ex.Message}).");
                }
            }

            result.Data = createDefault();
            result.Status = anyExisted ? LoadStatus.ResetAfterCorruption : LoadStatus.Created;
            return result;
        }

        public bool Exists(string key) => _storage.Exists(key);

        public void Delete(string key)
        {
            _storage.Delete(key);
            for (int i = 1; i <= BackupCount; i++) _storage.Delete(BackupKey(key, i));
        }

        private void Migrate(string kind, JObject payload, int from, int to, List<string> log)
        {
            if (from == to) return;
            _migrations.TryGetValue(kind, out var steps);
            for (int s = from; s < to; s++)
            {
                if (steps == null || !steps.TryGetValue(s, out var m))
                    throw new InvalidOperationException($"No migration for '{kind}' from schema {s}.");
                m.Apply(payload);
                log.Add($"Migrated {kind} schema {s} -> {s + 1}.");
            }
        }

        private static bool TryReadEnvelope(byte[] bytes, string kind, out JObject payload, out int schema, out string error)
        {
            payload = null; schema = 0; error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty file"; return false; }
            try
            {
                var env = JObject.Parse(Encoding.UTF8.GetString(bytes));
                if ((string)env["format"] != Format) { error = "unknown format"; return false; }
                if ((string)env["kind"] != kind) { error = $"wrong kind '{(string)env["kind"]}'"; return false; }
                schema = (int?)env["schema"] ?? 0;
                payload = env["payload"] as JObject;
                if (payload == null) { error = "missing payload"; return false; }
                if (Sha256(payload.ToString(Formatting.None)) != (string)env["checksum"]) { error = "checksum mismatch"; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = "unreadable (" + ex.GetType().Name + ")";
                return false;
            }
        }

        private static string BackupKey(string key, int index) => key + ".bak" + index;

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>Well-known save keys and the typed load/save helpers the game uses.</summary>
    public sealed class SaveService
    {
        public const int ProfileSlots = 3;
        public const string SettingsKey = "settings.json";
        public static string ProfileKey(int slot) => $"profile{slot}.json";
        public const string RunKey = "run_in_progress.json";

        public SaveManager Manager { get; }
        public SaveService(ISaveStorage storage) { Manager = new SaveManager(storage); }

        public LoadResult<SettingsData> LoadSettings() => Manager.Load(SettingsKey, "settings", SettingsData.CurrentSchema, () => new SettingsData());
        public void SaveSettings(SettingsData s) => Manager.Save(SettingsKey, "settings", SettingsData.CurrentSchema, s);

        public LoadResult<ProfileData> LoadProfile(int slot) => Manager.Load(ProfileKey(Clamp(slot)), "profile", ProfileData.CurrentSchema, () => new ProfileData());
        public void SaveProfile(int slot, ProfileData p) => Manager.Save(ProfileKey(Clamp(slot)), "profile", ProfileData.CurrentSchema, p);
        public void DeleteProfile(int slot) => Manager.Delete(ProfileKey(Clamp(slot)));
        public bool ProfileExists(int slot) => Manager.Exists(ProfileKey(Clamp(slot)));

        private static int Clamp(int slot) => slot < 0 ? 0 : (slot >= ProfileSlots ? ProfileSlots - 1 : slot);
    }
}
