using System;
using System.Collections.Generic;

namespace Hollowcrawl.ProcGen
{
    /// <summary>Footprint of a room on the floor grid. Each cell is one screen (CellWidth x CellHeight tiles).</summary>
    public enum RoomShape
    {
        Single = 0,   // 1x1
        Wide = 1,     // 2x1
        Tall = 2,     // 1x2
        Large = 3,    // 2x2
        LNoNE = 4,    // 2x2 missing north-east cell
        LNoNW = 5,    // 2x2 missing north-west cell
        LNoSE = 6,    // 2x2 missing south-east cell
        LNoSW = 7,    // 2x2 missing south-west cell
    }

    public enum Direction { North = 0, East = 1, South = 2, West = 3 }

    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Y; // Y grows southward (matches template row order)
        public Cell(int x, int y) { X = x; Y = y; }
        public Cell Step(Direction d)
        {
            switch (d)
            {
                case Direction.North: return new Cell(X, Y - 1);
                case Direction.East: return new Cell(X + 1, Y);
                case Direction.South: return new Cell(X, Y + 1);
                default: return new Cell(X - 1, Y);
            }
        }
        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static Cell operator -(Cell a, Cell b) => new Cell(a.X - b.X, a.Y - b.Y);
        public bool Equals(Cell o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>A door socket on a shape: the local cell it belongs to and the wall direction.</summary>
    public readonly struct DoorSocket : IEquatable<DoorSocket>
    {
        public readonly Cell LocalCell;
        public readonly Direction Side;
        public DoorSocket(Cell localCell, Direction side) { LocalCell = localCell; Side = side; }
        /// <summary>Stable id used in template files, e.g. "0,1:E".</summary>
        public string Id => $"{LocalCell.X},{LocalCell.Y}:{"NESW"[(int)Side]}";
        public bool Equals(DoorSocket o) => LocalCell.Equals(o.LocalCell) && Side == o.Side;
        public override bool Equals(object obj) => obj is DoorSocket d && Equals(d);
        public override int GetHashCode() => LocalCell.GetHashCode() * 4 + (int)Side;
        public override string ToString() => Id;
    }

    public static class RoomShapes
    {
        public const int CellWidth = 15;   // interior tiles per cell, excluding walls
        public const int CellHeight = 9;

        private static readonly Dictionary<RoomShape, Cell[]> Cells = new Dictionary<RoomShape, Cell[]>
        {
            { RoomShape.Single, new[] { new Cell(0, 0) } },
            { RoomShape.Wide, new[] { new Cell(0, 0), new Cell(1, 0) } },
            { RoomShape.Tall, new[] { new Cell(0, 0), new Cell(0, 1) } },
            { RoomShape.Large, new[] { new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1) } },
            { RoomShape.LNoNE, new[] { new Cell(0, 0), new Cell(0, 1), new Cell(1, 1) } },
            { RoomShape.LNoNW, new[] { new Cell(1, 0), new Cell(0, 1), new Cell(1, 1) } },
            { RoomShape.LNoSE, new[] { new Cell(0, 0), new Cell(1, 0), new Cell(0, 1) } },
            { RoomShape.LNoSW, new[] { new Cell(0, 0), new Cell(1, 0), new Cell(1, 1) } },
        };

        private static readonly Dictionary<RoomShape, DoorSocket[]> Sockets = new Dictionary<RoomShape, DoorSocket[]>();

        public static IReadOnlyList<Cell> GetCells(RoomShape shape) => Cells[shape];

        public static int Width(RoomShape s) => s == RoomShape.Single || s == RoomShape.Tall ? 1 : 2;
        public static int Height(RoomShape s) => s == RoomShape.Single || s == RoomShape.Wide ? 1 : 2;
        public static int TileWidth(RoomShape s) => Width(s) * CellWidth;
        public static int TileHeight(RoomShape s) => Height(s) * CellHeight;

        public static bool Contains(RoomShape shape, Cell local)
        {
            var cells = Cells[shape];
            for (int i = 0; i < cells.Length; i++) if (cells[i].Equals(local)) return true;
            return false;
        }

        /// <summary>All outward-facing door sockets for a shape.</summary>
        public static IReadOnlyList<DoorSocket> GetSockets(RoomShape shape)
        {
            if (Sockets.TryGetValue(shape, out var s)) return s;
            var list = new List<DoorSocket>();
            foreach (var c in Cells[shape])
                for (int d = 0; d < 4; d++)
                    if (!Contains(shape, c.Step((Direction)d))) list.Add(new DoorSocket(c, (Direction)d));
            s = list.ToArray();
            Sockets[shape] = s;
            return s;
        }

        /// <summary>Interior tile just inside a door socket (template coordinates, row 0 = north).</summary>
        public static (int x, int y) DoorTile(DoorSocket socket)
        {
            int ox = socket.LocalCell.X * CellWidth, oy = socket.LocalCell.Y * CellHeight;
            switch (socket.Side)
            {
                case Direction.North: return (ox + CellWidth / 2, oy);
                case Direction.South: return (ox + CellWidth / 2, oy + CellHeight - 1);
                case Direction.West: return (ox, oy + CellHeight / 2);
                default: return (ox + CellWidth - 1, oy + CellHeight / 2);
            }
        }

        public static Direction Opposite(Direction d) => (Direction)(((int)d + 2) % 4);

        public static bool TryParseSocket(string id, out DoorSocket socket)
        {
            socket = default;
            if (string.IsNullOrEmpty(id)) return false;
            var parts = id.Split(':');
            if (parts.Length != 2 || parts[1].Length != 1) return false;
            var xy = parts[0].Split(',');
            if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y)) return false;
            int dir = "NESW".IndexOf(char.ToUpperInvariant(parts[1][0]));
            if (dir < 0) return false;
            socket = new DoorSocket(new Cell(x, y), (Direction)dir);
            return true;
        }
    }
}
