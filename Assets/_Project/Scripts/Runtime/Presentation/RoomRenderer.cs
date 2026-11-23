using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Draws a room's tile grid into a single texture (placeholder look until tilesets exist). Rebuilt only
    /// when a tile or door changes, so it costs nothing per frame.
    /// </summary>
    public sealed class RoomRenderer : MonoBehaviour
    {
        public const int PixelsPerTile = 32;
        private const int PlaceholderScale = PixelsPerTile / 16;
        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private RoomGrid _grid;
        public string Theme = "crypt";

        // Art hook: a readable texture at Resources/Tiles/<theme>/<kind> (or Resources/Tiles/<kind>) replaces the
        // generated colour for that tile kind; any square size is resampled to PixelsPerTile (the pixel art from
        // Tools/PixelArt is 32x32). Kinds are TileKind names in lower case (floor, rock, wall, door_open...).
        private static readonly System.Collections.Generic.Dictionary<string, Color32[][]> TileArt = new System.Collections.Generic.Dictionary<string, Color32[][]>();

        public static string TileKey(TileKind k)
        {
            switch (k)
            {
                case TileKind.MarkedRock: return "marked_rock";
                case TileKind.DoorOpen: return "door_open";
                case TileKind.DoorClosed: return "door_closed";
                default: return k.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Art for a tile kind at a grid cell. A strip of N square variants picks one per cell (stable hash).</summary>
        private Color32[] ArtFor(TileKind k, int x, int y)
        {
            string key = Theme + "/" + TileKey(k);
            if (!TileArt.TryGetValue(key, out var variants))
            {
                variants = Load("Tiles/" + key) ?? Load("Tiles/" + TileKey(k));
                TileArt[key] = variants;
            }
            if (variants == null) return null;
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9E3779B9u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return variants[(int)(h % (uint)variants.Length)];
        }

        private static Color32[][] Load(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            Color32[] src;
            try { src = tex.GetPixels32(); }
            catch (UnityException) { Debug.LogWarning($"[Art] {path} needs Read/Write enabled in its import settings."); return null; }
            int size = tex.height, count = Mathf.Max(1, tex.width / size);
            var result = new Color32[count][];
            for (int v = 0; v < count; v++)
            {
                var dst = new Color32[PixelsPerTile * PixelsPerTile];
                for (int y = 0; y < PixelsPerTile; y++)
                    for (int x = 0; x < PixelsPerTile; x++)
                        dst[y * PixelsPerTile + x] = src[(y * size / PixelsPerTile) * tex.width + v * size + x * size / PixelsPerTile];
                result[v] = dst;
            }
            return result;
        }

        public static RoomRenderer Create(Transform parent)
        {
            var go = new GameObject("Room");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RoomRenderer>();
            r._renderer = go.AddComponent<SpriteRenderer>();
            r._renderer.sortingOrder = -100;
            return r;
        }

        public void Show(RoomGrid grid)
        {
            _grid = grid;
            int w = (grid.Width + 2) * PixelsPerTile, h = (grid.Height + 2) * PixelsPerTile;
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) Destroy(_texture);
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }
            Redraw();
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, w, h), new Vector2(0f, 0f), PixelsPerTile);
            // texture origin is the outer wall corner at (-1, -1) in room units
            transform.localPosition = new Vector3(-1f, -1f, 0f);
        }

        public void Redraw()
        {
            if (_grid == null) return;
            int w = _grid.Width + 2, h = _grid.Height + 2;
            var px = new Color32[w * PixelsPerTile * h * PixelsPerTile];
            for (int row = -1; row <= _grid.Height; row++)
                for (int x = -1; x <= _grid.Width; x++)
                {
                    var kind = _grid.Get(x, row);
                    int ty = _grid.Height - row; // texture rows grow upward; +1 border offset already in (H - row)
                    int tx = x + 1;
                    var art = ArtFor(kind, x, row);
                    // Door art is modelled for the top/bottom walls; turn it a quarter for the side walls.
                    bool sideDoor = (kind == TileKind.DoorOpen || kind == TileKind.DoorClosed) && (x == -1 || x == _grid.Width);
                    for (int py = 0; py < PixelsPerTile; py++)
                        for (int pxl = 0; pxl < PixelsPerTile; pxl++)
                        {
                            int sx = pxl / PlaceholderScale, sy = py / PlaceholderScale;
                            bool edge = sx == 0 || sy == 0;
                            int ax = sideDoor ? py : pxl, ay = sideDoor ? PixelsPerTile - 1 - pxl : py;
                            var c = art != null ? art[ay * PixelsPerTile + ax] : TileColor(kind, (x + row) & 1, edge, sx, sy);
                            px[(ty * PixelsPerTile + py) * w * PixelsPerTile + tx * PixelsPerTile + pxl] = c;
                        }
                }
            _texture.SetPixels32(px);
            _texture.Apply();
        }

        private static Color32 TileColor(TileKind k, int checker, bool edge, int px, int py)
        {
            byte shade = (byte)(checker == 0 ? 0 : 6);
            switch (k)
            {
                case TileKind.Floor: return new Color32((byte)(52 + shade), (byte)(44 + shade), (byte)(40 + shade), 255);
                case TileKind.Water: return new Color32(40, 70, (byte)(110 + shade), 255);
                case TileKind.Wall: return edge ? new Color32(30, 26, 26, 255) : new Color32(70, 60, 58, 255);
                case TileKind.Void: return new Color32(10, 9, 10, 255);
                case TileKind.Rock: return edge ? new Color32(60, 58, 60, 255) : new Color32(110, 104, 100, 255);
                case TileKind.MarkedRock: return px > 5 && px < 10 && py > 5 && py < 10 ? new Color32(220, 190, 80, 255) : new Color32(110, 104, 100, 255);
                case TileKind.Block: return edge ? new Color32(40, 40, 46, 255) : new Color32(84, 84, 96, 255);
                case TileKind.Pit: return new Color32(6, 5, 6, 255);
                case TileKind.Spikes: return (px + py) % 4 == 0 ? new Color32(200, 200, 210, 255) : new Color32(60, 50, 48, 255);
                case TileKind.Prop: return edge ? new Color32(70, 45, 25, 255) : new Color32(140, 95, 50, 255);
                case TileKind.Explosive: return edge ? new Color32(90, 20, 20, 255) : new Color32(190, 50, 40, 255);
                case TileKind.Fire: return (px * 7 + py * 3) % 5 < 2 ? new Color32(255, 200, 60, 255) : new Color32(220, 90, 30, 255);
                case TileKind.DoorOpen: return new Color32(24, 20, 20, 255);
                case TileKind.DoorClosed: return edge ? new Color32(60, 35, 20, 255) : new Color32(120, 75, 40, 255);
                default: return new Color32(255, 0, 255, 255);
            }
        }
    }
}
