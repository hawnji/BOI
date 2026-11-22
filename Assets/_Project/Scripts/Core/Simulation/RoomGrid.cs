using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;

namespace Hollowcrawl.Simulation
{
    public enum TileKind : byte
    {
        Floor, Void, Rock, Block, Pit, Spikes, Prop, MarkedRock, Fire, Water, Explosive,
        Wall, DoorOpen, DoorClosed,
    }

    /// <summary>
    /// The collision grid of one room. World units are tiles. The interior spans x in [0, Width] and
    /// y in [0, Height] with y pointing up (north); template rows count down from the north edge.
    /// A one-tile border holds walls and door openings, so leaving through a door is just walking into
    /// a DoorOpen tile.
    /// </summary>
    public sealed class RoomGrid
    {
        public int Width { get; }
        public int Height { get; }
        private readonly TileKind[] _tiles;
        private readonly byte[] _hp;

        public const int PropHitPoints = 3;
        public const int FireHitPoints = 4;

        public RoomGrid(int width, int height)
        {
            Width = width; Height = height;
            _tiles = new TileKind[(width + 2) * (height + 2)];
            _hp = new byte[_tiles.Length];
            for (int row = -1; row <= height; row++)
                for (int x = -1; x <= width; x++)
                    Set(x, row, x < 0 || row < 0 || x >= width || row >= height ? TileKind.Wall : TileKind.Floor);
        }

        public static RoomGrid FromTemplate(RoomTemplate template, RoomShape shape)
        {
            var g = new RoomGrid(RoomShapes.TileWidth(shape), RoomShapes.TileHeight(shape));
            for (int row = 0; row < g.Height; row++)
                for (int x = 0; x < g.Width; x++)
                {
                    var cell = new Cell(x / RoomShapes.CellWidth, row / RoomShapes.CellHeight);
                    if (!RoomShapes.Contains(shape, cell)) { g.Set(x, row, TileKind.Void); continue; }
                    g.Set(x, row, template == null ? TileKind.Floor : FromCode(template.TileAt(x, row)));
                }
            return g;
        }

        public static TileKind FromCode(char c)
        {
            switch (c)
            {
                case TileCodes.Void: return TileKind.Void;
                case TileCodes.Rock: return TileKind.Rock;
                case TileCodes.Block: return TileKind.Block;
                case TileCodes.Pit: return TileKind.Pit;
                case TileCodes.Spikes: return TileKind.Spikes;
                case TileCodes.Prop: return TileKind.Prop;
                case TileCodes.MarkedRock: return TileKind.MarkedRock;
                case TileCodes.Fire: return TileKind.Fire;
                case TileCodes.Water: return TileKind.Water;
                case TileCodes.Explosive: return TileKind.Explosive;
                default: return TileKind.Floor; // floor and spawn markers
            }
        }

        private int Index(int x, int row) => (row + 1) * (Width + 2) + (x + 1);
        private bool InRange(int x, int row) => x >= -1 && row >= -1 && x <= Width && row <= Height;

        public TileKind Get(int x, int row) => InRange(x, row) ? _tiles[Index(x, row)] : TileKind.Wall;

        public void Set(int x, int row, TileKind kind)
        {
            if (!InRange(x, row)) return;
            int i = Index(x, row);
            _tiles[i] = kind;
            _hp[i] = (byte)(kind == TileKind.Prop ? PropHitPoints : kind == TileKind.Fire ? FireHitPoints : 0);
        }

        // ------------------------------------------------------------------ coordinates

        public Vec2 TileCenter(int x, int row) => new Vec2(x + 0.5f, Height - row - 0.5f);

        public void WorldToTile(Vec2 p, out int x, out int row)
        {
            x = (int)Math.Floor(p.X);
            row = Height - 1 - (int)Math.Floor(p.Y);
        }

        public TileKind At(Vec2 p) { WorldToTile(p, out int x, out int row); return Get(x, row); }

        public Vec2 Center => new Vec2(Width * 0.5f, Height * 0.5f);

        // ------------------------------------------------------------------ doors

        /// <summary>Border tile of a door socket (outside the interior, possibly inside an L-shape's void corner).</summary>
        public static void DoorTile(DoorSocket s, out int x, out int row)
        {
            int cx = s.LocalCell.X * RoomShapes.CellWidth, cy = s.LocalCell.Y * RoomShapes.CellHeight;
            switch (s.Side)
            {
                case Direction.North: x = cx + RoomShapes.CellWidth / 2; row = cy - 1; break;
                case Direction.South: x = cx + RoomShapes.CellWidth / 2; row = cy + RoomShapes.CellHeight; break;
                case Direction.West: x = cx - 1; row = cy + RoomShapes.CellHeight / 2; break;
                default: x = cx + RoomShapes.CellWidth; row = cy + RoomShapes.CellHeight / 2; break;
            }
        }

