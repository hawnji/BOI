using Hollowcrawl.Combat;

namespace Hollowcrawl.Items
{
    public struct Vec2
    {
        public float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public float Length => (float)System.Math.Sqrt(X * X + Y * Y);
        public Vec2 Normalized { get { float l = Length; return l > 1e-5f ? new Vec2(X / l, Y / l) : new Vec2(0f, 0f); } }
        public Vec2 Rotated(float degrees)
        {
            double r = degrees * System.Math.PI / 180.0;
            float c = (float)System.Math.Cos(r), s = (float)System.Math.Sin(r);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    public enum PickupKind { Coin, Bomb, Key, VigorHalf, VigorFull, Ward, Umbral, Ember, Consumable, Trinket, Item }

    /// <summary>
    /// Everything item effects are allowed to do to the game world. Implemented by the Unity runtime
    /// (spawning pooled objects) and by a recording fake in headless tests. Effects never touch
    /// MonoBehaviours directly, which keeps the engine testable and platform independent.
    /// </summary>
    public interface IGameWorld
    {
        Vec2 PlayerPosition { get; }
        Vec2 AimDirection { get; }

        void SpawnProjectile(ProjectileSpec spec, Vec2 origin, Vec2 direction);
        void SpawnExplosion(Vec2 position, float radius, float damage, Team team, DamageType types);
        /// <summary>Damages every enemy in the current room (radius &lt;= 0 means the whole room).</summary>
        void DamageEnemiesInRadius(Vec2 center, float radius, float damage, DamageType types);
        void ApplyStatusToEnemy(int enemyId, string statusId, float duration, float potency);
        void ApplyStatusToEnemiesInRadius(Vec2 center, float radius, string statusId, float duration, float potency);
        void SpawnPickup(PickupKind kind, Vec2 position, string contentId = null);
        void SpawnCompanion(string companionId, object source);
        void RemoveCompanionsFromSource(object source);
        /// <summary>Finds up to maxCount enemy ids nearest to a point (excluding one id). Returns the count found.</summary>
        int FindNearestEnemies(Vec2 point, float range, int excludeId, int[] results);
        bool TryGetEnemyPosition(int enemyId, out Vec2 position);
        void TeleportPlayer(TeleportTarget target);
        void PlayFeedback(string visualKey, string audioKey, Vec2 position);
    }

    public enum TeleportTarget { RandomRoom, StartRoom, UnvisitedSpecialRoom }
}
