using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    public sealed class ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public void Error(string s) => Errors.Add(s);
        public void Warn(string s) => Warnings.Add(s);
        public void Merge(ValidationReport other) { Errors.AddRange(other.Errors); Warnings.AddRange(other.Warnings); }
        public override string ToString() => string.Join("\n", Errors) + (Warnings.Count > 0 ? "\n(warnings)\n" + string.Join("\n", Warnings) : "");
    }

    /// <summary>
    /// Checks a handcrafted template so the generator can never produce an unfair room:
    /// correct size, known tiles, open doors reachable from each other without bombs or flight,
    /// every enemy/pickup/interaction marker reachable, and type-specific requirements.
    /// </summary>
    public static class RoomTemplateValidator
    {
        public static ValidationReport Validate(RoomTemplate t)
        {
            var r = new ValidationReport();
            string id = string.IsNullOrEmpty(t.Id) ? "<no id>" : t.Id;
            if (string.IsNullOrEmpty(t.Id)) r.Error("Template has no id.");
            if (t.Types == null || t.Types.Length == 0) r.Error($"{id}: no room types.");
            if (t.Weight <= 0f) r.Error($"{id}: weight must be positive.");
            if (t.Difficulty < 1 || t.Difficulty > 10) r.Error($"{id}: difficulty must be 1..10.");

            int w = t.Width, h = t.Height;
            if (t.Layout == null || t.Layout.Length != h)
            {
                r.Error($"{id}: layout must have {h} rows for {t.Shape}, has {t.Layout?.Length ?? 0}.");
                return r;
            }
            for (int y = 0; y < h; y++)
            {
                if (t.Layout[y].Length != w) { r.Error($"{id}: row {y} has {t.Layout[y].Length} chars, expected {w}."); continue; }
                for (int x = 0; x < w; x++)
                {
                    char c = t.Layout[y][x];
                    if (!TileCodes.IsKnown(c)) r.Error($"{id}: unknown tile '{c}' at {x},{y}.");
                    bool inside = RoomShapes.Contains(t.Shape, new Cell(x / RoomShapes.CellWidth, y / RoomShapes.CellHeight));
                    if (!inside && c != TileCodes.Void) r.Error($"{id}: tile at {x},{y} is outside the {t.Shape} footprint (use ' ').");
                    if (inside && c == TileCodes.Void) r.Error($"{id}: void tile inside the room at {x},{y}.");
                }
            }
            if (!r.IsValid) return r;

            foreach (var b in t.BlockedDoors)
            {
                if (!RoomShapes.TryParseSocket(b, out var s)) { r.Error($"{id}: bad blocked door id '{b}'."); continue; }
                bool exists = false;
                foreach (var sock in RoomShapes.GetSockets(t.Shape)) if (sock.Equals(s)) exists = true;
                if (!exists) r.Error($"{id}: blocked door '{b}' is not a socket of {t.Shape}.");
            }

            var openDoors = new List<DoorSocket>();
            foreach (var s in RoomShapes.GetSockets(t.Shape)) if (t.IsDoorOpen(s)) openDoors.Add(s);
            if (openDoors.Count == 0) { r.Error($"{id}: every door is blocked."); return r; }

            foreach (var d in openDoors)
            {
                var (dx, dy) = RoomShapes.DoorTile(d);
                if (!TileCodes.IsSafeWalkable(t.TileAt(dx, dy))) r.Error($"{id}: door {d.Id} entrance tile ({dx},{dy}) is blocked by '{t.TileAt(dx, dy)}'.");
            }
            if (!r.IsValid) return r;

            var reach = FloodFill(t, RoomShapes.DoorTile(openDoors[0]));
            foreach (var d in openDoors)
            {
                var (dx, dy) = RoomShapes.DoorTile(d);
                if (!reach[dy, dx]) r.Error($"{id}: door {d.Id} cannot be reached from door {openDoors[0].Id} without bombs/flight.");
            }
            CheckMarkersReachable(t, reach, TileCodes.Enemy, "enemy spawn", r, id, warnOnly: t.HasTag("flyers"));
            CheckMarkersReachable(t, reach, TileCodes.Pickup, "pickup spot", r, id, warnOnly: true);
            CheckMarkersReachable(t, reach, TileCodes.Interact, "interaction point", r, id, warnOnly: false);

            foreach (var type in t.Types) CheckTypeRules(t, type, r, id);
            return r;
        }

        private static void CheckTypeRules(RoomTemplate t, RoomType type, ValidationReport r, string id)
        {
            int enemies = t.Count(TileCodes.Enemy), interact = t.Count(TileCodes.Interact);
            switch (type)
            {
                case RoomType.Combat:
                case RoomType.Challenge:
                    if (enemies < 1) r.Error($"{id}: {type} rooms need at least one enemy spawn 'E'.");
                    if (enemies > 14) r.Warn($"{id}: {enemies} enemy spawns is a lot for one room.");
                    break;
                case RoomType.Treasure:
                case RoomType.Reward:
                case RoomType.Event:
                case RoomType.Healing:
                case RoomType.Curse:
                    if (interact < 1) r.Error($"{id}: {type} rooms need an interaction point 'I'.");
                    if (enemies > 0) r.Warn($"{id}: {type} room contains enemy spawns.");
                    break;
                case RoomType.Shop:
                    if (interact < 2) r.Error($"{id}: shops need at least 2 interaction points (merchandise slots).");
                    break;
                case RoomType.Boss:
                case RoomType.Miniboss:
                    if (t.FindTiles(TileCodes.Enemy).Count < 1) r.Error($"{id}: {type} rooms need an 'E' marking the boss spawn.");
                    if (t.Count(TileCodes.Spikes) > 0) r.Warn($"{id}: spikes in a boss arena.");
                    break;
                case RoomType.Start:
                    if (enemies > 0) r.Error($"{id}: start rooms cannot contain enemies.");
                    if (t.Count(TileCodes.Spikes) + t.Count(TileCodes.Fire) > 0) r.Error($"{id}: start rooms cannot contain hazards.");
                    break;
            }
        }

        private static void CheckMarkersReachable(RoomTemplate t, bool[,] reach, char code, string label, ValidationReport r, string id, bool warnOnly)
        {
            foreach (var (x, y) in t.FindTiles(code))
            {
                if (reach[y, x]) continue;
                string msg = $"{id}: {label} at {x},{y} is unreachable.";
                if (warnOnly) r.Warn(msg); else r.Error(msg);
            }
        }

        /// <summary>4-way flood fill over safe walkable tiles.</summary>
        public static bool[,] FloodFill(RoomTemplate t, (int x, int y) start)
        {
            int w = t.Width, h = t.Height;
            var seen = new bool[h, w];
            if (!TileCodes.IsSafeWalkable(t.TileAt(start.x, start.y))) return seen;
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            seen[start.y, start.x] = true;
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
            }
            return seen;

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || seen[y, x]) return;
                if (!TileCodes.IsSafeWalkable(t.TileAt(x, y))) return;
                seen[y, x] = true;
                stack.Push((x, y));
            }
        }

        /// <summary>Detects layouts that duplicate each other (identical or mirrored), to stop content inflation.</summary>
        public static List<string> FindDuplicates(IReadOnlyList<RoomTemplate> templates)
        {
            var seen = new Dictionary<string, string>();
            var dupes = new List<string>();
            foreach (var t in templates)
            {
                if (t.Layout == null) continue;
                string key = t.Shape + "|" + string.Join("/", t.Layout);
                string mirror = t.Shape + "|" + string.Join("/", MirrorX(t.Layout));
                if (seen.TryGetValue(key, out var other) || seen.TryGetValue(mirror, out other))
                    dupes.Add($"{t.Id} duplicates {other}");
                else seen[key] = t.Id;
            }
            return dupes;
        }

        private static IEnumerable<string> MirrorX(string[] rows)
        {
            foreach (var row in rows)
            {
                var a = row.ToCharArray();
                System.Array.Reverse(a);
                yield return new string(a);
            }
        }
    }
}
