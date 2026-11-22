using System;
using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Content;
using Hollowcrawl.Economy;
using Hollowcrawl.Enemies;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.Movement;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Rooms;
using Hollowcrawl.Stats;

namespace Hollowcrawl.Simulation
{
    public enum RunState { Playing, Won, Lost }

    /// <summary>Everything needed to start a run. Characters (phase 9) fill this in.</summary>
    public sealed class RunSetup
    {
        public RunSeed Seed = new RunSeed(1);
        public DifficultyDefinition Difficulty = DifficultyDefinition.Normal();
        public string CharacterId = "wanderer";
        public int StartingContainers = 3;
        public int StartingWard;
        public int StartingUmbral;
        public int StartingCoins;
        public int StartingBombs = 1;
        public int StartingKeys;
        public List<string> StartingItems = new List<string>();
        public List<StatModifier> StatModifiers = new List<StatModifier>();
        /// <summary>Floor ids in order. Empty = every floor definition sorted by depth.</summary>
        public List<string> FloorSequence = new List<string>();
        /// <summary>Number of floors in the run (the last floor's boss ends it). 0 = length of the sequence.</summary>
        public int FloorCount;
        public bool IsDaily;
        public Func<string, bool> IsUnlocked = _ => true;
    }

    /// <summary>
    /// A whole run, headless: floor generation, room transitions, door rules, room population, rewards,
    /// shops, floor progression, victory and defeat. Owns the player's item engine. Unity drives it with
    /// <see cref="Step"/> on a fixed timestep and draws <see cref="CurrentRoom"/>.
    /// </summary>
    public sealed class RunSimulation : IRoomHost
    {
        public const float FixedStep = 1f / 60f;

        public RunSetup Setup { get; }
        public ContentDatabase Content { get; }
        public RoomTemplateLibrary Templates { get; }
        public InteractionEngine Engine { get; }
        public PlayerActor Player { get; }
        public SimEventQueue Events { get; } = new SimEventQueue();
        public RunState State { get; private set; } = RunState.Playing;
        public int FloorIndex { get; private set; } = -1;
        public FloorDefinition Floor { get; private set; }
        public FloorLayout Layout { get; private set; }
        public FloorState FloorState { get; private set; }
        public RoomSimulation CurrentRoom { get; private set; }
        public float RunTime { get; private set; }
        public int RoomsCleared { get; private set; }
        public int EnemiesKilled { get; private set; }
        public int BossesKilled { get; private set; }
        public int TotalFloors { get; }
        public string DeathCause { get; private set; }

        public readonly List<CompanionActor> Companions = new List<CompanionActor>();

        public event Action<RoomSimulation> RoomChanged;
        public event Action<int> FloorChanged;
        public event Action<RunState> Ended;

        private readonly List<FloorDefinition> _sequence = new List<FloorDefinition>();
        private readonly Dictionary<int, RoomSimulation> _rooms = new Dictionary<int, RoomSimulation>();
        private readonly HashSet<int> _populated = new HashSet<int>();
        private readonly Dictionary<int, (ShopInventory shop, int index)> _shopSlots = new Dictionary<int, (ShopInventory, int)>();
        private readonly GenerationHistory _history = new GenerationHistory();
        private readonly FloorGenerator _generator;
        private readonly ItemPools _pools;
        private readonly DropTables _drops = new DropTables();
        private readonly SeededRandom _rng;
        private readonly SeededRandom _dropRng;
        private readonly IdSource _ids = new IdSource();
        private bool _pendingNextFloor;
        private TeleportTarget? _pendingTeleport;

