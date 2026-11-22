using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    public enum DoorKind { Normal = 0, Locked = 1, Secret = 2, Boss = 3, Challenge = 4, Curse = 5 }

    public sealed class RoomDoor
    {
        public int FromRoom;
        public int ToRoom;
        /// <summary>The socket on this room's template (local cell + side).</summary>
        public DoorSocket Socket;
        public DoorKind Kind;
        public override string ToString() => $"{FromRoom}->{ToRoom} {Socket.Id} {Kind}";
    }

    public sealed class EnemySpawn
    {
        public string EnemyId;
        public int TileX, TileY;
        public bool Elite;
    }

    public sealed class PickupSpawn
    {
        public string Kind;   // PickupKind name or "random"
        public int TileX, TileY;
    }

    public sealed class RoomNode
    {
        public int Index;
        public RoomType Type;
        public RoomShape Shape;
        /// <summary>Grid position of the shape's local (0,0).</summary>
        public Cell Anchor;
        public readonly List<Cell> Cells = new List<Cell>();
        public readonly List<RoomDoor> Doors = new List<RoomDoor>();
        public string TemplateId;
        public int DistanceFromStart;
        public bool OnMainPath;
        public bool IsDeadEnd => Doors.FindAll(d => d.Kind != DoorKind.Secret).Count == 1;
        public readonly List<EnemySpawn> Enemies = new List<EnemySpawn>();
        public readonly List<PickupSpawn> Pickups = new List<PickupSpawn>();
        /// <summary>Boss or miniboss id for those room types.</summary>
        public string EncounterId;
        public string CompositionKey;
        public override string ToString() => $"#{Index} {Type} {Shape} @{Anchor} tpl={TemplateId}";
    }

    public sealed class FloorLayout
    {
        public uint Seed;
        public int FloorIndex;
        public string FloorId;
        public readonly List<RoomNode> Rooms = new List<RoomNode>();
        public readonly Dictionary<Cell, int> CellToRoom = new Dictionary<Cell, int>();
        public int StartRoom;
        public int BossRoom;
        public string BossId;
        public int Attempts;
        public readonly List<string> Notes = new List<string>();

        public RoomNode RoomAt(Cell c) => CellToRoom.TryGetValue(c, out var i) ? Rooms[i] : null;
        public IEnumerable<RoomNode> OfType(RoomType t) { foreach (var r in Rooms) if (r.Type == t) yield return r; }
        public int Count(RoomType t) { int n = 0; foreach (var r in Rooms) if (r.Type == t) n++; return n; }
    }
}
