using System;
using System.Collections.Generic;
using Hollowcrawl.Rng;

namespace Hollowcrawl.ProcGen
{
    /// <summary>
    /// Hybrid floor generator: builds a room graph on a cell grid, assigns room types, places secret
    /// rooms, picks a handcrafted template for every room (shape, type, theme, doors, difficulty and
    /// anti-repetition weighting), populates encounters, then validates. Deterministic for a given
    /// (run seed, floor index, definition, template library, history state).
    /// </summary>
    public sealed class FloorGenerator
    {
        public const int GridSize = 13;
        public const int MaxAttempts = 60;

        private static readonly RoomShape[] LargeShapes =
            { RoomShape.Wide, RoomShape.Tall, RoomShape.Large, RoomShape.LNoNE, RoomShape.LNoNW, RoomShape.LNoSE, RoomShape.LNoSW };

        private readonly RoomTemplateLibrary _library;

        public FloorGenerator(RoomTemplateLibrary library)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public static IReadOnlyList<SpecialRoomRule> DefaultSpecialRules(int floorIndex) => new List<SpecialRoomRule>
        {
            new SpecialRoomRule { Type = RoomType.Treasure, Chance = 1f, Locked = floorIndex >= 1 },
            new SpecialRoomRule { Type = RoomType.Shop, Chance = 1f, Locked = floorIndex >= 1 },
            new SpecialRoomRule { Type = RoomType.Miniboss, Chance = 0.3f, MinFloor = 1 },
            new SpecialRoomRule { Type = RoomType.Challenge, Chance = 0.35f, MinFloor = 1 },
            new SpecialRoomRule { Type = RoomType.Event, Chance = 0.4f },
            new SpecialRoomRule { Type = RoomType.Reward, Chance = 0.3f, Locked = true },
            new SpecialRoomRule { Type = RoomType.Healing, Chance = 0.2f, MinFloor = 1 },
            new SpecialRoomRule { Type = RoomType.Curse, Chance = 0.25f, MinFloor = 1 },
        };

        /// <summary>Generates a floor. Throws only if no valid floor could be produced within MaxAttempts.</summary>
        public FloorLayout Generate(FloorDefinition def, int floorIndex, RunSeed seed, GenerationHistory history)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            history = history ?? new GenerationHistory();
            var floorRng = seed.CreateRandom().Fork("floor", floorIndex);
            var layoutRng = floorRng.Fork("layout");
            var templateRng = floorRng.Fork("templates");
            var encounterRng = floorRng.Fork("encounters");

            ValidationReport lastReport = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                history.BeginFloor();
                var layout = new FloorLayout { Seed = seed.Value, FloorIndex = floorIndex, FloorId = def.Id, Attempts = attempt };
                bool relaxed = attempt > MaxAttempts / 2;
                if (!BuildGraph(def, layout, layoutRng, relaxed)) continue;
                if (!AssignTypes(def, layout, floorIndex, layoutRng, relaxed)) continue;
                PlaceSecrets(def, layout, layoutRng);
                AddLoops(def, layout, layoutRng);
                AssignDoorKinds(layout);