        public RunSimulation(RunSetup setup, ContentDatabase content, RoomTemplateLibrary templates, EffectRegistry effects, ConditionRegistry conditions = null)
        {
            Setup = setup ?? new RunSetup();
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Templates = templates ?? throw new ArgumentNullException(nameof(templates));
            var root = Setup.Seed.CreateRandom();
            _rng = root.Fork("run");
            _dropRng = root.Fork("drops");

            var stats = new StatSheet();
            foreach (var m in Setup.StatModifiers) stats.Add(m);
            var health = new HealthPool(Setup.StartingContainers, Setup.StartingContainers * 2, Setup.StartingWard, Setup.StartingUmbral);
            var resources = new PlayerResources();
            resources.Set(ResourceKind.Coins, Setup.StartingCoins);
            resources.Set(ResourceKind.Bombs, Setup.StartingBombs);
            resources.Set(ResourceKind.Keys, Setup.StartingKeys);
            Engine = new InteractionEngine(effects ?? EffectRegistry.CreateDefault(), stats, health, resources, null, root.Fork("items"), conditions)
            {
                CharacterId = Setup.CharacterId,
            };
            Engine.SetCombos(Content.Synergies.Values, Content.Transformations.Values);
            health.Died += OnPlayerDied;

            Player = new PlayerActor(new MovementTuning()) { Id = _ids.Next() };
            _pools = new ItemPools(Content.Items.Values, Setup.IsUnlocked, root.Fork("pools"));
            _generator = new FloorGenerator(Templates);

            if (Setup.FloorSequence.Count > 0)
                foreach (var id in Setup.FloorSequence) { if (Content.Floors.TryGetValue(id, out var f)) _sequence.Add(f); }
            if (_sequence.Count == 0) _sequence.AddRange(Content.Floors.Values.OrderBy(f => f.Depth).ThenBy(f => f.Id, StringComparer.Ordinal));
            if (_sequence.Count == 0) throw new InvalidOperationException("No floor definitions loaded.");
            TotalFloors = Setup.FloorCount > 0 ? Setup.FloorCount : _sequence.Count;

            foreach (var id in Setup.StartingItems)
                if (Content.Items.TryGetValue(id, out var item)) { _pools.MarkOffered(id); Engine.AddItem(item); }

            StartFloor(0);
        }

        // ================================================================== loop

        /// <summary>Advances the run by one fixed step.</summary>
        public void Step(in PlayerInputFrame input, float dt = FixedStep)
        {
            if (State != RunState.Playing) return;
            RunTime += dt;
            SyncPlayerFromItems();
            var room = CurrentRoom;
            room.Step(dt, input);
            if (State != RunState.Playing) return;

            CheckRoomClear();

            if (_pendingNextFloor) { _pendingNextFloor = false; StartFloor(FloorIndex + 1); return; }
            if (_pendingTeleport.HasValue) { var t = _pendingTeleport.Value; _pendingTeleport = null; DoTeleport(t); return; }
            if (room.ExitRequested != null) UseDoor(room.ExitRequested);
        }

        private void SyncPlayerFromItems()
        {
            var h = Engine.Health;
            int target = (int)Math.Round(Engine.Stats[StatId.MaxHealth]);
            if (target != h.Containers) h.SetContainers(target, target > h.Containers);
            Player.Flying = Engine.CountHeldWithTag("flight") > 0;
        }

        // ================================================================== floors and rooms

        private void StartFloor(int index)
        {
            if (index >= TotalFloors) { End(RunState.Won); return; }
            CurrentRoom?.OnPlayerLeft();
            FloorIndex = index;
            Floor = _sequence[Math.Min(index, _sequence.Count - 1)];
            Layout = _generator.Generate(Floor, index, Setup.Seed, _history);
            _rooms.Clear();
            _populated.Clear();
            _shopSlots.Clear();
            FloorState = new FloorState(Layout);
            Engine.NotifyFloorStarted(index);
            Events.Add(new SimEvent { Kind = SimEventKind.FloorStarted, Value = index, Key = Floor.Id });
            FloorChanged?.Invoke(index);
            var start = GetRoom(Layout.StartRoom);
            EnterRoom(Layout.StartRoom, start.Grid.Center, firstTimeInRun: true);
        }