        /// <summary>The interior tile just inside a door, where an entering player appears.</summary>
        public static void DoorEntryTile(DoorSocket s, out int x, out int row)
        {
            DoorTile(s, out x, out row);
            switch (s.Side)
            {
                case Direction.North: row += 1; break;
                case Direction.South: row -= 1; break;
                case Direction.West: x += 1; break;
                default: x -= 1; break;
            }
        }

        public void SetDoor(DoorSocket s, bool open)
        {
            DoorTile(s, out int x, out int row);
            Set(x, row, open ? TileKind.DoorOpen : TileKind.DoorClosed);
            DoorEntryTile(s, out int ex, out int er);
            var inner = Get(ex, er);
            if (inner != TileKind.Floor && inner != TileKind.Water) Set(ex, er, TileKind.Floor); // templates must never wall in a door
        }

        // ------------------------------------------------------------------ rules

        public static bool BlocksWalker(TileKind k) =>
            k == TileKind.Wall || k == TileKind.Void || k == TileKind.DoorClosed || k == TileKind.Rock || k == TileKind.Block ||
            k == TileKind.Pit || k == TileKind.Prop || k == TileKind.MarkedRock || k == TileKind.Explosive;

        public static bool BlocksFlyer(TileKind k) => k == TileKind.Wall || k == TileKind.Void || k == TileKind.DoorClosed;

        public static bool BlocksMovement(TileKind k, bool flying) => flying ? BlocksFlyer(k) : BlocksWalker(k);

        /// <summary>Obstacles stop non-spectral shots; boundaries (walls, doors, void) stop every shot.</summary>
        public static bool BlocksShot(TileKind k, out bool boundary)
        {
            boundary = k == TileKind.Wall || k == TileKind.Void || k == TileKind.DoorClosed || k == TileKind.DoorOpen;
            return boundary || k == TileKind.Rock || k == TileKind.Block || k == TileKind.Prop || k == TileKind.MarkedRock || k == TileKind.Explosive;
        }

        public static bool IsBombable(TileKind k) =>
            k == TileKind.Rock || k == TileKind.Prop || k == TileKind.MarkedRock || k == TileKind.Explosive || k == TileKind.Fire;

        public bool IsWalkable(int x, int row) => !BlocksWalker(Get(x, row)) && Get(x, row) != TileKind.Spikes && Get(x, row) != TileKind.Fire;

        /// <summary>Damages a shootable tile (props, fires). Returns true when it breaks.</summary>
        public bool DamageTile(int x, int row)
        {
            if (!InRange(x, row)) return false;
            int i = Index(x, row);
            var k = _tiles[i];
            if (k != TileKind.Prop && k != TileKind.Fire) return false;
            if (_hp[i] > 1) { _hp[i]--; return false; }
            Set(x, row, TileKind.Floor);
            return true;
        }

        // ------------------------------------------------------------------ collision

        /// <summary>Moves a circle by delta, sliding along blocking tiles. Sub-steps so fast movers can't tunnel.</summary>
        public Vec2 MoveCircle(Vec2 pos, float radius, Vec2 delta, bool flying, out bool collided)
        {
            collided = false;
            float len = delta.Length;
            int steps = Math.Max(1, (int)Math.Ceiling(len / 0.2f));
            var stepDelta = delta * (1f / steps);
            for (int s = 0; s < steps; s++)
            {
                pos = pos + stepDelta;
                for (int pass = 0; pass < 2; pass++)
                    if (ResolveOverlaps(ref pos, radius, flying)) collided = true;
            }
            return pos;
        }

