using System;
using System.Collections.Generic;
using Hollowcrawl.Content;

namespace Hollowcrawl.Items
{
    public enum ItemKind { Passive = 0, Active = 1, Trinket = 2, Special = 3 }

    public enum ItemRarity { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3, Cursed = 4 }

    public enum ItemStacking
    {
        /// <summary>Only one copy can be held; duplicates are refused by item pools.</summary>
        Unique = 0,
        /// <summary>Copies stack without limit (effects receive the stack count).</summary>
        Stack = 1,
        /// <summary>Copies stack up to MaxStacks.</summary>
        StackCapped = 2,
    }

    public enum ActiveChargeType
    {
        Rooms = 0,     // +1 per cleared room
        Seconds = 1,   // recharges over time
        Kills = 2,     // +1 per kill
        OneShot = 3,   // consumed on use
    }

    /// <summary>One effect entry in a content file: a handler type key plus parameters.</summary>
    public sealed class EffectSpec
    {
        public string Type;
        public ParamBag Params = new ParamBag();
        /// <summary>Optional condition expression (see ConditionRegistry), e.g. "health_full".</summary>
        public string Condition;
        /// <summary>Optional explicit id so synergies can suppress this specific effect ("itemId/effectId").</summary>
        public string Id;

        public EffectSpec() { }
        public EffectSpec(string type, ParamBag p = null, string condition = null, string id = null)
        {
            Type = type;
            Params = p ?? new ParamBag();
            Condition = condition;
            Id = id;
        }
    }

    public sealed class ActiveItemSpec
    {
        public ActiveChargeType ChargeType = ActiveChargeType.Rooms;
        public int MaxCharge = 3;            // rooms / kills, or seconds for timed
        public bool StartsCharged = true;
        /// <summary>Effects executed when the active is used (must implement IActiveEffect).</summary>
        public List<EffectSpec> OnUse = new List<EffectSpec>();
    }

    public sealed class ItemDefinition
    {
        public string Id;
        public string Name;
        public string Description;   // short pickup text
        public string LongDescription;
        public string IconKey;
        public ItemKind Kind;
        public ItemRarity Rarity;
        /// <summary>0..4, used to weight pools and for quality-gated rewards.</summary>
        public int Quality = 1;
        public string[] Pools = Array.Empty<string>();
        public string[] Tags = Array.Empty<string>();
        public ItemStacking Stacking = ItemStacking.Unique;
        public int MaxStacks = 1;
        public List<EffectSpec> Effects = new List<EffectSpec>();
        public ActiveItemSpec Active;
        public string UnlockId;
        public int ShopPrice = 15;

        public bool HasTag(string tag)
        {
            for (int i = 0; i < Tags.Length; i++) if (string.Equals(Tags[i], tag, StringComparison.Ordinal)) return true;
            return false;
        }

        public override string ToString() => Id;
    }

    /// <summary>A requirement line: an exact item id or "any item with tag", with a count.</summary>
    public sealed class Requirement
    {
        public string ItemId;
        public string Tag;
        public int Count = 1;

        public static Requirement ForItem(string id) => new Requirement { ItemId = id };
        public static Requirement ForTag(string tag, int count) => new Requirement { Tag = tag, Count = count };

        public override string ToString() => ItemId != null ? ItemId : ("tag:" + Tag + "x" + Count);
    }

    /// <summary>
    /// A combination of items (and optional condition) that grants extra effects and can suppress or
    /// replace effects of its ingredients. Example: fire + piercing -> "Sunlance" beam.
    /// </summary>
    public sealed class SynergyDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Hidden synergies are not shown in the codex until discovered.</summary>
        public bool Hidden;
        /// <summary>Higher priority is evaluated first and wins exclusive groups.</summary>
        public int Priority;
        /// <summary>Only the highest-priority active synergy of a group is applied (null = no group).</summary>
        public string ExclusiveGroup;
        public List<Requirement> Requires = new List<Requirement>();
        public string Condition;
        public List<EffectSpec> Effects = new List<EffectSpec>();
        /// <summary>Effect references to disable while active: "itemId/effectType" or "itemId/effectId" or "*/effectType".</summary>
        public string[] Suppresses = Array.Empty<string>();
        public string VisualKey;
        public string AudioKey;
    }

    /// <summary>
    /// A transformation: triggered by collecting enough items of a set during the run (items do not
    /// have to still be held). Once triggered it lasts the rest of the run unless replaced.
    /// </summary>
    public sealed class TransformationDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public List<Requirement> Requires = new List<Requirement>();
        public string Condition;
        public List<EffectSpec> Effects = new List<EffectSpec>();
        /// <summary>Transformations sharing a group replace each other: the newest one wins.</summary>
        public string ExclusiveGroup;
        public string VisualKey;
        public string AudioKey;
    }
}