        private RoomSimulation GetRoom(int index)
        {
            if (_rooms.TryGetValue(index, out var sim)) return sim;
            var node = Layout.Rooms[index];
            var template = Templates.Get(node.TemplateId);
            var grid = RoomGrid.FromTemplate(template, node.Shape);
            sim = new RoomSimulation(this, node, grid, Engine, _rng.Fork("room", FloorIndex * 1000 + index), _ids, Events, Setup.Difficulty, FloorIndex)
            {
                Companions = Companions,
            };
            _rooms[index] = sim;
            return sim;
        }

        private void EnterRoom(int index, Vec2 playerPosition, bool firstTimeInRun = false)
        {
            CurrentRoom?.OnPlayerLeft();
            var runtime = FloorState.Rooms[index];
            bool firstVisit = _populated.Add(index);
            var sim = GetRoom(index);
            CurrentRoom = sim;
            Engine.World = sim;
            RefreshDoors();
            sim.PlacePlayer(Player, sim.NearestOpen(playerPosition));
            if (firstVisit) Populate(sim, runtime);
            Engine.InCombat = !runtime.Cleared;
            RefreshDoors();
            Events.Add(new SimEvent { Kind = SimEventKind.RoomEntered, Value = index, Key = runtime.Node.Type.ToString(), Flag = firstVisit });
            Engine.NotifyRoomEntered(new RoomContext { RoomType = runtime.Node.Type.ToString(), RoomIndex = index, FirstVisit = firstVisit, TookDamage = false });
            RoomChanged?.Invoke(sim);
        }

        private void Populate(RoomSimulation sim, RoomRuntime runtime)
        {
            var node = runtime.Node;
            var template = Templates.Get(node.TemplateId);
            var grid = sim.Grid;
            foreach (var spawn in node.Enemies)
                sim.SpawnEnemy(spawn.EnemyId, grid.TileCenter(spawn.TileX, spawn.TileY), spawn.Elite);
            if ((node.Type == RoomType.Boss || node.Type == RoomType.Miniboss) && sim.Enemies.Count == 0 && node.EncounterId != null)
                sim.SpawnEnemy(node.EncounterId, grid.Center + new Vec2(0f, 1.5f), false);
            foreach (var p in node.Pickups)
                sim.SpawnPickupActor(ResolvePickupKind(p.Kind), grid.TileCenter(p.TileX, p.TileY), scatter: false);

            var slots = template != null ? template.FindTiles(TileCodes.Interact) : new List<(int x, int y)>();
            switch (node.Type)
            {
                case RoomType.Treasure:
                    PlacePedestal(sim, slots.Count > 0 ? grid.TileCenter(slots[0].x, slots[0].y) : grid.Center, "treasure");
                    break;
                case RoomType.Secret:
                case RoomType.HiddenVault:
                    PlacePedestal(sim, slots.Count > 0 ? grid.TileCenter(slots[0].x, slots[0].y) : grid.Center, "secret");
                    break;
                case RoomType.Curse:
                    PlacePedestal(sim, slots.Count > 0 ? grid.TileCenter(slots[0].x, slots[0].y) : grid.Center, "curse", "treasure");
                    break;
                case RoomType.Shop:
                    PopulateShop(sim, slots);
                    break;
                case RoomType.Healing:
                    sim.SpawnPickupActor(PickupKind.VigorFull, grid.Center + new Vec2(-1f, 0f), scatter: false);
                    sim.SpawnPickupActor(PickupKind.VigorFull, grid.Center + new Vec2(1f, 0f), scatter: false);
                    break;
                case RoomType.Reward:
                    for (int i = 0; i < 3; i++) sim.SpawnPickupActor(ResolvePickupKind(_drops.RollChest(_dropRng).ToString()), grid.Center + new Vec2(i - 1f, 0f));
                    break;
            }

            int living = sim.LivingEnemies;
            runtime.EnemiesAlive = living;
            if (living == 0 && !runtime.Cleared) FloorState.MarkClearedSilently(runtime);
            foreach (var e in sim.Enemies) e.Brain.Activate();
        }

