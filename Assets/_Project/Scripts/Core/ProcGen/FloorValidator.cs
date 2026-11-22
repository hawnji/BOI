using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    /// <summary>Checks a generated floor for navigability and structural correctness.</summary>
    public static class FloorValidator
    {
        public static ValidationReport Validate(FloorLayout layout, RoomTemplateLibrary library)
        {
            var r = new ValidationReport();
            if (layout.Rooms.Count == 0) { r.Error("Floor has no rooms."); return r; }
            if (layout.Count(RoomType.Start) != 1) r.Error("Floor must have exactly one start room.");
            if (layout.Count(RoomType.Boss) != 1) r.Error("Floor must have exactly one boss room.");
            if (layout.Count(RoomType.Treasure) < 1) r.Warn("Floor has no treasure room.");

            foreach (var room in layout.Rooms)
            {
                if (room.TemplateId == null) { r.Error($"Room {room.Index} has no template."); continue; }
                var t = library.Get(room.TemplateId);
                if (t == null) { r.Error($"Room {room.Index} references missing template {room.TemplateId}."); continue; }
                if (t.Shape != room.Shape) r.Error($"Room {room.Index}: template {t.Id} shape {t.Shape} != room shape {room.Shape}.");
                if (!t.AllowsType(room.Type)) r.Error($"Room {room.Index}: template {t.Id} does not allow {room.Type}.");
                foreach (var d in room.Doors)
                {
                    if (!t.IsDoorOpen(d.Socket)) r.Error($"Room {room.Index}: door {d.Socket.Id} is walled off in {t.Id}.");
                    var other = layout.Rooms[d.ToRoom];
                    if (!other.Doors.Exists(x => x.ToRoom == room.Index && x.Kind == d.Kind)) r.Error($"Door {d} has no matching door back.");
                    // The socket must face the neighbour's cell.
                    var worldCell = room.Anchor + d.Socket.LocalCell;
                    if (layout.RoomAt(worldCell.Step(d.Socket.Side))?.Index != d.ToRoom) r.Error($"Door {d} does not face room {d.ToRoom}.");
                }
                if ((room.Type == RoomType.Combat || room.Type == RoomType.Challenge) && room.Enemies.Count == 0)
                    r.Warn($"Combat room {room.Index} has no enemies (empty enemy pool?).");
                if (room.Type == RoomType.Boss && room.Enemies.Count == 0) r.Warn("Boss room has no boss assigned.");
            }

            // Everything except secrets must be reachable through ordinary doors (locked doors count: keys are obtainable).
            var reach = Reach(layout, d => d.Kind != DoorKind.Secret);
            foreach (var room in layout.Rooms)
            {
                bool secret = room.Type == RoomType.Secret || room.Type == RoomType.HiddenVault;
                if (!secret && !reach.Contains(room.Index)) r.Error($"Room {room.Index} ({room.Type}) is unreachable.");
                if (secret)
                {
                    bool hasEntrance = room.Doors.Exists(d => reach.Contains(d.ToRoom));
                    if (!hasEntrance) r.Error($"Secret room {room.Index} has no reachable neighbour.");
                }
            }

            // The boss must be reachable without keys, bombs or health costs.
            var free = Reach(layout, d => d.Kind == DoorKind.Normal || d.Kind == DoorKind.Boss);
            if (!free.Contains(layout.BossRoom)) r.Error("Boss room requires keys/bombs/sacrifice to reach.");

            // Locked rooms must be dead ends so locks never gate progression.
            foreach (var room in layout.Rooms)
            {
                bool lockedRoom = room.Doors.Exists(d => d.Kind == DoorKind.Locked) && room.Type != RoomType.Start;
                if (!lockedRoom) continue;
                int ordinary = room.Doors.FindAll(d => d.Kind != DoorKind.Secret).Count;
                if (ordinary > 1 && room.Doors.TrueForAll(d => d.Kind == DoorKind.Locked || d.Kind == DoorKind.Secret))
                    r.Error($"Locked room {room.Index} is not a dead end.");
            }
            return r;
        }

        private static HashSet<int> Reach(FloorLayout layout, System.Predicate<RoomDoor> passable)
        {
            var seen = new HashSet<int> { layout.StartRoom };
            var q = new Queue<int>();
            q.Enqueue(layout.StartRoom);
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                foreach (var d in layout.Rooms[i].Doors)
                    if (passable(d) && seen.Add(d.ToRoom)) q.Enqueue(d.ToRoom);
            }
            return seen;
        }
    }
}
