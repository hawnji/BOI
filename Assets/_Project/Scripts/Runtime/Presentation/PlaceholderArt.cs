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

        private static readonly Dictionary<string, Sprite> ArtCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Loads pre-rendered art (Tools/ArtGen) as a sprite exactly one world unit wide, whatever the import
        /// settings: imported sprites are re-wrapped at the right pixels-per-unit, plain textures are wrapped too.
        /// </summary>
        public static Sprite LoadArt(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (ArtCache.TryGetValue(key, out var cached)) return cached;
            Sprite result = null;
            var sprite = Resources.Load<Sprite>("Sprites/" + key);
            if (sprite != null)
            {
                var r = sprite.rect;
                result = Mathf.Approximately(sprite.pixelsPerUnit, r.width) ? sprite
                    : Sprite.Create(sprite.texture, r, new Vector2(0.5f, 0.5f), r.width, 0, SpriteMeshType.FullRect);
            }
            else
            {
                var tex = Resources.Load<Texture2D>("Sprites/" + key);
                if (tex != null) result = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
            }
            ArtCache[key] = result;
            return result;
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
