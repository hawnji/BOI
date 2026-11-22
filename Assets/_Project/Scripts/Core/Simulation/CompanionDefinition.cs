using Hollowcrawl.Items;

namespace Hollowcrawl.Simulation
{
    public enum CompanionBehavior
    {
        Shooter = 0,    // trails the player and shoots the nearest enemy
        Mimic = 1,      // fires in the player's aim direction whenever the player fires
        Orbital = 2,    // circles the player, hurts enemies it touches, blocks enemy shots
        Chaser = 3,     // hunts the nearest enemy and deals contact damage
        Collector = 4,  // fetches loose pickups (not shop goods or pedestals)
        Bomber = 5,     // drops player-safe bombs near enemies
        Provider = 6,   // spawns a pickup every few cleared rooms
        Guardian = 7,   // stays between the player and the nearest enemy shot, blocking it
    }

    /// <summary>A companion type referenced by the "companion" effect's `companion` id.</summary>
    public sealed class CompanionDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string SpriteKey;
        public CompanionBehavior Behavior = CompanionBehavior.Shooter;
        public float Radius = 0.25f;
        public float Speed = 6f;
        public float FireInterval = 1.2f;
        /// <summary>Multiplier on the player's damage for companion shots.</summary>
        public float DamageScale = 0.6f;
        public float Range = 8f;
        /// <summary>Damage per second for Orbital/Chaser contact.</summary>
        public float ContactDps = 12f;
        public float OrbitRadius = 1.4f;
        public float OrbitSpeed = 120f;
        public bool BlocksShots;
        public PickupKind ProvidesPickup = PickupKind.Coin;
        public int ProvideEveryRooms = 3;
        public string ShotStatus;
        public float ShotStatusChance;
        public float BombDamage = 25f;
        public float BombRadius = 1.4f;
    }
}
