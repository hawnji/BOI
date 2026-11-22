using System;
using System.Collections.Generic;

namespace Hollowcrawl.Combat
{
    /// <summary>How a shot travels. Only one delivery can be active; conflicts are resolved by priority.</summary>
    public enum ProjectileDelivery
    {
        Bolt = 0,       // standard travelling projectile
        Lobbed = 1,     // arcs over obstacles, lands at range
        Beam = 2,       // instant line, pierces by default
        Orbiting = 3,   // circles the player then releases
        Boomerang = 4,  // returns to the shooter
        Wave = 5,       // wide slow crescent that passes through enemies
    }

    public struct StatusApplication
    {
        public string StatusId;
        public float Chance;     // 0..1, before luck scaling
        public float Duration;   // seconds
        public float Potency;    // handler-specific strength (dps, slow %, ...)
    }

    /// <summary>
    /// Fully describes one shot before it is spawned. Built fresh per volley by the interaction
    /// engine's projectile pipeline: base stats from the StatSheet, then every active projectile
    /// modifier in priority order. Pooled; call Reset() before reuse.
    /// </summary>
    public sealed class ProjectileSpec
    {
        public const int MaxGeneration = 3;          // split children of children of children stop here
        public const int MaxShotsPerVolley = 16;

        // Core numbers
        public float Damage;
        public float Speed;
        public float Range;
        public float Size;
        public float Knockback;
        public float CritChance;
        public float CritMultiplier;
        public DamageType Types;
        public Team Team;

        // Volley shape
        public int ShotsPerVolley;
        public float SpreadDegrees;

        // Delivery (exclusive)
        public ProjectileDelivery Delivery;
        public int DeliveryPriority;
        public string DeliverySource;

        // Behaviors (stackable)
        public int PierceCount;          // enemies passed through, -1 = infinite
        public bool Spectral;            // passes obstacles
        public int Bounces;              // wall/enemy bounces
        public float HomingStrength;     // 0 = none, degrees per second turn rate / 360
        public int SplitCount;           // children spawned on hit/expire
        public float SplitDamageScale = 0.5f;
        public float ExplosionRadius;    // 0 = no explosion
        public float ExplosionDamageScale = 1f;
        public int ChainJumps;           // arcs to nearby enemies on hit
        public float ChainRange = 3f;
        public float ChainDamageScale = 0.6f;
        public float ChargeTime;         // >0 = shots are charged; damage scales with charge
        public float AccelerationPerSecond;
        public float SineAmplitude;      // wavy flight

        public int Generation;           // 0 = fired by player, 1+ = spawned by another projectile
        public readonly List<StatusApplication> Statuses = new List<StatusApplication>(4);
        /// <summary>Keys for the presentation layer to pick trails/sprites/sfx (e.g. "fire", "synergy:sunburst").</summary>
        public readonly List<string> VisualKeys = new List<string>(4);
        /// <summary>Ids of modifiers that touched this spec, in order (debugging).</summary>
        public readonly List<string> AppliedModifiers = new List<string>(8);

        public bool IsCharged => ChargeTime > 0f;
        public bool CanSplit => SplitCount > 0 && Generation < MaxGeneration;

        public void Reset()
        {
            Damage = Speed = Range = Size = Knockback = CritChance = 0f;
            CritMultiplier = 2f;
            Types = DamageType.Physical;
            Team = Team.Player;
            ShotsPerVolley = 1;
            SpreadDegrees = 0f;
            Delivery = ProjectileDelivery.Bolt;
            DeliveryPriority = int.MinValue;
            DeliverySource = null;
            PierceCount = 0;
            Spectral = false;
            Bounces = 0;
            HomingStrength = 0f;
            SplitCount = 0;
            SplitDamageScale = 0.5f;
            ExplosionRadius = 0f;
            ExplosionDamageScale = 1f;
            ChainJumps = 0;
            ChainRange = 3f;
            ChainDamageScale = 0.6f;
            ChargeTime = 0f;
            AccelerationPerSecond = 0f;
            SineAmplitude = 0f;
            Generation = 0;
            Statuses.Clear();
            VisualKeys.Clear();
            AppliedModifiers.Clear();
        }

