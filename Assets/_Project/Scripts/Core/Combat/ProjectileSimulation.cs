using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Combat
{
    /// <summary>World queries a projectile needs (implemented by the Unity room/enemy registry, faked in tests).</summary>
    public interface IProjectileQueries
    {
        /// <summary>Fills results with up to results.Length enemy ids nearest to point within range, skipping excludeId. Returns count.</summary>
        int FindNearestEnemies(Vec2 point, float range, int excludeId, int[] results);
        bool TryGetEnemyPosition(int enemyId, out Vec2 position);
        Vec2 ShooterPosition { get; }
    }

    public struct ProjectileSpawnRequest
    {
        public ProjectileSpec Spec;
        public Vec2 Origin;
        public Vec2 Direction;
    }

    /// <summary>Everything that should happen as a result of a collision or expiry. Reused per projectile (no allocations).</summary>
    public sealed class ProjectileOutcome
    {
        public bool Destroyed;
        public float Damage;
        public bool Critical;
        public bool Explode;
        public Vec2 ExplosionPosition;
        public float ExplosionRadius;
        public float ExplosionDamage;
        public readonly List<ProjectileSpawnRequest> Spawns = new List<ProjectileSpawnRequest>(8);
        /// <summary>Enemies hit by chain lightning (id), each taking ChainDamage.</summary>
        public readonly List<int> ChainTargets = new List<int>(4);
        public float ChainDamage;

        public void Clear()
        {
            Destroyed = false; Damage = 0f; Critical = false; Explode = false;
            ExplosionPosition = default; ExplosionRadius = 0f; ExplosionDamage = 0f;
            Spawns.Clear(); ChainTargets.Clear(); ChainDamage = 0f;
        }
    }

    /// <summary>
    /// Engine-free flight and collision rules for one projectile: homing, acceleration, wavy flight,
    /// boomerang return, lobbed landing, range expiry, piercing, bouncing, splitting, chaining and
    /// explosions. The Unity ProjectileBehaviour moves its transform to <see cref="Position"/> and
    /// forwards physics contacts to <see cref="HitEnemy"/> / <see cref="HitWall"/>.
    /// </summary>
    public sealed class ProjectileSimulation
    {
        private readonly int[] _queryBuffer = new int[12];
        private readonly HashSet<int> _hitIds = new HashSet<int>();
        private Vec2 _basePosition;
        private float _sinePhase;
        private bool _returning;

        public ProjectileSpec Spec { get; } = new ProjectileSpec();
        public ProjectileOutcome Outcome { get; } = new ProjectileOutcome();
        public Vec2 Position { get; private set; }
        public Vec2 Direction { get; private set; }
        public float Speed { get; private set; }
        public float Travelled { get; private set; }
        public float Age { get; private set; }
        public int PierceLeft { get; private set; }
        public int BouncesLeft { get; private set; }
        public bool Alive { get; private set; }
        /// <summary>Lobbed shots only collide when they land.</summary>
        public bool IsAirborne => Spec.Delivery == ProjectileDelivery.Lobbed && Alive;
        public int HitCount => _hitIds.Count;

        public void Launch(ProjectileSpec spec, Vec2 origin, Vec2 direction)
        {
            spec.CopyTo(Spec);
            Spec.Sanitize();
            Position = _basePosition = origin;
            Direction = direction.Length < 1e-5f ? new Vec2(0f, -1f) : direction.Normalized;
            Speed = Spec.Speed;
            Travelled = 0f; Age = 0f; _sinePhase = 0f; _returning = false;
            PierceLeft = Spec.PierceCount;
            BouncesLeft = Spec.Bounces;
            _hitIds.Clear();
            Outcome.Clear();
            Alive = true;
        }

        /// <summary>Advances the flight. Returns false once the projectile has expired (Outcome then holds expiry effects).</summary>
        public bool Step(float dt, IProjectileQueries queries)
        {
            if (!Alive) return false;
            Outcome.Clear();
            Age += dt;

            if (Spec.HomingStrength > 0f && queries != null && !_returning) Steer(dt, queries);
            if (Spec.AccelerationPerSecond != 0f) Speed = Math.Max(0.5f, Math.Min(Spec.Speed * 3f, Speed + Spec.AccelerationPerSecond * dt));

            if (Spec.Delivery == ProjectileDelivery.Boomerang && !_returning && Travelled >= Spec.Range * 0.5f) { _returning = true; _hitIds.Clear(); }
            if (_returning && queries != null)
            {
                var toShooter = queries.ShooterPosition - _basePosition;
                if (toShooter.Length < 0.4f) { Alive = false; return false; } // caught
                Direction = toShooter.Normalized;
            }

            float step = Speed * dt;
            _basePosition = _basePosition + Direction * step;
            Travelled += step;

            if (Spec.SineAmplitude > 0f)
            {
                _sinePhase += dt * 12f;
                var perp = new Vec2(-Direction.Y, Direction.X);
                Position = _basePosition + perp * ((float)Math.Sin(_sinePhase) * Spec.SineAmplitude);
            }
            else Position = _basePosition;

            float maxTravel = Spec.Delivery == ProjectileDelivery.Boomerang ? Spec.Range * 1.6f : Spec.Range;
            if (Travelled >= maxTravel)
            {
                Expire();
                return false;
            }
            return true;
        }

        private void Steer(float dt, IProjectileQueries q)
        {
            int n = q.FindNearestEnemies(Position, 6f + Spec.HomingStrength * 4f, -1, _queryBuffer);
            for (int i = 0; i < n; i++)
            {
                if (!q.TryGetEnemyPosition(_queryBuffer[i], out var target)) continue;
                var want = (target - Position).Normalized;
                float maxTurn = Spec.HomingStrength * 360f * dt;
                float angle = SignedAngle(Direction, want);
                float turn = Math.Max(-maxTurn, Math.Min(maxTurn, angle));
                Direction = Direction.Rotated(turn).Normalized;
                return;
            }
        }

        /// <summary>
        /// Resolves a hit on an enemy. Returns false if this enemy was already hit by this projectile (piercing
        /// shots never double-hit). On true, Outcome holds damage and any chain/explosion/split effects.
        /// </summary>
        public bool HitEnemy(int enemyId, Vec2 enemyPosition, SeededRandom rng, float luck, IProjectileQueries queries)
        {
            Outcome.Clear();
            if (!Alive || IsAirborne || !_hitIds.Add(enemyId)) return false;

            Outcome.Damage = Spec.RollDamage(rng, luck, out bool crit);
            Outcome.Critical = crit;

            if (Spec.ChainJumps > 0 && queries != null)
            {
                int n = queries.FindNearestEnemies(enemyPosition, Spec.ChainRange, enemyId, _queryBuffer);
                for (int i = 0; i < n && Outcome.ChainTargets.Count < Spec.ChainJumps; i++)
                    if (!_hitIds.Contains(_queryBuffer[i])) Outcome.ChainTargets.Add(_queryBuffer[i]);
                Outcome.ChainDamage = Outcome.Damage * Spec.ChainDamageScale;
            }

            bool consumed;
            if (Spec.Delivery == ProjectileDelivery.Wave || PierceLeft < 0) consumed = false;
            else if (PierceLeft > 0) { PierceLeft--; consumed = false; }
            else consumed = true;

            if (Spec.ExplosionRadius > 0f) SetExplosion(Position);
            if (consumed)
            {
                AddSplits(Direction);
                Outcome.Destroyed = true;
                Alive = false;
            }
            return true;
        }

        /// <summary>Resolves contact with a wall/obstacle. Spectral shots ignore obstacles (walls still stop them).</summary>
        public void HitWall(Vec2 normal, bool isRoomBoundary)
        {
            Outcome.Clear();
            if (!Alive || IsAirborne) return;
            if (Spec.Spectral && !isRoomBoundary) return;
            if (BouncesLeft > 0 && normal.Length > 1e-5f)
            {
                BouncesLeft--;
                var n = normal.Normalized;
                float d = Direction.X * n.X + Direction.Y * n.Y;
                Direction = new Vec2(Direction.X - 2f * d * n.X, Direction.Y - 2f * d * n.Y).Normalized;
                _hitIds.Clear(); // a bounced shot may hit the same enemy again
                return;
            }
            if (Spec.Delivery == ProjectileDelivery.Beam) return;
            Expire();
        }

        /// <summary>Range ran out or a wall stopped the shot: explosive shots detonate and splitting shots burst.</summary>
        private void Expire()
        {
            Alive = false;
            Outcome.Destroyed = true;
            if (Spec.ExplosionRadius > 0f) SetExplosion(Position);
            AddSplits(Direction);
        }

        private void SetExplosion(Vec2 at)
        {
            Outcome.Explode = true;
            Outcome.ExplosionPosition = at;
            Outcome.ExplosionRadius = Spec.ExplosionRadius * Math.Max(0.5f, Spec.Size);
            Outcome.ExplosionDamage = Spec.Damage * Spec.ExplosionDamageScale;
        }

        private void AddSplits(Vec2 forward)
        {
            if (!Spec.CanSplit) return;
            int count = Spec.SplitCount;
            // Two children fan out at ±45°, more spread evenly around the impact.
            float step = count <= 2 ? 90f : 360f / count;
            float start = count <= 2 ? -45f : 0f;
            for (int i = 0; i < count; i++)
            {
                var child = new ProjectileSpec();
                Spec.MakeSplitChild(child);
                Outcome.Spawns.Add(new ProjectileSpawnRequest { Spec = child, Origin = Position, Direction = forward.Rotated(start + step * i) });
            }
        }

        private static float SignedAngle(Vec2 from, Vec2 to)
        {
            double a = Math.Atan2(from.X * to.Y - from.Y * to.X, from.X * to.X + from.Y * to.Y);
            return (float)(a * 180.0 / Math.PI);
        }
    }
}
