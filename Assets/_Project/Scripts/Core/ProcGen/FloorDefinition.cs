using System;
using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    /// <summary>An enemy that may appear on a floor, with its population cost.</summary>
    public sealed class EnemyPoolEntry
    {
        public string EnemyId;
        public int Cost = 1;           // budget points
        public float Weight = 1f;
        public string[] Tags = Array.Empty<string>(); // e.g. "flying", "ranged", "melee", "summoner"
        public int MaxPerRoom = 6;
        public bool HasTag(string tag) { foreach (var t in Tags) if (t == tag) return true; return false; }
    }

    public sealed class WeightedId
    {
        public string Id;
        public float Weight = 1f;
        public WeightedId() { }
        public WeightedId(string id, float weight = 1f) { Id = id; Weight = weight; }
    }

    /// <summary>Chance and limits for an optional special room on a floor.</summary>
    public sealed class SpecialRoomRule
    {
        public RoomType Type;
        public float Chance = 1f;
        public bool Locked;           // requires a key
        public int MinFloor = 0;
        public int MaxCount = 1;
    }

    /// <summary>Data for one floor environment ("Ashen Crypt", "Drowned Galleries", ...).</summary>
    public sealed class FloorDefinition
    {
        public string Id;
        public string Name;
        /// <summary>Theme id used to filter room templates (templates may also use "*").</summary>
        public string Theme;
        /// <summary>1-based depth this environment appears at (used for scaling).</summary>
        public int Depth = 1;
        public int MinRooms = 8;
        public int MaxRooms = 12;
        public int MinTemplateDifficulty = 1;
        public int MaxTemplateDifficulty = 4;
        /// <summary>Enemy budget per combat room before template difficulty scaling.</summary>
        public int BaseEnemyBudget = 6;
        public float LargeRoomChance = 0.15f;
        public float LoopChance = 0.08f;
        public List<SpecialRoomRule> SpecialRooms = new List<SpecialRoomRule>();
        public List<EnemyPoolEntry> Enemies = new List<EnemyPoolEntry>();
        public List<WeightedId> Bosses = new List<WeightedId>();
        public List<WeightedId> Minibosses = new List<WeightedId>();
        public float SecretRoomChance = 1f;
        public float HiddenVaultChance = 0.5f;
        public float EliteChance = 0.08f;
        public string MusicKey;
        public string AmbienceKey;
        public string[] Hazards = Array.Empty<string>();
    }
}
