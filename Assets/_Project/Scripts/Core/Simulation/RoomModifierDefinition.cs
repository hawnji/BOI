namespace Hollowcrawl.Simulation
{
    /// <summary>
    /// A rule that can be rolled onto a combat room the first time it is entered (chance from the difficulty's
    /// RoomModifierChance). Data lives in Content/Rooms/room_modifiers.json.
    /// </summary>
    public sealed class RoomModifierDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public float Weight = 1f;
        /// <summary>0-based floor index from which this modifier can appear.</summary>
        public int MinFloor;

        public float EnemyHealthMultiplier = 1f;
        public float EnemySpeedMultiplier = 1f;
        /// <summary>Multiplier on damage enemies take from the player (below 1 = armoured).</summary>
        public float EnemyDamageTakenMultiplier = 1f;
        public bool AllElite;
        /// <summary>Fraction of the room's spawns that are duplicated next to the original (0.5 = half again).</summary>
        public float ExtraEnemies;
        /// <summary>Enemies burst when they die (enemy-team explosion that hurts the player).</summary>
        public bool ExplodeOnDeath;
        public float DeathExplosionRadius = 1.2f;
        /// <summary>Fraction of max health regenerated per second by living enemies.</summary>
        public float EnemyRegenPerSecond;
        /// <summary>Above 0: the room is dark beyond this radius around the player (presentation reads it).</summary>
        public float VisionRadius;
        /// <summary>Extra pickups dropped at the room centre when the room is cleared.</summary>
        public int RewardPickups;
        /// <summary>Drop a chest-quality roll (instead of a plain pickup) for each reward.</summary>
        public bool RewardChest;
    }
}
