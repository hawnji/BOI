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
        public const int PixelsPerTile = 16;
        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private RoomGrid _grid;
        public string Theme = "crypt";

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
                    for (int py = 0; py < PixelsPerTile; py++)
                        for (int pxl = 0; pxl < PixelsPerTile; pxl++)
                        {
                            bool edge = pxl == 0 || py == 0;
                            var c = TileColor(kind, (x + row) & 1, edge, pxl, py);
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