        /// <summary>Requests a delivery. Applied only if its priority beats the current one (ties keep the first).</summary>
        public bool TrySetDelivery(ProjectileDelivery delivery, int priority, string source)
        {
            if (priority <= DeliveryPriority) return false;
            Delivery = delivery;
            DeliveryPriority = priority;
            DeliverySource = source;
            return true;
        }

        public void AddVisual(string key)
        {
            if (!string.IsNullOrEmpty(key) && !VisualKeys.Contains(key)) VisualKeys.Add(key);
        }

        /// <summary>Clamps values to safe ranges so no combination can produce runaway spawning.</summary>
        public void Sanitize()
        {
            if (ShotsPerVolley < 1) ShotsPerVolley = 1;
            if (ShotsPerVolley > MaxShotsPerVolley) ShotsPerVolley = MaxShotsPerVolley;
            if (SplitCount > 8) SplitCount = 8;
            if (ChainJumps > 10) ChainJumps = 10;
            if (Bounces > 12) Bounces = 12;
            if (Damage < 0f) Damage = 0f;
            if (Speed < 0.5f) Speed = 0.5f;
            if (Size < 0.1f) Size = 0.1f;
            if (CritChance > 1f) CritChance = 1f;
            if (Generation > MaxGeneration) SplitCount = 0;
        }

        public void CopyTo(ProjectileSpec other)
        {
            other.Reset();
            other.Damage = Damage; other.Speed = Speed; other.Range = Range; other.Size = Size;
            other.Knockback = Knockback; other.CritChance = CritChance; other.CritMultiplier = CritMultiplier;
            other.Types = Types; other.Team = Team; other.ShotsPerVolley = ShotsPerVolley; other.SpreadDegrees = SpreadDegrees;
            other.Delivery = Delivery; other.DeliveryPriority = DeliveryPriority; other.DeliverySource = DeliverySource;
            other.PierceCount = PierceCount; other.Spectral = Spectral; other.Bounces = Bounces; other.HomingStrength = HomingStrength;
            other.SplitCount = SplitCount; other.SplitDamageScale = SplitDamageScale; other.ExplosionRadius = ExplosionRadius;
            other.ExplosionDamageScale = ExplosionDamageScale; other.ChainJumps = ChainJumps; other.ChainRange = ChainRange;
            other.ChainDamageScale = ChainDamageScale; other.ChargeTime = ChargeTime; other.AccelerationPerSecond = AccelerationPerSecond;
            other.SineAmplitude = SineAmplitude; other.Generation = Generation;
            other.Statuses.AddRange(Statuses); other.VisualKeys.AddRange(VisualKeys); other.AppliedModifiers.AddRange(AppliedModifiers);
        }

        /// <summary>Creates the spec for a split child (one generation deeper, reduced damage, no further volley).</summary>
        public void MakeSplitChild(ProjectileSpec child)
        {
            CopyTo(child);
            child.Generation = Generation + 1;
            child.Damage = Damage * SplitDamageScale;
            child.Size = Size * 0.7f;
            child.ShotsPerVolley = 1;
            child.SpreadDegrees = 0f;
            child.ChargeTime = 0f;
            if (child.Generation >= MaxGeneration) child.SplitCount = 0;
            child.ExplosionRadius *= 0.6f;
        }

        /// <summary>Rolls a critical hit and returns final damage. Luck shifts crit chance slightly.</summary>
        public float RollDamage(Rng.SeededRandom rng, float luck, out bool critical)
        {
            float chance = Math.Max(0f, Math.Min(1f, CritChance + luck * 0.01f));
            critical = rng.Chance(chance);
            return critical ? Damage * CritMultiplier : Damage;
        }
    }
}
