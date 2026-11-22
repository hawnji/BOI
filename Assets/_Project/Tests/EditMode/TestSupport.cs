using System;
using System.Collections.Generic;
using System.IO;
using Hollowcrawl.Combat;
using Hollowcrawl.Content;
using Hollowcrawl.Economy;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Tests
{
    /// <summary>Records every world call so tests can assert what effects did.</summary>
    public sealed class FakeWorld : IGameWorld
    {
        public Vec2 PlayerPosition { get; set; } = new Vec2(0f, 0f);
        public Vec2 AimDirection { get; set; } = new Vec2(1f, 0f);
        public readonly List<ProjectileSpec> Projectiles = new List<ProjectileSpec>();
        public readonly List<(Vec2 pos, float radius, float damage)> Explosions = new List<(Vec2, float, float)>();
        public readonly List<(float radius, float damage)> AreaDamage = new List<(float, float)>();
        public readonly List<string> Statuses = new List<string>();
        public readonly List<PickupKind> Pickups = new List<PickupKind>();
        public readonly List<(string id, object source)> Companions = new List<(string, object)>();
        public readonly List<TeleportTarget> Teleports = new List<TeleportTarget>();
        public Action<ProjectileSpec> OnSpawn;

        public void SpawnProjectile(ProjectileSpec spec, Vec2 origin, Vec2 direction)
        {
            var copy = new ProjectileSpec();
            spec.CopyTo(copy);
            Projectiles.Add(copy);
            OnSpawn?.Invoke(copy);
        }
        public void SpawnExplosion(Vec2 position, float radius, float damage, Team team, DamageType types) => Explosions.Add((position, radius, damage));
        public void DamageEnemiesInRadius(Vec2 center, float radius, float damage, DamageType types) => AreaDamage.Add((radius, damage));
        public void ApplyStatusToEnemy(int enemyId, string statusId, float duration, float potency) => Statuses.Add(statusId);
        public void ApplyStatusToEnemiesInRadius(Vec2 center, float radius, string statusId, float duration, float potency) => Statuses.Add(statusId);
        public void SpawnPickup(PickupKind kind, Vec2 position, string contentId = null) => Pickups.Add(kind);
        public void SpawnCompanion(string companionId, object source) => Companions.Add((companionId, source));
        public void RemoveCompanionsFromSource(object source) => Companions.RemoveAll(c => ReferenceEquals(c.source, source));
        public int FindNearestEnemies(Vec2 point, float range, int excludeId, int[] results) => 0;
        public bool TryGetEnemyPosition(int enemyId, out Vec2 position) { position = default; return false; }
        public void TeleportPlayer(TeleportTarget target) => Teleports.Add(target);
        public void PlayFeedback(string visualKey, string audioKey, Vec2 position) { }
    }

    public static class TestContent
    {
        private static ContentDatabase _db;

        public static string ContentRoot
        {
            get
            {
                foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
                {
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Content");
                        if (Directory.Exists(candidate)) return candidate;
                        dir = dir.Parent;
                    }
                }
                throw new DirectoryNotFoundException("Could not find Assets/StreamingAssets/Content above " + AppContext.BaseDirectory);
            }
        }

        /// <summary>Loads every JSON file under Content (cached).</summary>
        public static ContentDatabase Database
        {
            get
            {
                if (_db != null) return _db;
                var db = new ContentDatabase();
                var files = Directory.GetFiles(ContentRoot, "*.json", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var f in files) db.LoadJson(Path.GetFileName(f), File.ReadAllText(f));
                _db = db;
                return db;
            }
        }

        public static ItemDefinition Item(string id) => Database.Items[id];

        public static InteractionEngine NewEngine(out FakeWorld world, uint seed = 1234, bool withCombos = true)
        {
            world = new FakeWorld();
            var stats = new StatSheet();
            var health = new HealthPool(3, 6);
            var engine = new InteractionEngine(EffectRegistry.CreateDefault(), stats, health, new PlayerResources(), world, new SeededRandom(seed));
            if (withCombos) engine.SetCombos(Database.Synergies.Values, Database.Transformations.Values);
            return engine;
        }
    }
}
