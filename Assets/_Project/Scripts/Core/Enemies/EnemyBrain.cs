using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Enemies
{
    /// <summary>What the brain needs to know about the world. Implemented by the Unity enemy component.</summary>
    public interface IEnemySensors
    {
        Vec2 SelfPosition { get; }
        Vec2 PlayerPosition { get; }
        bool HasLineOfSight { get; }
        float HealthFraction { get; }
        bool CanAttack { get; }          // false while frozen/feared (status effects)
        float MoveMultiplier { get; }    // slows
        int AliveSummons(string enemyId);
    }

    public enum BrainState { Dormant, Moving, Telegraphing, Attacking, Recovering, Burrowed }

    /// <summary>What the enemy should do this frame.</summary>
    public struct EnemyIntent
    {
        public Vec2 Velocity;
        public BrainState State;
        /// <summary>Set on the frame an attack starts its telegraph (for animation/vfx/audio cues).</summary>
        public AttackSpec TelegraphStarted;
        /// <summary>Set on each frame an attack fires (projectile waves fire several times during Duration).</summary>
        public AttackSpec AttackFired;
        public int WaveIndex;
        public Vec2 AttackDirection;
        public bool Invisible;           // burrowed
        public bool Contact;             // deals contact damage this frame
    }

    /// <summary>
    /// Modular, data-driven enemy AI. Decisions (attack selection, steering target) happen every
    /// ThinkInterval rather than every frame; between thinks the cached velocity is reused. All
    /// randomness comes from the enemy's own seeded stream, so encounters replay identically.
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly EnemyDefinition _def;
        private readonly SeededRandom _rng;
        private EnemyScaling _scaling;
        private MovementSpec _movement;
        private List<AttackSpec> _attacks;
        private readonly Dictionary<AttackSpec, float> _cooldowns = new Dictionary<AttackSpec, float>();
        private readonly WeightedPicker<AttackSpec> _picker = new WeightedPicker<AttackSpec>();

        private float _thinkTimer;
        private float _stateTimer;
        private float _globalCooldown;
        private float _waveTimer;
        private int _wave;
        private AttackSpec _current;
        private Vec2 _cachedVelocity;
        private Vec2 _lockedDirection;
        private float _orbitSign = 1f;
        private float _hopTimer;

        public BrainState State { get; private set; } = BrainState.Dormant;
        public AttackSpec CurrentAttack => _current;
        public float SpiralAngle { get; private set; }

        public EnemyBrain(EnemyDefinition def, EnemyScaling scaling, SeededRandom rng)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = rng ?? new SeededRandom(1);
            _scaling = scaling;
            SetBehaviour(def.Movement, def.Attacks);
            _orbitSign = _rng.Chance(0.5f) ? 1f : -1f;
            _globalCooldown = 0.6f + _rng.Range(0f, 0.6f); // stagger first attacks so rooms don't volley in unison
        }

        /// <summary>Swaps movement and attacks (boss phases, retreat behaviour). Cooldowns of kept attacks persist.</summary>
        public void SetBehaviour(MovementSpec movement, List<AttackSpec> attacks)
        {
            _movement = movement ?? new MovementSpec { Kind = MovementKind.Idle };
            _attacks = attacks ?? new List<AttackSpec>();
            foreach (var a in _attacks) if (!_cooldowns.ContainsKey(a)) _cooldowns[a] = a.Cooldown * 0.5f * _scaling.CooldownMultiplier;
            if (_current != null && !_attacks.Contains(_current)) { _current = null; State = BrainState.Moving; }
        }

        public void Activate() { if (State == BrainState.Dormant) State = BrainState.Moving; }

        /// <summary>Interrupts the current attack (stagger from big hits, freezing).</summary>
        public void Interrupt(float recovery)
        {
            if (_current == null) return;
            _current = null;
            State = BrainState.Recovering;
            _stateTimer = recovery;
        }

        public EnemyIntent Step(float dt, IEnemySensors s)
        {
            var intent = new EnemyIntent { State = State };
            if (State == BrainState.Dormant)
            {
                if ((s.PlayerPosition - s.SelfPosition).Length <= _def.AggroRange) State = BrainState.Moving;
                else return intent;
            }

            foreach (var a in _attacks) if (_cooldowns[a] > 0f) _cooldowns[a] -= dt;
            if (_globalCooldown > 0f) _globalCooldown -= dt;

            float speed = _movement.Speed * _scaling.SpeedMultiplier * Math.Max(0f, s.MoveMultiplier);
            Vec2 toPlayer = s.PlayerPosition - s.SelfPosition;

            switch (State)
            {
                case BrainState.Moving:
                case BrainState.Burrowed:
                    _thinkTimer -= dt;
                    if (_thinkTimer <= 0f)
                    {
                        _thinkTimer = _def.ThinkInterval;
                        _cachedVelocity = Steer(toPlayer, speed, s);
                        if (State == BrainState.Moving && s.CanAttack && _globalCooldown <= 0f && TrySelectAttack(toPlayer.Length, s, out var atk))
                        {
                            _current = atk;
                            State = BrainState.Telegraphing;
                            _stateTimer = atk.Telegraph * _scaling.TelegraphMultiplier;
                            _lockedDirection = toPlayer.Normalized;
                            intent.TelegraphStarted = atk;
                            _cachedVelocity = default;
                        }
                    }
                    intent.Velocity = _cachedVelocity;
                    intent.Contact = State == BrainState.Moving;
                    intent.Invisible = State == BrainState.Burrowed;
                    break;

                case BrainState.Telegraphing:
                    if (!s.CanAttack) { Interrupt(0.3f); break; }
                    _stateTimer -= dt;
                    intent.Contact = true;
                    if (_current.Kind != AttackKind.Dash) _lockedDirection = toPlayer.Normalized; // ranged attacks track until release
                    if (_stateTimer <= 0f)
                    {
                        State = BrainState.Attacking;
                        _stateTimer = Math.Max(0f, _current.Duration);
                        _wave = 0;
                        _waveTimer = 0f;
                        FireWave(ref intent);
                        if (_current.Kind == AttackKind.Dash) intent.Velocity = _lockedDirection * (_current.DashSpeed * _scaling.SpeedMultiplier);
                    }
                    break;

                case BrainState.Attacking:
                    intent.Contact = true;
                    if (_current.Kind == AttackKind.Dash) intent.Velocity = _lockedDirection * (_current.DashSpeed * _scaling.SpeedMultiplier);
                    _stateTimer -= dt;
                    if (_current.Waves > 1 && _wave < _current.Waves)
                    {
                        _waveTimer -= dt;
                        if (_waveTimer <= 0f) FireWave(ref intent);
                    }
                    if (_stateTimer <= 0f && (_current.Waves <= 1 || _wave >= _current.Waves))
                    {
                        _cooldowns[_current] = _current.Cooldown * _scaling.CooldownMultiplier;
                        _globalCooldown = 0.25f;
                        State = BrainState.Recovering;
                        _stateTimer = _current.Recovery;
                        if (_current.Kind == AttackKind.Teleport && _movement.Kind == MovementKind.Burrow) { State = BrainState.Burrowed; _stateTimer = _movement.PauseTime; }
                    }
                    break;

                case BrainState.Recovering:
                    _stateTimer -= dt;
                    intent.Contact = true;
                    if (_stateTimer <= 0f) { _current = null; State = BrainState.Moving; _thinkTimer = 0f; }
                    break;
            }
            intent.State = State;
            intent.AttackDirection = _lockedDirection;
            return intent;
        }

        private void FireWave(ref EnemyIntent intent)
        {
            intent.AttackFired = _current;
            intent.WaveIndex = _wave;
            _wave++;
            SpiralAngle += _current.SpiralStep;
            if (_current.Waves > 1) _waveTimer = Math.Max(0.05f, _current.Duration / _current.Waves);
        }

        private bool TrySelectAttack(float distance, IEnemySensors s, out AttackSpec chosen)
        {
            _picker.Clear();
            foreach (var a in _attacks)
            {
                if (_cooldowns[a] > 0f) continue;
                if (a.MinDifficulty > _scaling.DifficultyLevel) continue;
                if (distance < a.MinRange || distance > a.MaxRange) continue;
                if (a.RequiresLineOfSight && !s.HasLineOfSight) continue;
                if (a.Kind == AttackKind.Summon && a.SummonId != null && s.AliveSummons(a.SummonId) >= a.MaxAlive) continue;
                _picker.Add(a, a.Weight);
            }
            return _picker.TryPick(_rng, out chosen);
        }

        private Vec2 Steer(Vec2 toPlayer, float speed, IEnemySensors s)
        {
            var kind = _movement.Kind;
            if (_def.RetreatBelowHealth > 0f && s.HealthFraction < _def.RetreatBelowHealth) kind = MovementKind.KeepDistance;
            float dist = toPlayer.Length;
            var dir = toPlayer.Normalized;
            switch (kind)
            {
                case MovementKind.Idle: return default;
                case MovementKind.Chase: return dir * speed;
                case MovementKind.Wander:
                case MovementKind.Patrol:
                    if (_cachedVelocity.Length < 1e-3f || _rng.Chance(kind == MovementKind.Wander ? 0.15f : 0.02f))
                        return new Vec2(1f, 0f).Rotated(kind == MovementKind.Patrol ? 90f * _rng.Range(0, 4) : _rng.Range(0f, 360f)) * speed;
                    return _cachedVelocity;
                case MovementKind.KeepDistance:
                {
                    float pref = _movement.PreferredDistance;
                    if (dist < pref - 0.75f) return dir * -speed;
                    if (dist > pref + 0.75f) return dir * speed;
                    return new Vec2(-dir.Y, dir.X) * (speed * 0.5f * _orbitSign);
                }
                case MovementKind.Orbit:
                {
                    var tangent = new Vec2(-dir.Y, dir.X) * _orbitSign;
                    float radial = Math.Max(-1f, Math.Min(1f, (dist - _movement.PreferredDistance) * 0.5f));
                    return (tangent + dir * radial).Normalized * speed;
                }
                case MovementKind.Strafe:
                    if (_rng.Chance(0.05f)) _orbitSign = -_orbitSign;
                    return new Vec2(-dir.Y, dir.X) * (speed * _orbitSign);
                case MovementKind.Hop:
                    _hopTimer -= _def.ThinkInterval;
                    if (_hopTimer > 0f) return default;
                    _hopTimer = _movement.PauseTime + _movement.HopDistance / Math.Max(0.1f, speed);
                    return dir * speed * 1.8f;
                case MovementKind.Burrow:
                    if (State == BrainState.Burrowed)
                    {
                        _hopTimer -= _def.ThinkInterval;
                        if (_hopTimer <= 0f) State = BrainState.Moving; // resurface
                        return dir * speed * 2f;
                    }
                    if (_rng.Chance(0.08f)) { State = BrainState.Burrowed; _hopTimer = _movement.PauseTime; }
                    return dir * speed * 0.5f;
                default: return dir * speed;
            }
        }
    }
}
