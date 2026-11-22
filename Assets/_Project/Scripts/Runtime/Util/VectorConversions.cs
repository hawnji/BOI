using Hollowcrawl.Items;
using UnityEngine;

namespace Hollowcrawl.Util
{
    /// <summary>Conversions between the engine-free Vec2 used by Core and UnityEngine.Vector2.</summary>
    public static class VectorConversions
    {
        public static Vector2 ToUnity(this Vec2 v) => new Vector2(v.X, v.Y);
        public static Vector3 ToUnity3(this Vec2 v) => new Vector3(v.X, v.Y, 0f);
        public static Vec2 ToCore(this Vector2 v) => new Vec2(v.x, v.y);
        public static Vec2 ToCore(this Vector3 v) => new Vec2(v.x, v.y);
    }
}
