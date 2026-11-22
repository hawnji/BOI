using System;
using System.Collections.Generic;
using Hollowcrawl.Items;

namespace Hollowcrawl.Combat
{
    /// <summary>
    /// Fire-rate cooldown, charge shots and volley spreading. Pure logic: the Unity shooter asks
    /// <see cref="Update"/> each frame and spawns the projectiles it returns.
    /// </summary>
    public sealed class FireController
    {
        private float _cooldown;
        private float _charge;
        private bool _wasHeld;

        public float ChargeFraction { get; private set; }
        public bool IsCharging => _charge > 0f;

        /// <summary>
        /// Advances timers. Returns true when a volley should be fired; <paramref name="chargeFraction"/> is the
        /// charge level for charged shots (1 for normal shots).
        /// </summary>
        public bool Update(float dt, bool wantsToFire, float fireRate, float chargeTime, out float chargeFraction)
        {
            chargeFraction = 1f;
            if (_cooldown > 0f) _cooldown -= dt;
            float interval = 1f / Math.Max(0.1f, fireRate);

            if (chargeTime > 0f)
            {
                // Hold to charge, release to fire. Charge speed scales with fire rate (faster tears = faster charge).
                float chargeDuration = chargeTime * Math.Max(0.25f, 2.73f / Math.Max(0.1f, fireRate));
                if (wantsToFire && _cooldown <= 0f)
                {
                    _charge = Math.Min(chargeDuration, _charge + dt);
                    ChargeFraction = _charge / chargeDuration;
                    _wasHeld = true;
                    return false;
                }
                if (_wasHeld && !wantsToFire && _charge > 0f)
                {
                    chargeFraction = ChargeFraction;
                    _charge = 0f; ChargeFraction = 0f; _wasHeld = false;
                    _cooldown = interval * 0.5f;
                    return chargeFraction >= 0.15f; // tiny taps fizzle
                }
                _wasHeld = wantsToFire;
                return false;
            }

            _charge = 0f; ChargeFraction = 0f; _wasHeld = wantsToFire;
            if (!wantsToFire || _cooldown > 0f) return false;
            _cooldown += interval;
            if (_cooldown < 0f) _cooldown = 0f; // don't bank shots while idle
            return true;
        }

        public void Reset() { _cooldown = 0f; _charge = 0f; ChargeFraction = 0f; _wasHeld = false; }

        /// <summary>Directions for a volley of <paramref name="shots"/> spread over <paramref name="spreadDegrees"/> between neighbours, centred on aim.</summary>
        public static void VolleyDirections(Vec2 aim, int shots, float spreadDegrees, List<Vec2> results)
        {
            results.Clear();
            aim = aim.Length < 1e-5f ? new Vec2(0f, -1f) : aim.Normalized;
            shots = Math.Max(1, Math.Min(ProjectileSpec.MaxShotsPerVolley, shots));
            if (shots == 1) { results.Add(aim); return; }
            float spread = spreadDegrees <= 0f ? 8f : spreadDegrees;
            float total = spread * (shots - 1);
            if (total > 330f) { spread = 330f / (shots - 1); total = 330f; }
            for (int i = 0; i < shots; i++) results.Add(aim.Rotated(-total * 0.5f + spread * i));
        }
    }
}
