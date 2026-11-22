using System;
using System.Collections.Generic;
using Hollowcrawl.Items;

namespace Hollowcrawl.Progression
{
    public sealed class CharacterDefinition
    {
        public string Id;
        public string Name;
        public string Title;
        public string Description;
        /// <summary>One line about what makes the character play differently.</summary>
        public string Trait;
        public string SpriteKey;
        /// <summary>Unlock id required to pick this character (null = available from the start).</summary>
        public string UnlockId;
        public string UnlockHint;
        public int Containers = 3;
        public int Ward;
        public int Umbral;
        /// <summary>Never has Vigor containers: container gains become Umbral, Vigor healing does nothing.</summary>
        public bool NoContainers;
        public int Coins, Bombs = 1, Keys;
        public int ConsumableSlots = 1;
        public int TrinketSlots = 1;
        public List<string> StartingItems = new List<string>();
        public string StartingConsumable;
        /// <summary>Permanent character effects (stat offsets, traits) attached as a Character source.</summary>
        public List<EffectSpec> Effects = new List<EffectSpec>();
    }

    public enum AchievementTrigger
    {
        Counter = 0,            // lifetime counter `key` >= target
        BossDefeated = 1,       // boss `key` defeated (any character)
        WinWithCharacter = 2,   // win a run as `key`
        ReachFloor = 3,         // reach floor index target (1-based)
        Transformation = 4,     // trigger transformation `key`
        ChallengeComplete = 5,  // complete challenge `key`
        DiscoverItems = 6,      // discovered item count >= target
        DiscoverSynergies = 7,  // discovered synergy count >= target
        RunCondition = 8,       // a per-run flag `key` was true in a winning run (e.g. "flawless", "no_items", "speed")
        AllCharacterWins = 9,   // won with every character
    }

    public sealed class AchievementDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public bool Hidden;
        public AchievementTrigger Trigger;
        public string Key;
        public long Target = 1;
        /// <summary>Unlock ids granted (characters, items, challenges use these in their unlockId).</summary>
        public List<string> Unlocks = new List<string>();
        public string IconKey;
    }

    public enum ChallengeGoal { DefeatFinalBoss = 0, ReachFloor = 1 }

    public sealed class ChallengeDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string UnlockId;
        public string CharacterId = "wanderer";
        public List<string> StartingItems = new List<string>();
        public string DifficultyId = "normal";
        public int FloorCount;              // 0 = default run length
        public ChallengeGoal Goal = ChallengeGoal.DefeatFinalBoss;
        public int GoalFloor;
        public bool NoTreasureRooms;
        public bool NoShops;
        public bool NoHealthPickups;
        public float HexChance = -1f;      // >= 0 overrides the difficulty's chance
        public string ForcedHex;
        public int StartingCoins = -1, StartingBombs = -1, StartingKeys = -1;
        public List<EffectSpec> Effects = new List<EffectSpec>();
        public string RewardUnlock;
    }

    public enum GameMode { Standard = 0, Challenge = 1, Daily = 2, Practice = 3, Custom = 4 }
}
