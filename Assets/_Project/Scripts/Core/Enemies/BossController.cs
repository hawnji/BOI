using System;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Enemies
{
    /// <summary>
    /// Drives a boss through its phases: watches health, swaps the brain's movement/attacks when a
    /// threshold is crossed, grants transition invulnerability and reports on-enter actions.
    /// Phases are never skipped silently: a burst of damage that crosses two thresholds plays both
    /// transitions in order.
    /// </summary>
    public sealed class BossController
    {
        private readonly BossDefinition _def;
        public EnemyBrain Brain { get; }
        public int PhaseIndex { get; private set; }
        public float InvulnerableTime { get; private set; }
        public BossPhase CurrentPhase => _def.Phases.Count > 0 ? _def.Phases[PhaseIndex] : null;

        public event Action<BossPhase, int> PhaseStarted;

        public BossController(BossDefinition def, EnemyScaling scaling, SeededRandom rng)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            if (def.Phases.Count == 0) def.Phases.Add(new BossPhase { HealthAtOrBelow = 1f, Movement = def.Body.Movement, Attacks = def.Body.Attacks, InvulnerableOnEnter = 0f });
            def.Phases.Sort((a, b) => b.HealthAtOrBelow.CompareTo(a.HealthAtOrBelow));
            Brain = new EnemyBrain(def.Body, scaling, rng);
            ApplyPhase(0, announce: false);
        }

        /// <summary>Call after the boss takes damage. Returns true if a new phase started.</summary>
        public bool UpdateHealth(float healthFraction)
        {
            int next = PhaseIndex + 1;
            if (next >= _def.Phases.Count || healthFraction > _def.Phases[next].HealthAtOrBelow) return false;
            ApplyPhase(next, announce: true);
            return true;
        }

        public void Tick(float dt) { if (InvulnerableTime > 0f) InvulnerableTime = Math.Max(0f, InvulnerableTime - dt); }

        public bool IsInvulnerable => InvulnerableTime > 0f;

        private void ApplyPhase(int index, bool announce)
        {
            PhaseIndex = index;
            var p = _def.Phases[index];
            Brain.Interrupt(0f);
            Brain.SetBehaviour(p.Movement ?? _def.Body.Movement, p.Attacks.Count > 0 ? p.Attacks : _def.Body.Attacks);
            if (announce) InvulnerableTime = p.InvulnerableOnEnter;
            if (announce) PhaseStarted?.Invoke(p, index);
        }
    }
}
