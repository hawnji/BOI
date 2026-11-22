using System;
using System.Collections.Generic;
using Hollowcrawl.Content;
using Hollowcrawl.Enemies;
using Hollowcrawl.Rng;
using Hollowcrawl.Save;
using Hollowcrawl.Simulation;

namespace Hollowcrawl.Progression
{
    /// <summary>Builds a <see cref="RunSetup"/> for each game mode from content and the player's profile.</summary>
    public static class RunSetupFactory
    {
        public const string DefaultCharacter = "wanderer";
        public const string DailyCharacter = "wanderer";

        public static DifficultyDefinition Difficulty(string id)
        {
            switch (id)
            {
                case "story": return DifficultyDefinition.Story();
                case "hard": return DifficultyDefinition.Hard();
                case "nightmare": return DifficultyDefinition.Nightmare();
                default: return DifficultyDefinition.Normal();
            }
        }

        public static bool IsCharacterAvailable(CharacterDefinition c, ProfileData profile) => c != null && (profile == null || profile.IsUnlocked(c.UnlockId));

        public static RunSetup Standard(ContentDatabase content, ProfileData profile, string characterId, RunSeed seed, string difficultyId = "normal")
        {
            var setup = Base(content, profile, characterId, seed);
            setup.Mode = GameMode.Standard;
            setup.Difficulty = Difficulty(difficultyId);
            return setup;
        }

        /// <summary>Same seed, character and rules for everyone on a given UTC date. Progression is recorded.</summary>
        public static RunSetup Daily(ContentDatabase content, ProfileData profile, DateTime utcDate)
        {
            var setup = Base(content, null, DailyCharacter, RunSeed.ForDate(utcDate));
            setup.Mode = GameMode.Daily;
            setup.IsDaily = true;
            setup.IsUnlocked = _ => true; // dailies ignore personal unlock state so every player gets the same pools
            return setup;
        }

        /// <summary>A chosen seed and character with no progression (used to learn or test).</summary>
        public static RunSetup Practice(ContentDatabase content, ProfileData profile, string characterId, RunSeed seed, string difficultyId = "normal")
        {
            var setup = Standard(content, profile, characterId, seed, difficultyId);
            setup.Mode = GameMode.Practice;
            return setup;
        }

        /// <summary>A typed-in seed. Recorded in statistics but cannot earn achievements.</summary>
        public static RunSetup Custom(ContentDatabase content, ProfileData profile, string characterId, RunSeed seed, string difficultyId = "normal")
        {
            var setup = Standard(content, profile, characterId, seed, difficultyId);
            setup.Mode = GameMode.Custom;
            return setup;
        }

        public static RunSetup Challenge(ContentDatabase content, ProfileData profile, string challengeId, RunSeed seed)
        {
            if (!content.Challenges.TryGetValue(challengeId, out var ch)) throw new ArgumentException("Unknown challenge " + challengeId);
            var setup = Base(content, profile, ch.CharacterId, seed);
            setup.Mode = GameMode.Challenge;
            setup.ChallengeId = ch.Id;
            setup.Difficulty = Difficulty(ch.DifficultyId);
            setup.StartingItems.AddRange(ch.StartingItems);
            if (ch.FloorCount > 0) setup.FloorCount = ch.FloorCount;
            if (ch.Goal == ChallengeGoal.ReachFloor) setup.GoalFloor = ch.GoalFloor;
            setup.NoTreasureRooms = ch.NoTreasureRooms;
            setup.NoShops = ch.NoShops;
            setup.NoHealthPickups = ch.NoHealthPickups;
            setup.HexChanceOverride = ch.HexChance;
            setup.ForcedHex = ch.ForcedHex;
            if (ch.StartingCoins >= 0) setup.StartingCoins = ch.StartingCoins;
            if (ch.StartingBombs >= 0) setup.StartingBombs = ch.StartingBombs;
            if (ch.StartingKeys >= 0) setup.StartingKeys = ch.StartingKeys;
            setup.ChallengeEffects.AddRange(ch.Effects);
            return setup;
        }

        private static RunSetup Base(ContentDatabase content, ProfileData profile, string characterId, RunSeed seed)
        {
            if (characterId == null || !content.Characters.TryGetValue(characterId, out var c))
                content.Characters.TryGetValue(DefaultCharacter, out c);
            var setup = new RunSetup { Seed = seed };
            if (profile != null) setup.IsUnlocked = profile.IsUnlocked;
            if (c == null) return setup; // content without characters (tests) keeps defaults
            setup.CharacterId = c.Id;
            setup.StartingContainers = c.NoContainers ? 1 : c.Containers;
            setup.NoContainers = c.NoContainers;
            setup.StartingWard = c.Ward;
            setup.StartingUmbral = c.Umbral;
            setup.StartingCoins = c.Coins;
            setup.StartingBombs = c.Bombs;
            setup.StartingKeys = c.Keys;
            setup.StartingItems = new List<string>(c.StartingItems);
            setup.StartingConsumable = c.StartingConsumable;
            setup.CharacterEffects = new List<Items.EffectSpec>(c.Effects);
            setup.ConsumableSlots = c.ConsumableSlots;
            setup.TrinketSlots = c.TrinketSlots;
            return setup;
        }
    }
}
