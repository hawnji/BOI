using Hollowcrawl.Combat;
using Hollowcrawl.Enemies;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;

namespace Hollowcrawl.Simulation
{
    /// <summary>
    /// Run-level services a room needs: content lookups and the consequences that outlive the room
    /// (pickups, door keys, clear tracking, teleports). Implemented by <see cref="RunSimulation"/>.
    /// </summary>
    public interface IRoomHost
    {
        EnemyDefinition FindEnemy(string id);
        BossDefinition FindBoss(string id);
        StatusEffectDefinition FindStatus(string id);

        /// <summary>Called when the player touches a pickup. Return true to consume it.</summary>
        bool TryCollect(PickupActor pickup);
        /// <summary>The player walked into a closed door (try a key, refuse, ...).</summary>
        void OnDoorBumped(RoomDoor door);
        void OnBlastNearDoor(RoomDoor door);
        void OnEnemySpawned(EnemyActor enemy);
        void OnEnemyKilled(EnemyActor enemy);
        void OnPlayerDamaged(int halvesLost);
        void OnTileDestroyed(int x, int row, TileKind previous);
        void OnExitReached(ExitActor exit);
        void SpawnCompanion(string companionId, object source);
        void RemoveCompanions(object source);
        void TeleportPlayer(TeleportTarget target);
        void RevealMap(bool includeSecrets);
        /// <summary>The player touched a shrine option whose cooldown has elapsed.</summary>
        void UseShrine(ShrineActor shrine);
        void OnBombPlaced();
    }

    /// <summary>Hands out run-unique actor ids.</summary>
    public sealed class IdSource
    {
        private int _next = 1;
        public int Next() => _next++;
    }
}