        private bool ResolveOverlaps(ref Vec2 p, float r, bool flying)
        {
            bool any = false;
            int x0 = (int)Math.Floor(p.X - r), x1 = (int)Math.Floor(p.X + r);
            int y0 = (int)Math.Floor(p.Y - r), y1 = (int)Math.Floor(p.Y + r);
            for (int wy = y0; wy <= y1; wy++)
                for (int x = x0; x <= x1; x++)
                {
                    int row = Height - 1 - wy;
                    if (!BlocksMovement(Get(x, row), flying)) continue;
                    float cx = Math.Max(x, Math.Min(x + 1, p.X));
                    float cy = Math.Max(wy, Math.Min(wy + 1, p.Y));
                    float dx = p.X - cx, dy = p.Y - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= r * r) continue;
                    any = true;
                    if (d2 > 1e-8f)
                    {
                        float d = (float)Math.Sqrt(d2);
                        p = p + new Vec2(dx / d, dy / d) * (r - d);
                    }
                    else
                    {
                        // centre inside the tile: leave through the nearest face
                        float left = p.X - x, right = x + 1 - p.X, down = p.Y - wy, up = wy + 1 - p.Y;
                        float m = Math.Min(Math.Min(left, right), Math.Min(down, up));
                        if (m == left) p = new Vec2(x - r, p.Y);
                        else if (m == right) p = new Vec2(x + 1 + r, p.Y);
                        else if (m == down) p = new Vec2(p.X, wy - r);
                        else p = new Vec2(p.X, wy + 1 + r);
                    }
                }
            return any;
        }

        public bool CircleOverlapsKind(Vec2 p, float r, TileKind kind)
        {
            int x0 = (int)Math.Floor(p.X - r), x1 = (int)Math.Floor(p.X + r);
            int y0 = (int)Math.Floor(p.Y - r), y1 = (int)Math.Floor(p.Y + r);
            for (int wy = y0; wy <= y1; wy++)
                for (int x = x0; x <= x1; x++)
                {
                    if (Get(x, Height - 1 - wy) != kind) continue;
                    float cx = Math.Max(x, Math.Min(x + 1, p.X)), cy = Math.Max(wy, Math.Min(wy + 1, p.Y));
                    float dx = p.X - cx, dy = p.Y - cy;
                    if (dx * dx + dy * dy < r * r) return true;
                }
            return false;
        }

        /// <summary>True when nothing that blocks shots lies between a and b.</summary>
        public bool LineOfSight(Vec2 a, Vec2 b)
        {
            var d = b - a;
            float len = d.Length;
            int steps = Math.Max(1, (int)(len / 0.25f));
            for (int i = 1; i < steps; i++)
            {
                var p = a + d * (i / (float)steps);
                if (BlocksShot(At(p), out _)) return false;
            }
            return true;
        }

        /// <summary>Approximate surface normal when a point moving from prev to p entered a blocking tile.</summary>
        public Vec2 HitNormal(Vec2 prev, Vec2 p)
        {
            WorldToTile(prev, out int px, out int pr);
            WorldToTile(p, out int x, out int r);
            float nx = x > px ? -1f : x < px ? 1f : 0f;
            float ny = r > pr ? 1f : r < pr ? -1f : 0f; // rows grow southward, y grows north
            if (nx != 0f && ny != 0f)
            {
                // corner: keep the axis whose neighbour is open
                if (BlocksShot(Get(x, pr), out _)) ny = 0f; else nx = 0f;
            }
            return new Vec2(nx, ny);
        }

        public void ForEachTileInRadius(Vec2 center, float radius, Action<int, int> visit)
        {
            int x0 = (int)Math.Floor(center.X - radius), x1 = (int)Math.Floor(center.X + radius);
            int y0 = (int)Math.Floor(center.Y - radius), y1 = (int)Math.Floor(center.Y + radius);
            for (int wy = y0; wy <= y1; wy++)
                for (int x = x0; x <= x1; x++)
                {
                    float cx = Math.Max(x, Math.Min(x + 1, center.X)), cy = Math.Max(wy, Math.Min(wy + 1, center.Y));
                    float dx = center.X - cx, dy = center.Y - cy;
                    if (dx * dx + dy * dy <= radius * radius) visit(x, Height - 1 - wy);
                }
        }

        /// <summary>Random walkable interior tile at least minDistance from a point (falls back to any walkable tile).</summary>
        public bool TryFindOpenTile(Rng.SeededRandom rng, Vec2 awayFrom, float minDistance, out Vec2 position)
        {
            var open = new List<Vec2>();
            var far = new List<Vec2>();
            for (int row = 0; row < Height; row++)
                for (int x = 0; x < Width; x++)
                {
                    if (!IsWalkable(x, row)) continue;
                    var c = TileCenter(x, row);
                    open.Add(c);
                    if ((c - awayFrom).Length >= minDistance) far.Add(c);
                }
            var pool = far.Count > 0 ? far : open;
            if (pool.Count == 0) { position = Center; return false; }
            position = pool[rng.Range(0, pool.Count)];
            return true;
        }
    }
}
