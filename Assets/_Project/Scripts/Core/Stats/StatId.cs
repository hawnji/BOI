namespace Hollowcrawl.Stats
{
    /// <summary>All player stats. Values are persisted by name in content files, so append only.</summary>
    public enum StatId
    {
        MaxHealth = 0,      // number of Vigor containers (each container = 2 half points)
        Damage = 1,
        FireRate = 2,       // shots per second
        ShotSpeed = 3,      // world units per second multiplier base
        Range = 4,          // world units travelled before a shot expires
        MoveSpeed = 5,
        Luck = 6,
        CritChance = 7,     // 0..1
        CritDamage = 8,     // multiplier, e.g. 2 = double damage
        ShotSize = 9,       // scale multiplier
        Knockback = 10,
        Defense = 11,       // flat damage reduction in half points, applied before minimum-damage rule
        BombDamage = 12,
        ConsumableEffect = 13, // multiplier for consumable potency
        Count = 14
    }

    public readonly struct StatLimits
    {
        public readonly float Base;
        public readonly float Min;
        public readonly float Max;
        public StatLimits(float b, float min, float max) { Base = b; Min = min; Max = max; }
    }

    /// <summary>Default base values and global hard caps. Characters override bases via data.</summary>
    public static class StatTable
    {
        private static readonly StatLimits[] Limits =
        {
            new StatLimits(3f, 1f, 12f),        // MaxHealth (containers)
            new StatLimits(3.5f, 0.5f, 999f),   // Damage
            new StatLimits(2.7f, 0.4f, 30f),    // FireRate
            new StatLimits(1f, 0.6f, 4f),       // ShotSpeed
            new StatLimits(6.5f, 1.5f, 30f),    // Range
            new StatLimits(1f, 0.4f, 2.2f),     // MoveSpeed
            new StatLimits(0f, -10f, 20f),      // Luck
            new StatLimits(0.03f, 0f, 1f),      // CritChance
            new StatLimits(2f, 1f, 10f),        // CritDamage
            new StatLimits(1f, 0.3f, 4f),       // ShotSize
            new StatLimits(1f, 0f, 10f),        // Knockback
            new StatLimits(0f, 0f, 6f),         // Defense
            new StatLimits(10f, 1f, 500f),      // BombDamage
            new StatLimits(1f, 0.25f, 5f),      // ConsumableEffect
        };

        public static StatLimits Get(StatId id) => Limits[(int)id];
    }
}