        private void PlacePedestal(RoomSimulation sim, Vec2 pos, string pool, string fallbackPool = "treasure", int price = 0)
        {
            var item = _pools.Draw(pool, Engine, Engine.Stats[StatId.Luck]) ?? _pools.Draw(fallbackPool, Engine, Engine.Stats[StatId.Luck]);
            if (item == null) { sim.SpawnPickupActor(PickupKind.Coin, pos); return; }
            sim.SpawnPickupActor(item.Kind == ItemKind.Trinket ? PickupKind.Trinket : PickupKind.Item, pos, item.Id, price, pedestal: true, scatter: false);
        }

        private void PopulateShop(RoomSimulation sim, List<(int x, int y)> slots)
        {
            var shop = ShopInventory.Generate(Math.Max(3, slots.Count), _pools, Engine, _dropRng, FloorIndex);
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                var o = shop.Offers[i];
                var pos = i < slots.Count ? sim.Grid.TileCenter(slots[i].x, slots[i].y) : sim.Grid.Center + new Vec2((i - (shop.Offers.Count - 1) * 0.5f) * 2f, 0f);
                var kind = o.Kind == ShopOfferKind.Item ? (o.Item.Kind == ItemKind.Trinket ? PickupKind.Trinket : PickupKind.Item) : ResolvePickupKind(o.Pickup.ToString());
                var p = sim.SpawnPickupActor(kind, pos, o.Item?.Id, Math.Max(1, o.Price), pedestal: true, scatter: false);
                if (p != null) _shopSlots[p.Id] = (shop, i);
            }
        }

        /// <summary>Maps generator/drop pickup names to kinds this build can grant. Consumables arrive in phase 7.</summary>
        private PickupKind ResolvePickupKind(string kind)
        {
            if (kind == null || kind == "random")
            {
                var k = _drops.RollProp(_dropRng, null) ?? PickupKind.Coin;
                return k == PickupKind.Consumable ? PickupKind.Coin : k;
            }
            if (!Enum.TryParse(kind, out PickupKind parsed)) return PickupKind.Coin;
            return parsed == PickupKind.Consumable ? PickupKind.Coin : parsed;
        }

        private void RefreshDoors()
        {
            var room = CurrentRoom;
            foreach (var d in room.Node.Doors)
            {
                var state = FloorState.GetDoorState(d);
                var visual = state == DoorState.Open ? DoorVisual.Open
                    : state == DoorState.Hidden ? DoorVisual.Hidden
                    : state == DoorState.Locked ? DoorVisual.Locked
                    : DoorVisual.Closed;
                room.SetDoor(d, visual);
            }
        }

        private void CheckRoomClear()
        {
            var runtime = FloorState.Current;
            runtime.EnemiesAlive = CurrentRoom.LivingEnemies;
            if (runtime.Cleared || runtime.EnemiesAlive > 0) return;
            FloorState.MarkCleared();
            OnRoomCleared(runtime);
        }

        private void OnRoomCleared(RoomRuntime runtime)
        {
            RoomsCleared++;
            var sim = CurrentRoom;
            var node = runtime.Node;
            Engine.NotifyRoomCleared(new RoomContext { RoomType = node.Type.ToString(), RoomIndex = node.Index, FirstVisit = true, TookDamage = runtime.TookDamageHere });
            Events.Add(new SimEvent { Kind = SimEventKind.RoomCleared, Value = node.Index, Key = node.Type.ToString() });
            if (node.Type == RoomType.Boss)
            {
                var boss = node.EncounterId != null ? FindBoss(node.EncounterId) : null;
                PlacePedestal(sim, sim.Grid.Center + new Vec2(0f, 1.5f), boss?.RewardPool ?? "boss");
                bool last = FloorIndex + 1 >= TotalFloors;
                sim.SpawnExit(sim.Grid.Center - new Vec2(0f, 1.5f), last);
            }
            else if (node.Type == RoomType.Miniboss)
            {
                for (int i = 0; i < 3; i++) sim.SpawnPickupActor(ResolvePickupKind(_drops.RollChest(_dropRng).ToString()), sim.Grid.Center);
            }
            else
            {
                var drop = _drops.RollRoomClear(_dropRng, Engine);
                if (drop.HasValue) sim.SpawnPickupActor(ResolvePickupKind(drop.Value.ToString()), sim.Grid.Center);
            }
            RefreshDoors();
        }

