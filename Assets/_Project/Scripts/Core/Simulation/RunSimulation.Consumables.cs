using System;
using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Consumables;
using Hollowcrawl.Economy;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Simulation
{
    /// <summary>Consumables (sigils and draughts), floor hexes and shrine events.</summary>
    public sealed partial class RunSimulation
    {
        public static readonly string[] DraughtColours = { "amber", "ash", "azure", "bone", "crimson", "jade", "ochre", "pearl", "plum", "rust", "slate", "teal", "umber", "violet" };

        /// <summary>Held consumables; slot 0 is used first.</summary>
        public readonly List<string> ConsumableSlots = new List<string>();
        public int ConsumableCapacity = 1;
        public readonly HashSet<string> IdentifiedDraughts = new HashSet<string>(StringComparer.Ordinal);
        public HexDefinition ActiveHex { get; private set; }

        private Dictionary<string, string> _draughtColours;
        private readonly Dictionary<string, object> _consumableOwners = new Dictionary<string, object>(StringComparer.Ordinal);
        private SeededRandom _consumableRng;
        private SeededRandom _hexRng;
        private SeededRandom _shrineRng;

        private void InitConsumables(SeededRandom root)
        {
            _consumableRng = root.Fork("consumables");
            _hexRng = root.Fork("hexes");
            _shrineRng = root.Fork("shrines");
            // shuffle draught appearances for this run
            var draughts = Content.Consumables.Values.Where(c => c.Kind == ConsumableKind.Draught).Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var colours = DraughtColours.ToList();
            _consumableRng.Shuffle(colours);
            _draughtColours = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < draughts.Count; i++) _draughtColours[draughts[i]] = colours[i % colours.Count] + (i >= colours.Count ? " " + (i / colours.Count + 1) : "");
        }

        public bool HasConsumableContent => Content.Consumables.Count > 0;

        /// <summary>Name as the player currently knows it ("Amber draught" until identified).</summary>
        public string ConsumableDisplayName(string id)
        {
            if (id == null || !Content.Consumables.TryGetValue(id, out var def)) return "?";
            if (def.Kind == ConsumableKind.Draught && !IdentifiedDraughts.Contains(id))
                return char.ToUpperInvariant(DraughtColour(id)[0]) + DraughtColour(id).Substring(1) + " draught";
            return def.Name;
        }

        public string DraughtColour(string id) => _draughtColours != null && _draughtColours.TryGetValue(id, out var c) ? c : "murky";

        public ConsumableDefinition DrawConsumable()
        {
            var picker = new WeightedPicker<ConsumableDefinition>();
            foreach (var c in Content.Consumables.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
                if (c.MinFloor <= FloorIndex && (c.UnlockId == null || Setup.IsUnlocked(c.UnlockId))) picker.Add(c, c.Weight);
            return picker.TryPick(_consumableRng, out var pick) ? pick : null;
        }

        private bool GrantConsumable(PickupActor p)
        {
            if (p.ContentId == null) p.ContentId = DrawConsumable()?.Id;
            if (p.ContentId == null) { Engine.Resources.Add(ResourceKind.Coins, 1); return true; }
            if (ConsumableSlots.Count >= Math.Max(1, ConsumableCapacity))
            {
                var dropped = ConsumableSlots[ConsumableSlots.Count - 1];
                ConsumableSlots.RemoveAt(ConsumableSlots.Count - 1);
                var d = CurrentRoom.SpawnPickupActor(PickupKind.Consumable, p.Position + new Vec2(0f, -0.9f), dropped);
                if (d != null) d.SpawnProtection = 1.5f;
            }
            ConsumableSlots.Insert(0, p.ContentId);
            return true;
        }

        private void HandleConsumableInput(in PlayerInputFrame input)
        {
            if (input.SwapConsumablePressed && ConsumableSlots.Count > 1)
            {
                var first = ConsumableSlots[0];
                ConsumableSlots.RemoveAt(0);
                ConsumableSlots.Add(first);
            }
            if (input.ConsumablePressed) UseConsumable();
        }

        /// <summary>Uses the consumable in slot 0. It is spent even if its effect had nothing to act on.</summary>
        public bool UseConsumable()
        {
            if (ConsumableSlots.Count == 0) return false;
            var id = ConsumableSlots[0];
            ConsumableSlots.RemoveAt(0);
            Stats.ConsumablesUsed++;
            if (!Content.Consumables.TryGetValue(id, out var def)) return false;
            if (!_consumableOwners.TryGetValue(id, out var owner)) _consumableOwners[id] = owner = new object();
            bool wasKnown = def.Kind != ConsumableKind.Draught || IdentifiedDraughts.Contains(id);
            Engine.UseStandalone(owner, def.Id, def.OnUse, Player.Position, Player.Aim);
            if (def.Kind == ConsumableKind.Draught) IdentifiedDraughts.Add(id);
            Events.Add(new SimEvent { Kind = SimEventKind.ConsumableUsed, Key = def.Id, Position = Player.Position, Flag = def.Harmful });
            if (!wasKnown) Events.Add(new SimEvent { Kind = SimEventKind.ConsumableIdentified, Key = def.Id, Position = Player.Position });
            return true;
        }

        // ------------------------------------------------------------------ hexes

        /// <summary>Rolls this floor's hex and returns the floor definition to generate (hexes may enlarge it).</summary>
        private FloorDefinition PrepareHex(FloorDefinition floor, int index)
        {
            if (ActiveHex != null) { Engine.DetachSource(ActiveHex); ActiveHex = null; }
            HexDefinition hex = null;
            if (Setup.ForcedHex != null) Content.Hexes.TryGetValue(Setup.ForcedHex, out hex);
            if (hex == null)
            {
                float chance = Setup.HexChanceOverride >= 0f ? Setup.HexChanceOverride : Setup.Difficulty.CurseChance;
                if (index == 0 || Content.Hexes.Count == 0 || !_hexRng.Chance(chance)) return floor;
                var picker = new WeightedPicker<HexDefinition>();
                foreach (var h in Content.Hexes.Values.OrderBy(h => h.Id, StringComparer.Ordinal)) if (h.MinFloor <= index) picker.Add(h, h.Weight);
                if (!picker.TryPick(_hexRng, out hex)) return floor;
            }
            ApplyHex(hex);
            if (hex.FloorSizeMultiplier <= 1f) return floor;
            var bigger = floor.Clone();
            bigger.MinRooms = (int)Math.Round(floor.MinRooms * hex.FloorSizeMultiplier);
            bigger.MaxRooms = Math.Max(bigger.MinRooms, (int)Math.Round(floor.MaxRooms * hex.FloorSizeMultiplier));
            return bigger;
        }

        public void ApplyHex(HexDefinition hex)
        {
            if (ActiveHex != null) Engine.DetachSource(ActiveHex);
            ActiveHex = hex;
            Engine.AttachSource(hex, hex.Id, EffectSourceKind.Curse, hex.Effects);
            Events.Add(new SimEvent { Kind = SimEventKind.HexApplied, Key = hex.Id });
        }

        public bool MapHidden => ActiveHex != null && ActiveHex.HideMap;

        // ------------------------------------------------------------------ shrines

        private void PopulateShrine(RoomSimulation sim, List<(int x, int y)> slots)
        {
            var picker = new WeightedPicker<ShrineDefinition>();
            foreach (var s in Content.Shrines.Values.OrderBy(s => s.Id, StringComparer.Ordinal)) if (s.MinFloor <= FloorIndex) picker.Add(s, s.Weight);
            if (!picker.TryPick(_shrineRng, out var def)) return;
            for (int i = 0; i < def.Options.Count; i++)
            {
                var pos = i < slots.Count ? sim.Grid.TileCenter(slots[i].x, slots[i].y) : sim.Grid.Center + new Vec2((i - (def.Options.Count - 1) * 0.5f) * 2.5f, 0f);
                sim.SpawnShrine(def, i, pos);
            }
        }

        public void UseShrine(ShrineActor shrine)
        {
            var o = shrine.Option;
            if (!PayShrineCost(o)) { Events.Add(new SimEvent { Kind = SimEventKind.PurchaseFailed, ActorId = shrine.Id, Position = shrine.Position, Value = o.Amount, Key = o.Cost.ToString() }); return; }
            shrine.Uses++;
            Stats.ShrinesUsed++;
            var picker = new WeightedPicker<ShrineOutcome>();
            for (int i = 0; i < o.Outcomes.Count; i++)
                picker.Add(o.Outcomes[i], o.Outcomes[i].Weight + (i == 0 ? o.FirstOutcomeWeightPerUse * (shrine.Uses - 1) : 0f));
            picker.TryPick(_shrineRng, out var outcome);
            ApplyShrineOutcome(shrine, outcome);
            Events.Add(new SimEvent { Kind = SimEventKind.ShrineUsed, ActorId = shrine.Id, Position = shrine.Position, Key = outcome?.Message ?? outcome?.Kind.ToString() });
            if (shrine.Uses >= o.MaxUses) shrine.Removed = true;
        }

        private bool PayShrineCost(ShrineOption o)
        {
            var res = Engine.Resources;
            switch (o.Cost)
            {
                case ShrineCostKind.None: return true;
                case ShrineCostKind.Coins: return res.TrySpend(ResourceKind.Coins, o.Amount);
                case ShrineCostKind.Keys: return res.TrySpend(ResourceKind.Keys, o.Amount);
                case ShrineCostKind.Bombs: return res.TrySpend(ResourceKind.Bombs, o.Amount);
                case ShrineCostKind.Health:
                    var h = Engine.Health;
                    if (h.TotalHalves <= o.Amount) return false; // shrines never take your last half point
                    h.TakeDamage(o.Amount, 0f, null, ignoreInvulnerability: true);
                    Engine.NotifyPlayerDamaged(o.Amount);
                    return true;
                default: return false;
            }
        }

        private void ApplyShrineOutcome(ShrineActor shrine, ShrineOutcome oc)
        {
            if (oc == null) return;
            var sim = CurrentRoom;
            var at = shrine.Position + new Vec2(0f, -1.1f);
            switch (oc.Kind)
            {
                case ShrineOutcomeKind.Pickup:
                    for (int i = 0; i < Math.Max(1, oc.Count); i++) sim.SpawnPickupActor(ResolvePickupKind(oc.Pickup.ToString()), at);
                    break;
                case ShrineOutcomeKind.Coins:
                    Engine.Resources.Add(ResourceKind.Coins, oc.Count);
                    break;
                case ShrineOutcomeKind.Item:
                    PlacePedestal(sim, at, oc.Pool ?? "treasure");
                    break;
                case ShrineOutcomeKind.Heal:
                    Engine.Health.Heal(Math.Max(1, oc.Count));
                    break;
                case ShrineOutcomeKind.Damage:
                {
                    int halves = Math.Min(Math.Max(1, oc.Count), Math.Max(0, Engine.Health.TotalHalves - 1));
                    if (halves > 0) { Engine.Health.TakeDamage(halves, 0f, null, ignoreInvulnerability: true); Engine.NotifyPlayerDamaged(halves); }
                    break;
                }
                case ShrineOutcomeKind.Hex:
                    if (oc.ContentId != null && Content.Hexes.TryGetValue(oc.ContentId, out var hex)) ApplyHex(hex);
                    break;
                case ShrineOutcomeKind.Consumable:
                    sim.SpawnPickupActor(PickupKind.Consumable, at, oc.ContentId ?? DrawConsumable()?.Id);
                    break;
                case ShrineOutcomeKind.Stat:
                    if (oc.Effect != null) Engine.AttachSource(new object(), shrine.Definition.Id + "_boon", EffectSourceKind.Temporary, new List<EffectSpec> { oc.Effect });
                    break;
                case ShrineOutcomeKind.Enemies:
                {
                    for (int i = 0; i < Math.Max(1, oc.Count); i++)
                    {
                        var e = sim.SpawnEnemy(oc.ContentId, at + new Vec2(1f, 0f).Rotated(360f / Math.Max(1, oc.Count) * i), false);
                        e?.Brain.Activate();
                    }
                    var runtime = FloorState.Current;
                    if (sim.LivingEnemies > 0 && runtime.Cleared) { runtime.Cleared = false; Engine.InCombat = true; RefreshDoors(); }
                    break;
                }
            }
        }
    }
}
