using System;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Combat
{
    /// <summary>
    /// Player health with three original health types, measured in half points:
    ///   Vigor  - refillable health inside containers (MaxHealth stat = number of containers, 2 half points each).
    ///   Ward   - temporary shield shards, absorbed before Vigor, lost for good when spent.
    ///   Umbral - temporary dark shards, absorbed after Ward; each full shard that breaks raises UmbralBurst
    ///            (the gameplay layer damages every enemy in the room).
    /// Damage order: Ward -> Umbral -> Vigor. Healing only refills Vigor.
    /// Defense: each point gives an 8% chance to negate a hit entirely (Defense stat is capped at 6).
    /// </summary>
    public sealed class HealthPool
    {
        public const int MaxTemporaryHalves = 24;

        public int Containers { get; private set; }
        public int Vigor { get; private set; }
        public int Ward { get; private set; }
        public int Umbral { get; private set; }
        public float InvulnerableTime { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Default invulnerability window after taking damage, in seconds.</summary>
        public float HitInvulnerability { get; set; } = 1f;

        public int MaxVigor => Containers * 2;
        public int TotalHalves => Vigor + Ward + Umbral;

        public event Action<int> Damaged;       // half points actually lost
        public event Action<int> Healed;
        public event Action UmbralBurst;
        public event Action HitBlocked;          // negated by defense or invulnerability
        /// <summary>
        /// Raised when a hit would kill. Handlers set <c>args.Prevent = true</c> (and restore health)
        /// to cancel death (e.g. a revive item). Evaluated before Died.
        /// </summary>
        public event Action<LethalHitArgs> LethalHit;
        public event Action Died;
        public event Action Changed;

        public sealed class LethalHitArgs { public bool Prevent; }

        public HealthPool(int containers, int vigor, int ward = 0, int umbral = 0)
        {
            Containers = Math.Max(0, containers);
            Vigor = Clamp(vigor, 0, MaxVigor);
            Ward = Clamp(ward, 0, MaxTemporaryHalves);
            Umbral = Clamp(umbral, 0, MaxTemporaryHalves);
        }

        public void Tick(float dt)
        {
            if (InvulnerableTime > 0f) InvulnerableTime = Math.Max(0f, InvulnerableTime - dt);
        }

        public void GrantInvulnerability(float seconds)
        {
            if (seconds > InvulnerableTime) InvulnerableTime = seconds;
        }

        /// <summary>Applies damage in half points. Returns half points actually removed.</summary>
        public int TakeDamage(int halves, float defense, SeededRandom rng, bool ignoreInvulnerability = false)
        {
            if (IsDead || halves <= 0) return 0;
            if (!ignoreInvulnerability && InvulnerableTime > 0f) { HitBlocked?.Invoke(); return 0; }
            if (defense > 0f && rng != null && rng.Chance(Math.Min(0.48f, defense * 0.08f)))
            {
                GrantInvulnerability(HitInvulnerability * 0.5f);
                HitBlocked?.Invoke();
                return 0;
            }

            int remaining = halves;
            int fromWard = Math.Min(Ward, remaining); Ward -= fromWard; remaining -= fromWard;

            int umbralBefore = Umbral;
            int fromUmbral = Math.Min(Umbral, remaining); Umbral -= fromUmbral; remaining -= fromUmbral;
            int brokenShards = (umbralBefore + 1) / 2 - (Umbral + 1) / 2; // a shard breaks when its last half is lost

            int fromVigor = Math.Min(Vigor, remaining); Vigor -= fromVigor; remaining -= fromVigor;

            int lost = fromWard + fromUmbral + fromVigor;
            GrantInvulnerability(HitInvulnerability);
            for (int i = 0; i < brokenShards; i++) UmbralBurst?.Invoke();
            Damaged?.Invoke(lost);

            if (TotalHalves <= 0)
            {
                var args = new LethalHitArgs();
                LethalHit?.Invoke(args);
                if (!args.Prevent || TotalHalves <= 0)
                {
                    IsDead = true;
                    Changed?.Invoke();
                    Died?.Invoke();
                    return lost;
                }
            }
            Changed?.Invoke();
            return lost;
        }

        public int Heal(int halves)
        {
            if (IsDead || halves <= 0) return 0;
            int healed = Math.Min(halves, MaxVigor - Vigor);
            if (healed <= 0) return 0;
            Vigor += healed;
            Healed?.Invoke(healed);
            Changed?.Invoke();
            return healed;
        }

        public void AddWard(int halves) { Ward = Clamp(Ward + halves, 0, MaxTemporaryHalves); Changed?.Invoke(); }
        public void AddUmbral(int halves) { Umbral = Clamp(Umbral + halves, 0, MaxTemporaryHalves); Changed?.Invoke(); }

        /// <summary>Sets container count (from the MaxHealth stat). New containers arrive filled when fillNew is true.</summary>
        public void SetContainers(int containers, bool fillNew)
        {
            containers = Math.Max(0, containers);
            int delta = containers - Containers;
            Containers = containers;
            if (delta > 0 && fillNew) Vigor += delta * 2;
            Vigor = Clamp(Vigor, 0, MaxVigor);
            Changed?.Invoke();
            if (TotalHalves <= 0 && !IsDead) { IsDead = true; Died?.Invoke(); }
        }

        /// <summary>Revive helper for lethal-hit handlers.</summary>
        public void Restore(int vigorHalves)
        {
            IsDead = false;
            Vigor = Clamp(vigorHalves, 1, Math.Max(2, MaxVigor));
            if (Containers == 0) Containers = 1;
            Changed?.Invoke();
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
