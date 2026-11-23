using System.Collections.Generic;
using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Sprite lookup with generated placeholders. A real sprite at Resources/Sprites/&lt;key&gt; always wins,
    /// so art can be dropped in later without code changes. Placeholders are flat shapes coloured per
    /// category so every actor is readable before art exists.
    /// </summary>
    public static class PlaceholderArt
    {
        public const int PixelsPerUnit = 32;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Sprite _circle, _square, _diamond;

        public static Sprite Get(string key, ActorKind kind)
        {
            key = string.IsNullOrEmpty(key) ? kind.ToString() : key;
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = LoadArt(key);
            if (s == null) s = kind == ActorKind.Pickup || kind == ActorKind.Exit ? Diamond : kind == ActorKind.Bomb ? Square : Circle;
            Cache[key] = s;
            return s;
        }

        /// <summary>Tint for placeholder sprites (white when a real sprite was found).</summary>
        public static Color Tint(string key, ActorKind kind)
        {
            if (LoadArt(key) != null) return Color.white;
            switch (kind)
            {
                case ActorKind.Player: return new Color(0.95f, 0.9f, 0.75f);
                case ActorKind.PlayerShot: return new Color(0.6f, 0.85f, 1f);
                case ActorKind.EnemyShot: return new Color(1f, 0.35f, 0.3f);
                case ActorKind.Bomb: return new Color(0.2f, 0.2f, 0.22f);
                case ActorKind.Hazard: return new Color(1f, 0.5f, 0.1f, 0.45f);
                case ActorKind.Companion: return new Color(0.7f, 1f, 0.7f);
                case ActorKind.Exit: return new Color(0.1f, 0.1f, 0.1f);
                case ActorKind.Pickup: return PickupTint(key);
                default: return FromHash(key);
            }
        }

        /// <summary>Pixels per world unit of all pixel art (one dungeon tile = 32 pixels).</summary>
        public const int ArtPixelsPerUnit = 32;

        private static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>();

        /// <summary>
        /// Loads an animation strip from Resources/Sprites/&lt;key&gt;: a horizontal row of square frames
        /// (frame size = image height; a square image is a single frame). Pixel art is drawn at native size,
        /// 32 pixels per world unit, with point filtering. Returns null when no art exists for the key.
        /// </summary>
        public static Sprite[] LoadFrames(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (FrameCache.TryGetValue(key, out var cached)) return cached;
            Sprite[] frames = null;
            var tex = Resources.Load<Texture2D>("Sprites/" + key);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                int size = tex.height, count = Mathf.Max(1, tex.width / size);
                frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                    frames[i] = Sprite.Create(tex, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), ArtPixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            FrameCache[key] = frames;
            return frames;
        }

        /// <summary>First frame of the art for a key (or null).</summary>
        public static Sprite LoadArt(string key)
        {
            var f = LoadFrames(key);
            return f != null && f.Length > 0 ? f[0] : null;
        }

        private static Color PickupTint(string key)
        {
            if (key.Contains("coin")) return new Color(1f, 0.85f, 0.2f);
            if (key.Contains("vigor")) return new Color(0.9f, 0.15f, 0.2f);
            if (key.Contains("ward")) return new Color(0.5f, 0.7f, 1f);
            if (key.Contains("umbral")) return new Color(0.35f, 0.2f, 0.5f);
            if (key.Contains("bomb")) return new Color(0.25f, 0.25f, 0.28f);
            if (key.Contains("key")) return new Color(0.8f, 0.8f, 0.85f);
            if (key.Contains("pedestal")) return new Color(0.75f, 0.6f, 1f);
            return FromHash(key);
        }

        private static Color FromHash(string key)
        {
            uint h = 2166136261;
            foreach (char c in key) { h ^= c; h *= 16777619; }
            float hue = (h % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }

        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(Shape.Circle));
        public static Sprite Square => _square != null ? _square : (_square = Make(Shape.Square));
        public static Sprite Diamond => _diamond != null ? _diamond : (_diamond = Make(Shape.Diamond));

        private enum Shape { Circle, Square, Diamond }

        private static Sprite Make(Shape shape)
        {
            const int n = PixelsPerUnit;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    bool inside = shape == Shape.Circle ? dx * dx + dy * dy <= 1f : shape == Shape.Diamond ? Mathf.Abs(dx) + Mathf.Abs(dy) <= 1f : Mathf.Abs(dx) <= 0.9f && Mathf.Abs(dy) <= 0.9f;
                    bool rim = shape == Shape.Circle ? dx * dx + dy * dy > 0.72f : shape == Shape.Diamond ? Mathf.Abs(dx) + Mathf.Abs(dy) > 0.85f : Mathf.Abs(dx) > 0.78f || Mathf.Abs(dy) > 0.78f;
                    byte v = rim ? (byte)170 : (byte)255;
                    px[y * n + x] = inside ? new Color32(v, v, v, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            // one sprite unit = diameter 1, so actors scale by 2 * radius
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