        private void UseDoor(RoomDoor door)
        {
            int from = FloorState.CurrentRoom;
            var result = FloorState.TryUseDoor(door, Engine.Resources, Engine.Health);
            if (result != DoorUseResult.Passed && result != DoorUseResult.PaidHealth)
            {
                // pushed back into the room (challenge door at less than full health, etc.)
                RoomGrid.DoorEntryTile(door.Socket, out int x, out int r);
                CurrentRoom.PlacePlayer(Player, CurrentRoom.Grid.TileCenter(x, r));
                Events.Add(new SimEvent { Kind = SimEventKind.Feedback, Key = "door_refused_" + result, Position = Player.Position });
                return;
            }
            var fromNode = Layout.Rooms[from];
            var toNode = Layout.Rooms[door.ToRoom];
            var entry = MatchingDoor(fromNode, door, toNode);
            var toSim = GetRoom(door.ToRoom);
            Vec2 pos = toSim.Grid.Center;
            if (entry != null) { RoomGrid.DoorEntryTile(entry.Socket, out int x, out int r); pos = toSim.Grid.TileCenter(x, r); }
            EnterRoom(door.ToRoom, pos);
        }

        public static RoomDoor MatchingDoor(RoomNode fromNode, RoomDoor door, RoomNode toNode)
        {
            var worldCell = fromNode.Anchor + door.Socket.LocalCell;
            var neighbour = worldCell.Step(door.Socket.Side);
            var opposite = RoomShapes.Opposite(door.Socket.Side);
            RoomDoor fallback = null;
            foreach (var d in toNode.Doors)
            {
                if (d.ToRoom != fromNode.Index) continue;
                fallback = fallback ?? d;
                if ((toNode.Anchor + d.Socket.LocalCell).Equals(neighbour) && d.Socket.Side == opposite) return d;
            }
            return fallback;
        }

        private void DoTeleport(TeleportTarget target)
        {
            int index;
            switch (target)
            {
                case TeleportTarget.StartRoom: index = Layout.StartRoom; break;
                case TeleportTarget.UnvisitedSpecialRoom:
                {
                    var options = FloorState.Rooms.Where(r => !r.Visited && r.Node.Type != RoomType.Combat && r.Node.Type != RoomType.Secret && r.Node.Type != RoomType.HiddenVault).Select(r => r.Node.Index).ToList();
                    index = options.Count > 0 ? options[_rng.Range(0, options.Count)] : Layout.StartRoom;
                    break;
                }
                default:
                {
                    var options = FloorState.Rooms.Where(r => r.Node.Type != RoomType.Secret && r.Node.Type != RoomType.HiddenVault && r.Node.Index != FloorState.CurrentRoom).Select(r => r.Node.Index).ToList();
                    index = options.Count > 0 ? options[_rng.Range(0, options.Count)] : Layout.StartRoom;
                    break;
                }
            }
            FloorState.TeleportTo(index);
            var sim = GetRoom(index);
            EnterRoom(index, sim.Grid.Center);
        }

        /// <summary>Debug/test helper: moves the player straight into a room of this floor (no door rules).</summary>
        public void DebugEnterRoom(int index)
        {
            FloorState.TeleportTo(index);
            EnterRoom(index, GetRoom(index).Grid.Center);
        }