                // Template choice and encounter population mutate history; snapshot-free approach:
                // a failed attempt only affects anti-repetition weights, never validity, and stays deterministic.
                if (!SelectTemplates(def, layout, templateRng, history, out string templateError))
                {
                    layout.Notes.Add(templateError);
                    continue;
                }
                PopulateEncounters(def, layout, floorIndex, encounterRng, history);
                lastReport = FloorValidator.Validate(layout, _library);
                if (!lastReport.IsValid) continue;
                foreach (var r in layout.Rooms) if (r.TemplateId != null) history.UseTemplate(r.TemplateId);
                foreach (var r in layout.Rooms) if (r.Type != RoomType.Combat && r.Type != RoomType.Start) history.UseSpecial(r.Type);
                return layout;
            }
            throw new InvalidOperationException($"Could not generate floor '{def.Id}' #{floorIndex} for seed {seed}: " + (lastReport?.ToString() ?? "graph/template constraints unsatisfiable"));
        }

        // ------------------------------------------------------------------ graph

        private bool BuildGraph(FloorDefinition def, FloorLayout layout, SeededRandom rng, bool relaxed)
        {
            int target = rng.Range(def.MinRooms, def.MaxRooms + 1);
            var start = new Cell(GridSize / 2, GridSize / 2);
            AddRoom(layout, RoomShape.Single, start);
            var queue = new List<int> { 0 };
            var dirs = new List<Direction> { Direction.North, Direction.East, Direction.South, Direction.West };
            int guard = 0;

            while (layout.Rooms.Count < target && guard++ < 400)
            {
                if (queue.Count == 0) queue.Add(rng.Range(0, layout.Rooms.Count)); // restart growth from a random room
                int parentIndex = queue[0];
                queue.RemoveAt(0);
                var parent = layout.Rooms[parentIndex];
                rng.Shuffle(dirs);
                foreach (var cell in parent.Cells.ToArray())
                {
                    foreach (var d in dirs)
                    {
                        if (layout.Rooms.Count >= target) break;
                        var n = cell.Step(d);
                        if (!InBounds(n) || layout.CellToRoom.ContainsKey(n)) continue;
                        if (parentIndex != 0 && rng.Chance(0.5f)) continue;
                        if (TryPlaceChild(def, layout, parent, cell, d, rng, out int child)) queue.Add(child);
                    }
                }
            }
            if (layout.Rooms.Count < def.MinRooms && !relaxed) return false;
            return layout.Rooms.Count >= 4;
        }

        private bool TryPlaceChild(FloorDefinition def, FloorLayout layout, RoomNode parent, Cell from, Direction d, SeededRandom rng, out int child)
        {
            child = -1;
            var target = from.Step(d);
            if (rng.Chance(def.LargeRoomChance))
            {
                var shape = LargeShapes[rng.Range(0, LargeShapes.Length)];
                var cells = RoomShapes.GetCells(shape);
                int offset = rng.Range(0, cells.Count);
                for (int k = 0; k < cells.Count; k++)
                {
                    var anchor = target - cells[(offset + k) % cells.Count];
                    if (CanPlace(layout, shape, anchor, parent.Index))
                    {
                        child = AddRoom(layout, shape, anchor);
                        Connect(layout, parent.Index, from, d);
                        return true;
                    }
                }
            }
            if (!CanPlace(layout, RoomShape.Single, target, parent.Index)) return false;
            child = AddRoom(layout, RoomShape.Single, target);
            Connect(layout, parent.Index, from, d);
            return true;
        }

        /// <summary>A new room may only touch its parent, which keeps the graph a tree (loops are added later on purpose).</summary>
        private static bool CanPlace(FloorLayout layout, RoomShape shape, Cell anchor, int parentIndex)
        {
            bool touchesParent = false;
            foreach (var local in RoomShapes.GetCells(shape))
            {
                var c = anchor + local;
                if (!InBounds(c) || layout.CellToRoom.ContainsKey(c)) return false;
                for (int dd = 0; dd < 4; dd++)
                {
                    var n = c.Step((Direction)dd);
                    if (RoomShapes.Contains(shape, n - anchor)) continue;
                    if (!layout.CellToRoom.TryGetValue(n, out int other)) continue;
                    if (other != parentIndex) return false;
                    touchesParent = true;
                }
            }
            return touchesParent || layout.Rooms.Count == 0;
        }

        private static int AddRoom(FloorLayout layout, RoomShape shape, Cell anchor)
        {
            var room = new RoomNode { Index = layout.Rooms.Count, Shape = shape, Anchor = anchor, Type = RoomType.Combat };
            foreach (var local in RoomShapes.GetCells(shape))
            {
                var c = anchor + local;
                room.Cells.Add(c);
                layout.CellToRoom[c] = room.Index;
            }
            layout.Rooms.Add(room);
            return room.Index;
        }

        /// <summary>Adds a door pair across the wall between cell 'from' and its neighbour in direction d.</summary>
        private static void Connect(FloorLayout layout, int roomA, Cell from, Direction d, DoorKind kind = DoorKind.Normal)
        {
            var to = from.Step(d);
            int roomB = layout.CellToRoom[to];
            var a = layout.Rooms[roomA];
            var b = layout.Rooms[roomB];
            a.Doors.Add(new RoomDoor { FromRoom = roomA, ToRoom = roomB, Socket = new DoorSocket(from - a.Anchor, d), Kind = kind });
            b.Doors.Add(new RoomDoor { FromRoom = roomB, ToRoom = roomA, Socket = new DoorSocket(to - b.Anchor, RoomShapes.Opposite(d)), Kind = kind });
        }

        private static bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < GridSize && c.Y < GridSize;

        // ------------------------------------------------------------------ room types

        private bool AssignTypes(FloorDefinition def, FloorLayout layout, int floorIndex, SeededRandom rng, bool relaxed)
        {
            ComputeDistances(layout, out var parents);
            layout.StartRoom = 0;
            layout.Rooms[0].Type = RoomType.Start;

            var deadEnds = new List<RoomNode>();
            foreach (var r in layout.Rooms) if (r.Index != 0 && r.Doors.Count == 1) deadEnds.Add(r);
            deadEnds.Sort((a, b) => a.DistanceFromStart != b.DistanceFromStart ? b.DistanceFromStart.CompareTo(a.DistanceFromStart) : a.Index.CompareTo(b.Index));

            RoomNode boss = null;
            foreach (var r in deadEnds)
            {
                if (r.DistanceFromStart < 2) continue;
                if (!_library.Has(r.Shape, RoomType.Boss, def.Theme)) continue;
                boss = r;
                break;
            }
            if (boss == null) return false;
            boss.Type = RoomType.Boss;
            layout.BossRoom = boss.Index;
            deadEnds.Remove(boss);

            // Mark the main path start -> boss.
            for (int i = boss.Index; i >= 0; i = parents[i]) { layout.Rooms[i].OnMainPath = true; if (i == 0) break; }

            var rules = def.SpecialRooms != null && def.SpecialRooms.Count > 0 ? (IReadOnlyList<SpecialRoomRule>)def.SpecialRooms : DefaultSpecialRules(floorIndex);
            var free = new List<RoomNode>(deadEnds);
            rng.Shuffle(free);
            foreach (var rule in rules)
            {
                if (floorIndex < rule.MinFloor) continue;
                for (int n = 0; n < Math.Max(1, rule.MaxCount); n++)
                {
                    if (!rng.Chance(rule.Chance)) continue;
                    var room = free.Find(r => _library.Has(r.Shape, rule.Type, def.Theme));
                    if (room == null)
                    {
                        if (rule.Chance >= 1f && !relaxed) return false; // mandatory room did not fit: retry the layout
                        layout.Notes.Add($"No dead end available for {rule.Type}.");
                        continue;
                    }
                    room.Type = rule.Type;
                    free.Remove(room);
                    if (rule.Locked) room.EncounterId = "locked"; // marker consumed by AssignDoorKinds
                }
            }
            return true;
        }

        private static void ComputeDistances(FloorLayout layout, out int[] parents)
        {
            parents = new int[layout.Rooms.Count];
            for (int i = 0; i < parents.Length; i++) { parents[i] = -1; layout.Rooms[i].DistanceFromStart = int.MaxValue; }
            var q = new Queue<int>();
            layout.Rooms[0].DistanceFromStart = 0;
            q.Enqueue(0);
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                foreach (var d in layout.Rooms[i].Doors)
                {
                    var o = layout.Rooms[d.ToRoom];
                    if (o.DistanceFromStart != int.MaxValue) continue;
                    o.DistanceFromStart = layout.Rooms[i].DistanceFromStart + 1;
                    parents[o.Index] = i;
                    q.Enqueue(o.Index);
                }
            }
        }

        // ------------------------------------------------------------------ secrets & loops

        private void PlaceSecrets(FloorDefinition def, FloorLayout layout, SeededRandom rng)
        {
            if (rng.Chance(def.SecretRoomChance) && _library.Has(RoomShape.Single, RoomType.Secret, def.Theme))
            {
                var best = new List<Cell>();
                int bestScore = 0;
                for (int y = 0; y < GridSize; y++)
                    for (int x = 0; x < GridSize; x++)
                    {
                        var c = new Cell(x, y);
                        if (layout.CellToRoom.ContainsKey(c)) continue;
                        int score = CountSecretNeighbours(layout, c, out bool touchesForbidden);
                        if (touchesForbidden || score == 0) continue;
                        if (score > bestScore) { bestScore = score; best.Clear(); }
                        if (score == bestScore) best.Add(c);
                    }
                if (best.Count > 0)
                {
                    var cell = best[rng.Range(0, best.Count)];
                    int idx = AddRoom(layout, RoomShape.Single, cell);
                    layout.Rooms[idx].Type = RoomType.Secret;
                    ConnectToAllNeighbours(layout, idx, DoorKind.Secret);
                    if (bestScore < 2) layout.Notes.Add("Secret room only borders one room.");
                }
            }

            if (rng.Chance(def.HiddenVaultChance) && _library.Has(RoomShape.Single, RoomType.HiddenVault, def.Theme))
            {
                var candidates = new List<(Cell cell, int dist)>();
                for (int y = 0; y < GridSize; y++)
                    for (int x = 0; x < GridSize; x++)
                    {
                        var c = new Cell(x, y);
                        if (layout.CellToRoom.ContainsKey(c)) continue;
                        int score = CountSecretNeighbours(layout, c, out bool forbidden);
                        if (forbidden || score != 1) continue;
                        RoomNode only = null;
                        for (int d = 0; d < 4; d++) { var r = layout.RoomAt(c.Step((Direction)d)); if (r != null) only = r; }
                        if (only == null || only.Type != RoomType.Combat) continue;
                        candidates.Add((c, only.DistanceFromStart));
                    }
                if (candidates.Count > 0)
                {
                    candidates.Sort((a, b) => b.dist != a.dist ? b.dist.CompareTo(a.dist) : (a.cell.Y * GridSize + a.cell.X).CompareTo(b.cell.Y * GridSize + b.cell.X));
                    int pickFrom = Math.Min(3, candidates.Count);
                    var cell = candidates[rng.Range(0, pickFrom)].cell;
                    int idx = AddRoom(layout, RoomShape.Single, cell);
                    layout.Rooms[idx].Type = RoomType.HiddenVault;
                    ConnectToAllNeighbours(layout, idx, DoorKind.Secret);
                }
            }
        }

        /// <summary>Distinct rooms around a cell; boss rooms and other secrets are forbidden neighbours.</summary>
        private static int CountSecretNeighbours(FloorLayout layout, Cell c, out bool touchesForbidden)
        {
            touchesForbidden = false;
            var seen = new HashSet<int>();
            for (int d = 0; d < 4; d++)
            {
                var r = layout.RoomAt(c.Step((Direction)d));
                if (r == null) continue;
                if (r.Type == RoomType.Boss || r.Type == RoomType.Secret || r.Type == RoomType.HiddenVault) { touchesForbidden = true; continue; }
                seen.Add(r.Index);
            }
            return seen.Count;
        }

        private static void ConnectToAllNeighbours(FloorLayout layout, int roomIndex, DoorKind kind)
        {
            var room = layout.Rooms[roomIndex];
            var connected = new HashSet<int>();
            foreach (var c in room.Cells)
                for (int d = 0; d < 4; d++)
                {
                    var n = c.Step((Direction)d);
                    if (!layout.CellToRoom.TryGetValue(n, out int other) || other == roomIndex || !connected.Add(other)) continue;
                    Connect(layout, roomIndex, c, (Direction)d, kind);
                }
        }

        private static void AddLoops(FloorDefinition def, FloorLayout layout, SeededRandom rng)
        {
            if (def.LoopChance <= 0f) return;
            foreach (var room in layout.Rooms)
            {
                if (room.Type != RoomType.Combat) continue;
                foreach (var c in room.Cells.ToArray())
                    for (int d = 0; d < 4; d++)
                    {
                        var n = c.Step((Direction)d);
                        if (!layout.CellToRoom.TryGetValue(n, out int other) || other <= room.Index) continue;
                        var o = layout.Rooms[other];
                        if (o.Type != RoomType.Combat) continue;
                        if (room.Doors.Exists(x => x.ToRoom == other)) continue;
                        if (rng.Chance(def.LoopChance)) Connect(layout, room.Index, c, (Direction)d);
                    }
            }
        }

        private static void AssignDoorKinds(FloorLayout layout)
        {
            foreach (var room in layout.Rooms)
            {
                foreach (var door in room.Doors)
                {
                    if (door.Kind == DoorKind.Secret) continue;
                    var a = layout.Rooms[door.FromRoom];
                    var b = layout.Rooms[door.ToRoom];
                    door.Kind = KindFor(b) > KindFor(a) ? KindFor(b) : KindFor(a);
                }
            }
            foreach (var room in layout.Rooms) if (room.EncounterId == "locked") room.EncounterId = null;

            DoorKind KindFor(RoomNode r)
            {
                if (r.Type == RoomType.Boss) return DoorKind.Boss;
                if (r.EncounterId == "locked") return DoorKind.Locked;
                if (r.Type == RoomType.Challenge) return DoorKind.Challenge;
                if (r.Type == RoomType.Curse) return DoorKind.Curse;
                return DoorKind.Normal;
            }
        }

        // ------------------------------------------------------------------ templates

        private bool SelectTemplates(FloorDefinition def, FloorLayout layout, SeededRandom rng, GenerationHistory history, out string error)
        {
            error = null;
            var picker = new WeightedPicker<RoomTemplate>();
            var usedThisFloor = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in layout.Rooms)
            {
                RoomTemplate chosen = null;
                for (int pass = 0; pass < 3 && chosen == null; pass++)
                {
                    picker.Clear();
                    foreach (var t in _library.Get(room.Shape, room.Type))
                    {
                        if (pass < 2 && !t.SuitsTheme(def.Theme)) continue;
                        if (!DoorsFit(t, room)) continue;
                        if (room.Type == RoomType.Combat && pass == 0 &&
                            (t.Difficulty < def.MinTemplateDifficulty || t.Difficulty > def.MaxTemplateDifficulty)) continue;
                        float w = t.Weight * history.TemplateFactor(t.Id);
                        if (usedThisFloor.Contains(t.Id)) w *= 0.05f;
                        picker.Add(t, w);
                    }
                    picker.TryPick(rng, out chosen);
                }
                if (chosen == null)
                {
                    error = $"No template for {room.Type} {room.Shape} with doors [{string.Join(",", room.Doors.ConvertAll(d => d.Socket.Id))}] in theme '{def.Theme}'.";
                    return false;
                }
                room.TemplateId = chosen.Id;
                usedThisFloor.Add(chosen.Id);
            }
            return true;
        }

        private static bool DoorsFit(RoomTemplate t, RoomNode room)
        {
            foreach (var d in room.Doors) if (!t.IsDoorOpen(d.Socket)) return false;
            return true;
        }

        // ------------------------------------------------------------------ encounters

        private void PopulateEncounters(FloorDefinition def, FloorLayout layout, int floorIndex, SeededRandom rng, GenerationHistory history)
        {
            var bossPicker = new WeightedPicker<string>();
            foreach (var b in def.Bosses) bossPicker.Add(b.Id, history.BossSeen(b.Id) ? b.Weight * 0.05f : b.Weight);
            bossPicker.TryPick(rng, out var bossId);
            layout.BossId = bossId;
            history.UseBoss(bossId);

            foreach (var room in layout.Rooms)
            {
                var t = _library.Get(room.TemplateId);
                if (t == null) continue;
                switch (room.Type)
                {
                    case RoomType.Combat:
                        PopulateCombat(def, room, t, floorIndex, rng, history, 1f);
                        break;
                    case RoomType.Challenge:
                        PopulateCombat(def, room, t, floorIndex, rng, history, 1.5f);
                        break;
                    case RoomType.Boss:
                        room.EncounterId = bossId;
                        PlaceEncounterSpawn(room, t, bossId);
                        break;
                    case RoomType.Miniboss:
                        var mp = new WeightedPicker<string>();
                        foreach (var m in def.Minibosses) mp.Add(m.Id, m.Weight);
                        mp.TryPick(rng, out var mini);
                        room.EncounterId = mini;
                        PlaceEncounterSpawn(room, t, mini);
                        break;
                }
                foreach (var (x, y) in t.FindTiles(TileCodes.Pickup))
                    if (rng.Chance(room.Type == RoomType.Secret || room.Type == RoomType.HiddenVault ? 1f : 0.3f))
                        room.Pickups.Add(new PickupSpawn { Kind = "random", TileX = x, TileY = y });
            }
        }

        private static void PlaceEncounterSpawn(RoomNode room, RoomTemplate t, string id)
        {
            var spawns = t.FindTiles(TileCodes.Enemy);
            if (spawns.Count == 0 || id == null) return;
            room.Enemies.Add(new EnemySpawn { EnemyId = id, TileX = spawns[0].x, TileY = spawns[0].y });
        }

        private static void PopulateCombat(FloorDefinition def, RoomNode room, RoomTemplate t, int floorIndex, SeededRandom rng, GenerationHistory history, float budgetScale)
        {
            if (def.Enemies.Count == 0) return;
            var spawns = t.FindTiles(TileCodes.Enemy);
            rng.Shuffle(spawns);
            int budget = (int)Math.Round((def.BaseEnemyBudget + floorIndex * 2 + t.Difficulty) * budgetScale);
            bool flyerOnlyAllowed = t.HasTag("flyers");

            // Lead enemy defines the composition; recently used leads are strongly down-weighted.
            var leadPicker = new WeightedPicker<EnemyPoolEntry>();
            foreach (var e in def.Enemies)
            {
                if (e.Cost > budget) continue;
                leadPicker.Add(e, history.CompositionRecent(e.EnemyId) ? e.Weight * 0.2f : e.Weight);
            }
            if (!leadPicker.TryPick(rng, out var lead)) lead = def.Enemies[0];

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var support = new WeightedPicker<EnemyPoolEntry>();
            foreach (var spawn in spawns)
            {
                if (budget <= 0 && room.Enemies.Count > 0) break;
                EnemyPoolEntry pick = null;
                counts.TryGetValue(lead.EnemyId, out int leadCount);
                if (lead.Cost <= Math.Max(budget, 1) && leadCount < lead.MaxPerRoom && (room.Enemies.Count == 0 || rng.Chance(0.55f))) pick = lead;
                else
                {
                    support.Clear();
                    foreach (var e in def.Enemies)
                    {
                        counts.TryGetValue(e.EnemyId, out int c);
                        if (e.Cost <= budget && c < e.MaxPerRoom) support.Add(e, e.Weight);
                    }
                    if (!support.TryPick(rng, out pick)) break;
                }
                counts.TryGetValue(pick.EnemyId, out int n);
                counts[pick.EnemyId] = n + 1;
                budget -= pick.Cost;
                room.Enemies.Add(new EnemySpawn { EnemyId = pick.EnemyId, TileX = spawn.x, TileY = spawn.y, Elite = rng.Chance(def.EliteChance) });
            }
            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            room.CompositionKey = string.Join("+", keys);
            history.UseComposition(lead.EnemyId);
        }
    }
}
