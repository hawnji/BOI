using System;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Economy
{
    /// <summary>
    /// Seeded reward rolls for cleared rooms, enemies, destructible props and chests. Base chances
    /// are data; items modify them through the interaction engine (drop_chance effects) and luck.
    /// </summary>
    public sealed class DropTables
    {
        public float RoomClearChance = 0.45f;
        public float EnemyDropChance = 0.04f;
        public float PropDropChance = 0.25f;
        public float LuckPerPoint = 0.03f;

        private readonly WeightedPicker<PickupKind> _roomTable = new WeightedPicker<PickupKind>();
        private readonly WeightedPicker<PickupKind> _smallTable = new WeightedPicker<PickupKind>();
        private readonly WeightedPicker<PickupKind> _chestTable = new WeightedPicker<PickupKind>();

        public DropTables()
        {
            _roomTable.Add(PickupKind.Coin, 4f);
            _roomTable.Add(PickupKind.VigorHalf, 2f);
            _roomTable.Add(PickupKind.Bomb, 1.5f);
            _roomTable.Add(PickupKind.Key, 1.5f);
            _roomTable.Add(PickupKind.Consumable, 1f);
            _roomTable.Add(PickupKind.Ward, 0.5f);
            _roomTable.Add(PickupKind.Trinket, 0.15f);

            _smallTable.Add(PickupKind.Coin, 6f);
            _smallTable.Add(PickupKind.VigorHalf, 2f);
            _smallTable.Add(PickupKind.Bomb, 1f);
            _smallTable.Add(PickupKind.Key, 1f);

            _chestTable.Add(PickupKind.Coin, 3f);
            _chestTable.Add(PickupKind.Consumable, 2f);
            _chestTable.Add(PickupKind.Trinket, 1f);
            _chestTable.Add(PickupKind.Item, 0.35f);
            _chestTable.Add(PickupKind.Ward, 1f);
        }

        /// <summary>Reward for clearing a room, or null for none.</summary>
        public PickupKind? RollRoomClear(SeededRandom rng, InteractionEngine engine)
        {
            float luck = engine?.Stats[Stats.StatId.Luck] ?? 0f;
            float chance = RoomClearChance + luck * LuckPerPoint;
            if (!rng.Chance(Math.Max(0.05f, Math.Min(0.95f, chance)))) return null;
            var kind = _roomTable.Pick(rng);
            if (engine != null && !rng.Chance(Math.Min(1f, engine.GetDropChance(kind, 1f)))) return PickupKind.Coin;
            return kind;
        }

        public PickupKind? RollEnemyDrop(SeededRandom rng, InteractionEngine engine, bool elite)
        {
            float luck = engine?.Stats[Stats.StatId.Luck] ?? 0f;
            float chance = (elite ? EnemyDropChance * 6f : EnemyDropChance) + luck * LuckPerPoint * 0.25f;
            var kind = _smallTable.Pick(rng);
            if (engine != null) chance = engine.GetDropChance(kind, chance);
            return rng.Chance(chance) ? kind : (PickupKind?)null;
        }

        public PickupKind? RollProp(SeededRandom rng, InteractionEngine engine)
        {
            float chance = PropDropChance;
            var kind = _smallTable.Pick(rng);
            if (engine != null) chance = engine.GetDropChance(kind, chance);
            return rng.Chance(chance) ? kind : (PickupKind?)null;
        }

        public PickupKind RollChest(SeededRandom rng) => _chestTable.Pick(rng);
    }
}
