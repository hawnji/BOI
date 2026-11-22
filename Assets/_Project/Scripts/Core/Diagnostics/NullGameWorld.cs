using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Items;

namespace Hollowcrawl.Diagnostics
{
    /// <summary>
    /// A world that only counts what effects asked for. Used by the synergy debugger sandbox (editor)
    /// to inspect item combinations without running a scene.
    /// </summary>
    public sealed class NullGameWorld : IGameWorld
    {
        public Vec2 PlayerPosition { get; set; }
        public Vec2 AimDirection { get; set; } = new Vec2(1f, 0f);
        public readonly Dictionary<string, int> Calls = new Dictionary<string, int>();

        private void Count(string what) { Calls.TryGetValue(what, out int n); Calls[what] = n + 1; }

        public void SpawnProjectile(ProjectileSpec spec, Vec2 origin, Vec2 direction) => Count("projectile");
        public void SpawnExplosion(Vec2 position, float radius, float damage, Team team, DamageType types) => Count("explosion");
        public void DamageEnemiesInRadius(Vec2 center, float radius, float damage, DamageType types) => Count("area_damage");
        public void ApplyStatusToEnemy(int enemyId, string statusId, float duration, float potency) => Count("status:" + statusId);
        public void ApplyStatusToEnemiesInRadius(Vec2 center, float radius, string statusId, float duration, float potency) => Count("status_aoe:" + statusId);
        public void SpawnPickup(PickupKind kind, Vec2 position, string contentId = null) => Count("pickup:" + kind);
        public void SpawnCompanion(string companionId, object source) => Count("companion:" + companionId);
        public void RemoveCompanionsFromSource(object source) => Count("companion_removed");
        public int FindNearestEnemies(Vec2 point, float range, int excludeId, int[] results) => 0;
        public bool TryGetEnemyPosition(int enemyId, out Vec2 position) { position = default; return false; }
        public void TeleportPlayer(TeleportTarget target) => Count("teleport:" + target);
        public void PlayFeedback(string visualKey, string audioKey, Vec2 position) => Count("feedback");
    }
}