        private void End(RunState state)
        {
            if (State != RunState.Playing) return;
            State = state;
            Events.Add(new SimEvent { Kind = state == RunState.Won ? SimEventKind.RunWon : SimEventKind.RunLost, Value = FloorIndex });
            Ended?.Invoke(state);
        }

        private void OnPlayerDied()
        {
            Events.Add(new SimEvent { Kind = SimEventKind.PlayerDied, Position = Player.Position });
            End(RunState.Lost);
        }

        // ================================================================== IRoomHost

        public EnemyDefinition FindEnemy(string id) => id != null && Content.Enemies.TryGetValue(id, out var e) ? e : null;
        public BossDefinition FindBoss(string id) => id != null && Content.Bosses.TryGetValue(id, out var b) ? b : null;
        public StatusEffectDefinition FindStatus(string id) => id != null && Content.Statuses.TryGetValue(id, out var s) ? s : null;

        public bool TryCollect(PickupActor p)
        {
            if (_shopSlots.TryGetValue(p.Id, out var slot)) return TryBuy(p, slot.shop, slot.index);
            if (p.Price > 0 && !Engine.Resources.TrySpend(ResourceKind.Coins, p.Price)) { RefusePurchase(p); return false; }
            return Grant(p);
        }

        private bool TryBuy(PickupActor p, ShopInventory shop, int index)
        {
            shop.Reprice(Engine);
            var offer = shop.Offers[index];
            if (offer.Kind == ShopOfferKind.Item)
            {
                if (!shop.TryBuy(index, Engine.Resources, Engine, out var displaced)) { RefusePurchase(p); return false; }
                if (displaced != null) DropDisplaced(displaced, p.Position);
                return true;
            }
            if (!CanGrant(p.Pickup)) { p.SpawnProtection = 0.5f; return false; }
            if (!shop.TryBuy(index, Engine.Resources, Engine, out _)) { RefusePurchase(p); return false; }
            return Grant(p);
        }

        private void RefusePurchase(PickupActor p)
        {
            p.SpawnProtection = 0.75f;
            Events.Add(new SimEvent { Kind = SimEventKind.PurchaseFailed, ActorId = p.Id, Position = p.Position, Value = p.Price });
        }

        private bool CanGrant(PickupKind kind)
        {
            var h = Engine.Health;
            switch (kind)
            {
                case PickupKind.VigorHalf:
                case PickupKind.VigorFull: return h.Vigor < h.MaxVigor;
                case PickupKind.Ward:
                case PickupKind.Umbral: return h.TotalHalves < h.MaxVigor + HealthPool.MaxTemporaryHalves;
                default: return true;
            }
        }

        private bool Grant(PickupActor p)
        {
            var res = Engine.Resources;
            var h = Engine.Health;
            switch (p.Pickup)
            {
                case PickupKind.Coin: res.Add(ResourceKind.Coins, 1); return true;
                case PickupKind.Bomb: res.Add(ResourceKind.Bombs, 1); return true;
                case PickupKind.Key: res.Add(ResourceKind.Keys, 1); return true;
                case PickupKind.Ember: res.Add(ResourceKind.Embers, 1); return true;
                case PickupKind.VigorHalf: if (!CanGrant(p.Pickup)) return false; h.Heal(1); Heal(p, 1); return true;
                case PickupKind.VigorFull: if (!CanGrant(p.Pickup)) return false; h.Heal(2); Heal(p, 2); return true;
                case PickupKind.Ward: h.AddWard(2); return true;
                case PickupKind.Umbral: h.AddUmbral(2); return true;
                case PickupKind.Item:
                case PickupKind.Trinket:
                {
                    ItemDefinition item = null;
                    if (p.ContentId != null) Content.Items.TryGetValue(p.ContentId, out item);
                    if (item == null && p.Pickup == PickupKind.Trinket) item = _pools.Draw("trinket", Engine, Engine.Stats[StatId.Luck]);
                    if (item == null) { res.Add(ResourceKind.Coins, 1); return true; }
                    if (!Engine.CanAccept(item)) return false;
                    var result = Engine.AddItem(item, out var displaced);
                    if (result == AddItemResult.RefusedUnique || result == AddItemResult.RefusedStackCap || result == AddItemResult.Invalid) return false;
                    if (displaced != null) DropDisplaced(displaced, p.Position);
                    p.ContentId = item.Id;
                    return true;
                }
                default: res.Add(ResourceKind.Coins, 1); return true;
            }
        }

