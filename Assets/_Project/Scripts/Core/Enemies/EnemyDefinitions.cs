using System;
using System.Collections.Generic;

namespace Hollowcrawl.Enemies
{
    public enum MovementKind
    {
        Idle = 0,         // turrets, stationary casters
        Wander = 1,       // random drifting, ignores player
        Chase = 2,        // straight pursuit
        KeepDistance = 3, // stays within a preferred band (ranged)
        Orbit = 4,        // circles the player
        Hop = 5,          // short jumps toward the player with pauses
        Strafe = 6,       // moves sideways relative to the player
        Burrow = 7,       // disappears and resurfaces near the player
        Patrol = 8,       // bounces between walls in straight lines
    }

    public enum AttackKind
    {
        Projectile = 0,  // fires a pattern of shots
        Dash = 1,        // telegraphed charge in a line
        Summon = 2,      // spawns minions
        Slam = 3,        // area damage around self after telegraph
        Explode = 4,     // suicide burst
        Hazard = 5,      // leaves a puddle/fire/spikes area
        Teleport = 6,    // repositions (can chain into another attack)
        Beam = 7,        // sustained line attack
    }

    public enum ShotPattern { Aimed = 0, Spread = 1, Ring = 2, Spiral = 3, Cross = 4, X = 5, RandomBurst = 6, Wall = 7 }

    public enum BossTier { Early = 0, Mid = 1, Late = 2, Secret = 3, Optional = 4, Challenge = 5, Final = 6, Miniboss = 7 }

    public sealed class MovementSpec
    {
        public MovementKind Kind = MovementKind.Chase;
        public float Speed = 2.5f;             // units/s at difficulty 1
        public float PreferredDistance = 4f;   // KeepDistance / Orbit
        public float PauseTime = 0.6f;         // Hop / Burrow downtime
        public float HopDistance = 2f;
        public bool Flying;                    // ignores pits and obstacles
    }

    public sealed class AttackSpec
    {
        public string Id;
        public AttackKind Kind = AttackKind.Projectile;
        public float Weight = 1f;
        public float Cooldown = 2f;
        /// <summary>Wind-up shown to the player before the attack happens (readable telegraph).</summary>
        public float Telegraph = 0.5f;
        /// <summary>How long the attack itself lasts (dash duration, beam duration, spiral firing time).</summary>
        public float Duration = 0f;
        public float Recovery = 0.3f;
        public float MinRange = 0f;
        public float MaxRange = 99f;
        public bool RequiresLineOfSight = true;
        // Projectile
        public ShotPattern Pattern = ShotPattern.Aimed;
        public int Count = 1;
        public float Spread = 15f;
        public float ProjectileSpeed = 6f;
        public int Damage = 1;          // half points dealt to the player
        public int Waves = 1;           // repeat volleys during Duration
        public float SpiralStep = 15f;  // degrees between successive spiral shots
        // Dash / Slam / Explode / Hazard
        public float DashSpeed = 10f;
        public float Radius = 1.5f;
        public string HazardId;
        // Summon
        public string SummonId;
        public int SummonCount = 1;
        public int MaxAlive = 4;
        /// <summary>Only used at or above this difficulty level (harder modes unlock extra mechanics).</summary>
        public int MinDifficulty = 0;
        public string TelegraphKey;    // animation/vfx key
        public string AudioKey;
    }

    public enum DeathEffectKind { None = 0, Explode = 1, SpawnEnemies = 2, ProjectileRing = 3, Hazard = 4, Split = 5 }

    public sealed class DeathEffectSpec
    {
        public DeathEffectKind Kind;
        public string EnemyId;
        public int Count = 2;
        public float Radius = 1.5f;
        public int Damage = 1;
        public float ProjectileSpeed = 5f;
        public string HazardId;
    }

    public sealed class EnemyDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string SpriteKey;
        public float Health = 10f;
        /// <summary>Extra health per floor depth (applied before difficulty).</summary>
        public float HealthPerFloor = 0.15f;
        public int ContactDamage = 1;
        public float Radius = 0.4f;
        /// <summary>0 = full knockback, 1 = immovable.</summary>
        public float KnockbackResistance = 0f;
        public float AggroRange = 99f;
        /// <summary>Seconds between AI decisions (movement steering is reused in between).</summary>
        public float ThinkInterval = 0.15f;
        public float RetreatBelowHealth = 0f;   // fraction; switch to KeepDistance when below
        public string[] Tags = Array.Empty<string>();
        public string[] StatusImmunities = Array.Empty<string>();
        public MovementSpec Movement = new MovementSpec();
        public List<AttackSpec> Attacks = new List<AttackSpec>();
        public List<DeathEffectSpec> OnDeath = new List<DeathEffectSpec>();
        public float LootMultiplier = 1f;
        public bool IsBoss;
        public bool HasTag(string t) { foreach (var x in Tags) if (x == t) return true; return false; }
    }

    public sealed class BossPhase
    {
        /// <summary>Phase starts when health fraction drops to or below this (first phase uses 1).</summary>
        public float HealthAtOrBelow = 1f;
        public MovementSpec Movement;
        public List<AttackSpec> Attacks = new List<AttackSpec>();
        public float InvulnerableOnEnter = 1f;  // seconds of invulnerability during the transition
        public List<AttackSpec> OnEnter = new List<AttackSpec>(); // e.g. summon wave, arena hazard
        public string TransitionKey;
    }

    public sealed class BossDefinition
    {
        public string Id;
        public string Name;
        public string Title;
        public string Description;
        public BossTier Tier;
        public EnemyDefinition Body = new EnemyDefinition { IsBoss = true, Health = 200f, KnockbackResistance = 0.9f };
        public List<BossPhase> Phases = new List<BossPhase>();
        public string MusicKey;
        public string IntroKey;
        public string DeathKey;
        /// <summary>Item pool for the reward pedestal ("boss" by default).</summary>
        public string RewardPool = "boss";
        public int RewardMinQuality = 1;
        public string UnlockOnDefeat;
    }
}
