using System;
using Hollowcrawl.Items;

namespace Hollowcrawl.Movement
{
    /// <summary>Tunables for top-down movement. Defaults favour tight, responsive control.</summary>
    public sealed class MovementTuning
    {
        /// <summary>World units per second at MoveSpeed stat 1.0.</summary>
        public float BaseSpeed = 5.2f;
        /// <summary>Acceleration in units/s² while input is held.</summary>
        public float Acceleration = 70f;
        /// <summary>Deceleration when input is released.</summary>
        public float Deceleration = 90f;
        /// <summary>Extra acceleration multiplier when input opposes current velocity (snappy reversals).</summary>
        public float TurnBoost = 1.8f;
        /// <summary>How fast knockback velocity decays (per second, exponential).</summary>
        public float KnockbackDamping = 9f;
        /// <summary>Input magnitudes below this are ignored (stick drift).</summary>
        public float InputDeadzone = 0.15f;
    }

    /// <summary>Per-surface movement properties (water slows, ice slides). Combined multiplicatively.</summary>
    public struct SurfaceModifier
    {
        public float SpeedMultiplier;      // 1 = normal
        public float TractionMultiplier;   // 1 = normal accel/decel, 0.2 = icy
        public static SurfaceModifier Normal => new SurfaceModifier { SpeedMultiplier = 1f, TractionMultiplier = 1f };
    }

    /// <summary>
    /// Deterministic top-down movement integrator (eight-directional for digital input, analog for sticks).
    /// The Unity PlayerController feeds input and modifiers each FixedUpdate and applies the returned
    /// velocity to a Rigidbody2D. Kept engine-free so feel can be unit-tested and tuned headless.
    /// </summary>
    public sealed class MovementModel
    {
        private readonly MovementTuning _t;
        private Vec2 _velocity;
        private Vec2 _knockback;
        private float _lockTimer;
        private float _rootTimer;

        public MovementModel(MovementTuning tuning = null) { _t = tuning ?? new MovementTuning(); }

        public Vec2 Velocity => _velocity + _knockback;
        public Vec2 ControlledVelocity => _velocity;
        public Vec2 Knockback => _knockback;
        public bool IsInputLocked => _lockTimer > 0f;
        public bool IsRooted => _rootTimer > 0f;
        /// <summary>Last non-zero movement direction (for facing/animation).</summary>
        public Vec2 Facing { get; private set; } = new Vec2(0f, -1f);

        /// <summary>Adds an impulse (units/second) that decays over time and ignores input control.</summary>
        public void AddKnockback(Vec2 impulse, float maxSpeed = 14f)
        {
            _knockback = _knockback + impulse;
            float len = _knockback.Length;
            if (len > maxSpeed) _knockback = _knockback * (maxSpeed / len);
        }

        /// <summary>Ignore input for a duration (stagger, cutscene, door transition). Momentum still decays.</summary>
        public void LockInput(float seconds) { if (seconds > _lockTimer) _lockTimer = seconds; }

        /// <summary>Prevent all controlled movement (roots/freeze); knockback still applies.</summary>
        public void Root(float seconds) { if (seconds > _rootTimer) _rootTimer = seconds; }

        public void Stop() { _velocity = default; _knockback = default; }

        /// <summary>
        /// Advances one step. <paramref name="input"/> magnitude 0..1; <paramref name="speedStat"/> is the MoveSpeed stat;
        /// <paramref name="statusMultiplier"/> comes from status effects (slows); <paramref name="surface"/> from the floor.
        /// Returns the total velocity to apply.
        /// </summary>
        public Vec2 Step(float dt, Vec2 input, float speedStat, float statusMultiplier, SurfaceModifier surface)
        {
            if (dt <= 0f) return Velocity;
            if (_lockTimer > 0f) _lockTimer = Math.Max(0f, _lockTimer - dt);
            if (_rootTimer > 0f) _rootTimer = Math.Max(0f, _rootTimer - dt);

            float mag = input.Length;
            if (mag < _t.InputDeadzone || _lockTimer > 0f) { input = default; mag = 0f; }
            else if (mag > 1f) { input = input * (1f / mag); mag = 1f; }

            float maxSpeed = _t.BaseSpeed * Math.Max(0f, speedStat) * Math.Max(0f, statusMultiplier) * Math.Max(0f, surface.SpeedMultiplier);
            if (_rootTimer > 0f) maxSpeed = 0f;
            float traction = Math.Max(0.05f, surface.TractionMultiplier);

            Vec2 target = mag > 0f ? input * maxSpeed : default;
            if (mag > 0f) Facing = input.Normalized;

            Vec2 delta = target - _velocity;
            float deltaLen = delta.Length;
            if (deltaLen > 1e-5f)
            {
                bool accelerating = mag > 0f;
                float rate = (accelerating ? _t.Acceleration : _t.Deceleration) * traction;
                // Opposing input (dot < 0) gets a boost so direction changes feel immediate.
                if (accelerating && (_velocity.X * target.X + _velocity.Y * target.Y) < 0f) rate *= _t.TurnBoost;
                float step = rate * dt;
                _velocity = step >= deltaLen ? target : _velocity + delta * (step / deltaLen);
            }

            if (_knockback.X != 0f || _knockback.Y != 0f)
            {
                float decay = (float)Math.Exp(-_t.KnockbackDamping * dt);
                _knockback = _knockback * decay;
                if (_knockback.Length < 0.05f) _knockback = default;
            }
            return Velocity;
        }

        /// <summary>Snaps an analog direction to the nearest of 8 directions (used for digital-feel options and facing sprites).</summary>
        public static Vec2 SnapTo8(Vec2 dir)
        {
            if (dir.Length < 1e-5f) return default;
            double angle = Math.Atan2(dir.Y, dir.X);
            double snapped = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
            return new Vec2((float)Math.Cos(snapped), (float)Math.Sin(snapped));
        }
    }
}