        private void Heal(PickupActor p, int halves) => Events.Add(new SimEvent { Kind = SimEventKind.PlayerHealed, Position = p.Position, Value = halves });

        private void DropDisplaced(ItemDefinition item, Vec2 at)
        {
            var dropped = CurrentRoom.SpawnPickupActor(item.Kind == ItemKind.Trinket ? PickupKind.Trinket : PickupKind.Item, at + new Vec2(0f, -1.2f), item.Id, 0, pedestal: true);
            if (dropped != null) dropped.SpawnProtection = 1.5f; // don't instantly swap back
        }

        public void OnDoorBumped(RoomDoor door)
        {
            if (FloorState.GetDoorState(door) != DoorState.Locked) return;
            if (FloorState.TryUseDoor(door, Engine.Resources, Engine.Health) == DoorUseResult.KeyUsed) RefreshDoors();
            else Events.Add(new SimEvent { Kind = SimEventKind.Feedback, Key = "door_needs_key", Position = Player.Position });
        }

        public void OnBlastNearDoor(RoomDoor door)
        {
            if (!FloorState.RevealSecretDoor(door)) return;
            RefreshDoors();
            RoomGrid.DoorTile(door.Socket, out int x, out int r);
            Events.Add(new SimEvent { Kind = SimEventKind.SecretRevealed, TileX = x, TileRow = r });
        }

        public void OnEnemySpawned(EnemyActor enemy) { }

        public void OnEnemyKilled(EnemyActor enemy)
        {
            EnemiesKilled++;
            if (enemy.IsBoss && enemy.Boss.Tier != BossTier.Miniboss) BossesKilled++;
            if (enemy.IsBoss || enemy.Definition.LootMultiplier <= 0f) return;
            var drop = _drops.RollEnemyDrop(_dropRng, Engine, enemy.Elite);
            if (drop.HasValue && CurrentRoom != null) CurrentRoom.SpawnPickupActor(ResolvePickupKind(drop.Value.ToString()), enemy.Position);
        }

        public void OnPlayerDamaged(int halvesLost)
        {
            FloorState.Current.TookDamageHere = true;
            if (Engine.Health.IsDead) DeathCause = DeathCause ?? "damage";
        }

        public void OnTileDestroyed(int x, int row, TileKind previous)
        {
            var sim = CurrentRoom;
            if (sim == null) return;
            var at = sim.Grid.TileCenter(x, row);
            if (previous == TileKind.Prop)
            {
                var drop = _drops.RollProp(_dropRng, Engine);
                if (drop.HasValue) sim.SpawnPickupActor(ResolvePickupKind(drop.Value.ToString()), at);
            }
            else if (previous == TileKind.MarkedRock)
                sim.SpawnPickupActor(ResolvePickupKind(_drops.RollChest(_dropRng).ToString()), at);
        }

        public void OnExitReached(ExitActor exit)
        {
            if (exit.IsVictory) End(RunState.Won);
            else _pendingNextFloor = true;
        }

        public void SpawnCompanion(string companionId, object source)
        {
            if (Companions.Count >= 12) return;
            Companions.Add(new CompanionActor { Id = _ids.Next(), CompanionId = companionId, Source = source, Position = Player.Position, Radius = 0.25f, ViewKey = "companion_" + companionId });
        }

        public void RemoveCompanions(object source) => Companions.RemoveAll(c => ReferenceEquals(c.Source, source));

        public void TeleportPlayer(TeleportTarget target) => _pendingTeleport = target;
    }
}
