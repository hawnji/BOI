using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Enemies
{
    /// <summary>Directions for enemy projectile patterns. Pure and allocation-free (fills a caller list).</summary>
    public static class AttackPatterns
    {
        public static void Directions(ShotPattern pattern, Vec2 aim, int count, float spread, float spiralAngle, SeededRandom rng, List<Vec2> results)
        {
            results.Clear();
            aim = aim.Length < 1e-5f ? new Vec2(0f, -1f) : aim.Normalized;
            count = Math.Max(1, Math.Min(32, count));
            switch (pattern)
            {
                case ShotPattern.Aimed:
                    for (int i = 0; i < count; i++) results.Add(aim);
                    break;
                case ShotPattern.Spread:
                {
                    float total = spread * (count - 1);
                    for (int i = 0; i < count; i++) results.Add(aim.Rotated(-total * 0.5f + spread * i));
                    break;
                }
                case ShotPattern.Ring:
                    for (int i = 0; i < count; i++) results.Add(aim.Rotated(360f / count * i));
                    break;
                case ShotPattern.Spiral:
                    for (int i = 0; i < count; i++) results.Add(new Vec2(1f, 0f).Rotated(spiralAngle + 360f / count * i));
                    break;
                case ShotPattern.Cross:
                    for (int i = 0; i < 4; i++) results.Add(new Vec2(1f, 0f).Rotated(90f * i));
                    break;
                case ShotPattern.X:
                    for (int i = 0; i < 4; i++) results.Add(new Vec2(1f, 0f).Rotated(45f + 90f * i));
                    break;
                case ShotPattern.RandomBurst:
                    for (int i = 0; i < count; i++) results.Add(aim.Rotated(rng.Range(-spread, spread)));
                    break;
                case ShotPattern.Wall:
                    // parallel shots: same direction; the shooter offsets origins perpendicular to aim
                    for (int i = 0; i < count; i++) results.Add(aim);
                    break;
            }
        }

        /// <summary>Perpendicular origin offset for Wall patterns (index i of count, spacing in units).</summary>
        public static Vec2 WallOffset(Vec2 aim, int i, int count, float spacing)
        {
            var perp = new Vec2(-aim.Y, aim.X).Normalized;
            return perp * ((i - (count - 1) * 0.5f) * spacing);
        }
    }
}
