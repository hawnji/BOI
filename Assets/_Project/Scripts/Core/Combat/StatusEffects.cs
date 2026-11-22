using System;
using System.Collections.Generic;

namespace Hollowcrawl.Combat
{
    public enum StatusStacking
    {
        /// <summary>Re-applying resets the duration; potency keeps the stronger value.</summary>
        Refresh = 0,
        /// <summary>Each application adds a stack (up to MaxStacks); potency scales with stacks.</summary>
        Intensify = 1,
        /// <summary>Re-applying adds to the remaining duration (up to MaxDuration).</summary>
        Extend = 2,
    }

    /// <summary>Data definition of a status effect (loaded from content).</summary>
    public sealed class StatusEffectDefinition
    {
        public string Id;
        public string DisplayName;
        public DamageType DamageType = DamageType.None;
        public float TickInterval = 0.5f;
        /// <summary>Damage per tick per unit of potency.</summary>
        public float DamagePerTick;
        /// <summary>Movement multiplier while active (1 = no slow, 0 = frozen in place).</summary>
        public float MoveMultiplier = 1f;
        /// <summary>Incoming damage multiplier while active (e.g. 1.25 for "brittle").</summary>
        public float DamageTakenMultiplier = 1f;
        public bool PreventsAttacking;
        public bool CanAffectBosses = true;
        public StatusStacking Stacking = StatusStacking.Refresh;
        public int MaxStacks = 1;
        public float MaxDuration = 10f;
        /// <summary>Status ids this one removes when applied (e.g. Burn removes Chill).</summary>
        public string[] Cancels = Array.Empty<string>();
    }

    public sealed class ActiveStatus
    {
        public StatusEffectDefinition Definition;
        public float Remaining;
        public float Potency;
        public int Stacks;
        public float TickTimer;
        public int SourceId;
    }

    /// <summary>
    /// Pure status-effect state for one entity. Ticks deterministically from Tick(dt); the owning
    /// MonoBehaviour applies the resulting damage through its health component.
    /// </summary>
    public sealed class StatusController
    {
        private readonly List<ActiveStatus> _active = new List<ActiveStatus>(4);
        private readonly bool _isBoss;

        public event Action<ActiveStatus> Applied;
        public event Action<ActiveStatus> Expired;
        /// <summary>Raised for each DoT tick: the damage to apply.</summary>
        public event Action<DamageInfo> DamageTick;

        public StatusController(bool isBoss = false) { _isBoss = isBoss; }

        public IReadOnlyList<ActiveStatus> Active => _active;

        public bool Has(string id) => Find(id) != null;

        public float MoveMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _active.Count; i++) m = Math.Min(m, _active[i].Definition.MoveMultiplier);
                return m;
            }
        }

        public float DamageTakenMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _active.Count; i++) m *= _active[i].Definition.DamageTakenMultiplier;
                return Math.Min(m, 3f);
            }
        }

        public bool CanAttack
        {
            get
            {
                for (int i = 0; i < _active.Count; i++) if (_active[i].Definition.PreventsAttacking) return false;
                return true;
            }
        }

        public bool Apply(StatusEffectDefinition def, float duration, float potency, int sourceId = 0)
        {
            if (def == null || duration <= 0f) return false;
            if (_isBoss && !def.CanAffectBosses) return false;

            for (int c = 0; c < def.Cancels.Length; c++) Remove(def.Cancels[c]);

            var existing = Find(def.Id);
            if (existing == null)
            {
                existing = new ActiveStatus
                {
                    Definition = def,
                    Remaining = Math.Min(duration, def.MaxDuration),
                    Potency = potency,
                    Stacks = 1,
                    TickTimer = def.TickInterval,
                    SourceId = sourceId,
                };
                _active.Add(existing);
            }
            else
            {
                switch (def.Stacking)
                {
                    case StatusStacking.Refresh:
                        existing.Remaining = Math.Max(existing.Remaining, Math.Min(duration, def.MaxDuration));
                        existing.Potency = Math.Max(existing.Potency, potency);
                        break;
                    case StatusStacking.Intensify:
                        existing.Stacks = Math.Min(existing.Stacks + 1, Math.Max(1, def.MaxStacks));
                        existing.Remaining = Math.Max(existing.Remaining, Math.Min(duration, def.MaxDuration));
                        existing.Potency = Math.Max(existing.Potency, potency);
                        break;
                    case StatusStacking.Extend:
                        existing.Remaining = Math.Min(existing.Remaining + duration, def.MaxDuration);
                        existing.Potency = Math.Max(existing.Potency, potency);
                        break;
                }
            }
            Applied?.Invoke(existing);
            return true;
        }

        public bool Remove(string id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Definition.Id != id) continue;
                var s = _active[i];
                _active.RemoveAt(i);
                Expired?.Invoke(s);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Remove(_active[i].Definition.Id);
        }

        public void Tick(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var s = _active[i];
                var def = s.Definition;
                if (def.DamagePerTick > 0f && def.TickInterval > 0f)
                {
                    s.TickTimer -= dt;
                    int guard = 0;
                    while (s.TickTimer <= 0f && guard++ < 8)
                    {
                        s.TickTimer += def.TickInterval;
                        DamageTick?.Invoke(new DamageInfo
                        {
                            Amount = def.DamagePerTick * s.Potency * s.Stacks,
                            Types = def.DamageType,
                            SourceTeam = Team.Neutral,
                            SourceId = s.SourceId,
                            IsDamageOverTime = true,
                        });
                    }
                }
                s.Remaining -= dt;
                if (s.Remaining <= 0f && i < _active.Count && _active[i] == s)
                {
                    _active.RemoveAt(i);
                    Expired?.Invoke(s);
                }
            }
        }

        private ActiveStatus Find(string id)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].Definition.Id == id) return _active[i];
            return null;
        }
    }
}
