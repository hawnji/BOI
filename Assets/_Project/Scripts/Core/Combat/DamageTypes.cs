using System;

namespace Hollowcrawl.Combat
{
    [Flags]
    public enum DamageType
    {
        None = 0,
        Physical = 1 << 0,
        Fire = 1 << 1,
        Poison = 1 << 2,
        Frost = 1 << 3,
        Shock = 1 << 4,
        Void = 1 << 5,     // ignores enemy armor
        Explosive = 1 << 6,
        Holy = 1 << 7,     // bonus vs. umbral enemies
        Blight = 1 << 8,   // original element: spreads rot between nearby enemies
    }

    public enum Team { Player = 0, Enemy = 1, Neutral = 2 }

    /// <summary>A single damage application, passed through the interaction engine before being applied.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Types;
        public Team SourceTeam;
        public bool IsCritical;
        public float Knockback;
        /// <summary>Direction the knockback pushes the target (unnormalized allowed).</summary>
        public float DirX, DirY;
        /// <summary>Opaque id of the attacker (entity id), 0 = environment.</summary>
        public int SourceId;
        /// <summary>True for damage-over-time ticks; DoT does not trigger on-hit effects.</summary>
        public bool IsDamageOverTime;
        /// <summary>Set by effects to stop the damage entirely (e.g. a shield proc).</summary>
        public bool Cancelled;

        public static DamageInfo Simple(float amount, DamageType type, Team team)
            => new DamageInfo { Amount = amount, Types = type, SourceTeam = team };
    }
}
