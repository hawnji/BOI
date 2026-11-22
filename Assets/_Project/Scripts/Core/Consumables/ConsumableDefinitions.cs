using System;
using System.Collections.Generic;
using Hollowcrawl.Economy;
using Hollowcrawl.Items;

namespace Hollowcrawl.Consumables
{
    /// <summary>
    /// Sigils are etched tablets whose effect is printed on them. Draughts are unlabeled flasks: their
    /// colours are shuffled every run and a draught is only identified once drunk.
    /// </summary>
    public enum ConsumableKind { Sigil = 0, Draught = 1 }

    public sealed class ConsumableDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public ConsumableKind Kind;
        public string IconKey;
        public float Weight = 1f;
        /// <summary>Bad draughts exist so drinking unknown ones is a gamble.</summary>
        public bool Harmful;
        public int MinFloor;
        public string UnlockId;
        /// <summary>Effects run once on use. Every entry must be an active effect (IActiveEffect).</summary>
        public List<EffectSpec> OnUse = new List<EffectSpec>();
    }

    /// <summary>
    /// A floor hex (curse). Behaviour comes from flags the run and HUD read plus ordinary effects that are
    /// attached as a Curse source for the floor.
    /// </summary>
    public sealed class HexDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public float Weight = 1f;
        public int MinFloor;
        public bool HideMap;            // minimap shows only visited rooms
        public bool HideRoomIcons;      // special-room icons hidden until visited
        public float FloorSizeMultiplier = 1f;
        public bool VeilPedestals;      // pedestal items show as "?" until picked up
        public float VisionRadius;      // > 0: darkness outside this radius (presentation)
        public List<EffectSpec> Effects = new List<EffectSpec>();
    }

    public enum ShrineCostKind { None = 0, Coins = 1, Health = 2, Keys = 3, Bombs = 4 }

    public enum ShrineOutcomeKind
    {
        Nothing = 0, Pickup = 1, Item = 2, Heal = 3, Damage = 4, Hex = 5, Consumable = 6, Stat = 7, Enemies = 8, Coins = 9,
    }

    public sealed class ShrineOutcome
    {
        public ShrineOutcomeKind Kind;
        public float Weight = 1f;
        public PickupKind Pickup = PickupKind.Coin;
        public int Count = 1;
        public string Pool;          // item pool for Item outcomes
        public string ContentId;     // enemy/consumable/hex id
        public EffectSpec Effect;    // Stat outcomes: a passive effect attached permanently
        public string Message;
    }

    public sealed class ShrineOption
    {
        public string Label;
        public ShrineCostKind Cost;
        public int Amount;
        public int MaxUses = 1;
        /// <summary>Each use adds this to the chance weight of the first outcome (e.g. "breaks more often").</summary>
        public float FirstOutcomeWeightPerUse;
        public List<ShrineOutcome> Outcomes = new List<ShrineOutcome>();
    }

    /// <summary>An event-room fixture offering risk/reward choices.</summary>
    public sealed class ShrineDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string SpriteKey;
        public float Weight = 1f;
        public int MinFloor;
        public List<ShrineOption> Options = new List<ShrineOption>();
    }
}
