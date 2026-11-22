using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Enemies;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    internal sealed class FakeSensors : IEnemySensors
    {
        public Vec2 SelfPosition { get; set; } = new Vec2(0f, 0f);
        public Vec2 PlayerPosition { get; set; } = new Vec2(3f, 0f);
        public bool HasLineOfSight { get; set; } = true;
        public float HealthFraction { get; set; } = 1f;
        public bool CanAttack { get; set; } = true;
        public float MoveMultiplier { get; set; } = 1f;
        public int Alive;
        public int AliveSummons(string enemyId) => Alive;
    }

    public class EnemyTests
    {
        private static EnemyScaling Normal(EnemyDefinition d) => EnemyScaling.Compute(d, 0, DifficultyDefinition.Normal(), false);

        private static EnemyDefinition Shooter(params AttackSpec[] attacks) => new EnemyDefinition
        {
            Id = "t", Name = "T", Health = 10f, ThinkInterval = 0.1f,
            Movement = new MovementSpec { Kind = MovementKind.Idle },
            Attacks = attacks.ToList(),
        };

        private static List<EnemyIntent> Run(EnemyBrain b, IEnemySensors s, float seconds, float dt = 0.05f)
        {
            var list = new List<EnemyIntent>();
            for (float t = 0f; t < seconds; t += dt) list.Add(b.Step(dt, s));
            return list;
        }

        [Test]
        public void TelegraphPrecedesFireAndCooldownLimitsRate()
        {
            var atk = new AttackSpec { Id = "shot", Cooldown = 1f, Telegraph = 0.5f, Recovery = 0.1f };
            var brain = new EnemyBrain(Shooter(atk), Normal(Shooter()), new SeededRandom(7));
            var intents = Run(brain, new FakeSensors(), 6f);
            int firstTele = intents.FindIndex(i => i.TelegraphStarted != null);
            int firstFire = intents.FindIndex(i => i.AttackFired != null);
            Assert.That(firstTele, Is.GreaterThanOrEqualTo(0));
            Assert.That((firstFire - firstTele) * 0.05f, Is.EqualTo(0.5f).Within(0.06f), "fires after the telegraph");
            int fires = intents.Count(i => i.AttackFired != null);
            Assert.That(fires, Is.InRange(3, 5), "1s cooldown + 0.5s telegraph + recovery over 6s");
        }

        [Test]
        public void WavesFireMultipleTimesDuringDuration()
        {
            var atk = new AttackSpec { Id = "w", Cooldown = 10f, Telegraph = 0.2f, Duration = 0.9f, Waves = 3, Recovery = 0.1f };
            var brain = new EnemyBrain(Shooter(atk), Normal(Shooter()), new SeededRandom(3));
            var waves = Run(brain, new FakeSensors(), 8f).Where(i => i.AttackFired != null).Select(i => i.WaveIndex).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, waves, "one attack (initial cooldown is half of 10s) with three waves");
        }

        [Test]
        public void DashMovesAlongLockedDirection()
        {
            var atk = new AttackSpec { Id = "d", Kind = AttackKind.Dash, Cooldown = 10f, Telegraph = 0.2f, Duration = 0.5f, DashSpeed = 8f };
            var brain = new EnemyBrain(Shooter(atk), Normal(Shooter()), new SeededRandom(3));
            var s = new FakeSensors { PlayerPosition = new Vec2(0f, 4f) };
            var dashing = Run(brain, s, 7f).Where(i => i.State == BrainState.Attacking).ToList();
            Assert.That(dashing, Is.Not.Empty);
            foreach (var i in dashing) { Assert.That(i.Velocity.Y, Is.EqualTo(8f).Within(0.01f)); Assert.That(i.Velocity.X, Is.EqualTo(0f).Within(0.01f)); }
        }

        [Test]
        public void SummonRespectsMaxAlive()
        {
            var atk = new AttackSpec { Id = "s", Kind = AttackKind.Summon, Cooldown = 0.5f, Telegraph = 0.2f, SummonId = "m", MaxAlive = 2, RequiresLineOfSight = false };
            var brain = new EnemyBrain(Shooter(atk), Normal(Shooter()), new SeededRandom(3));
            var s = new FakeSensors { Alive = 2 };
            Assert.That(Run(brain, s, 3f).Any(i => i.TelegraphStarted != null), Is.False);
            s.Alive = 1;
            Assert.That(Run(brain, s, 3f).Any(i => i.TelegraphStarted != null), Is.True);
        }

        [Test]
        public void MinDifficultyAndRangeAndSightGateAttacks()
        {
            var hardOnly = new AttackSpec { Id = "h", Cooldown = 0.5f, Telegraph = 0.2f, MinDifficulty = 2 };
            var def = Shooter(hardOnly);
            var normal = new EnemyBrain(def, Normal(def), new SeededRandom(1));
            Assert.That(Run(normal, new FakeSensors(), 3f).Any(i => i.AttackFired != null), Is.False);
            var hard = new EnemyBrain(def, EnemyScaling.Compute(def, 0, DifficultyDefinition.Hard(), false), new SeededRandom(1));
            Assert.That(Run(hard, new FakeSensors(), 3f).Any(i => i.AttackFired != null), Is.True);

            var close = new AttackSpec { Id = "c", Cooldown = 0.5f, Telegraph = 0.2f, MaxRange = 2f };
            var b2 = new EnemyBrain(Shooter(close), Normal(def), new SeededRandom(1));
            Assert.That(Run(b2, new FakeSensors { PlayerPosition = new Vec2(5f, 0f) }, 3f).Any(i => i.AttackFired != null), Is.False);

            var b3 = new EnemyBrain(Shooter(new AttackSpec { Id = "los", Cooldown = 0.5f, Telegraph = 0.2f }), Normal(def), new SeededRandom(1));
            Assert.That(Run(b3, new FakeSensors { HasLineOfSight = false }, 3f).Any(i => i.AttackFired != null), Is.False);
        }

        [Test]
        public void FrozenEnemyInterruptsTelegraph()
        {
            var atk = new AttackSpec { Id = "x", Cooldown = 0.5f, Telegraph = 1f };
            var s = new FakeSensors();
            // force: wait until telegraphing, then freeze
            var b = new EnemyBrain(Shooter(atk), Normal(Shooter()), new SeededRandom(5));
            int guard = 0;
            while (b.State != BrainState.Telegraphing && guard++ < 200) b.Step(0.05f, s);
            Assert.That(b.State, Is.EqualTo(BrainState.Telegraphing));
            s.CanAttack = false;
            var frozen = Run(b, s, 2f);
            Assert.That(frozen.Any(i => i.AttackFired != null), Is.False);
        }

        [Test]
        public void ChaseAndRetreatSteering()
        {
            var def = new EnemyDefinition { Id = "c", Name = "C", ThinkInterval = 0.1f, RetreatBelowHealth = 0.3f, Movement = new MovementSpec { Kind = MovementKind.Chase, Speed = 2f, PreferredDistance = 5f } };
            var brain = new EnemyBrain(def, Normal(def), new SeededRandom(2));
            var s = new FakeSensors { PlayerPosition = new Vec2(3f, 0f) };
            var v = brain.Step(0.05f, s).Velocity;
            Assert.That(v.X, Is.GreaterThan(1.9f), "chases toward player");
            s.HealthFraction = 0.2f;
            v = Run(brain, s, 0.3f).Last().Velocity;
            Assert.That(v.X, Is.LessThan(0f), "retreats when below threshold and too close");
            s.MoveMultiplier = 0f;
            v = Run(brain, s, 0.3f).Last().Velocity;
            Assert.That(v.Length, Is.EqualTo(0f).Within(1e-4f), "fully slowed enemies stand still");
        }

        [Test]
        public void DormantUntilPlayerInAggroRange()
        {
            var def = new EnemyDefinition { Id = "d", Name = "D", AggroRange = 4f, Movement = new MovementSpec { Kind = MovementKind.Chase } };
            var brain = new EnemyBrain(def, Normal(def), new SeededRandom(2));
            var s = new FakeSensors { PlayerPosition = new Vec2(10f, 0f) };
            Assert.That(brain.Step(0.1f, s).State, Is.EqualTo(BrainState.Dormant));
            s.PlayerPosition = new Vec2(3f, 0f);
            Assert.That(brain.Step(0.1f, s).State, Is.EqualTo(BrainState.Moving));
        }

        [Test]
        public void BrainIsDeterministicForSameSeed()
        {
            var def = new EnemyDefinition { Id = "w", Name = "W", ThinkInterval = 0.1f, Movement = new MovementSpec { Kind = MovementKind.Wander, Speed = 2f },
                Attacks = { new AttackSpec { Id = "a", Cooldown = 1f, Telegraph = 0.3f, Weight = 1 }, new AttackSpec { Id = "b", Cooldown = 1.3f, Telegraph = 0.3f, Weight = 2 } } };
            string Trace(uint seed)
            {
                var b = new EnemyBrain(def, Normal(def), new SeededRandom(seed));
                return string.Join("|", Run(b, new FakeSensors(), 5f).Select(i => $"{i.State}{i.Velocity}{i.AttackFired?.Id}"));
            }
            Assert.That(Trace(42), Is.EqualTo(Trace(42)));
            Assert.That(Trace(42), Is.Not.EqualTo(Trace(43)));
        }

        [Test]
        public void BossPhasesTransitionInOrderWithInvulnerability()
        {
            var p1 = new AttackSpec { Id = "p1", Cooldown = 1f, Telegraph = 0.3f };
            var p2 = new AttackSpec { Id = "p2", Cooldown = 1f, Telegraph = 0.3f };
            var p3 = new AttackSpec { Id = "p3", Cooldown = 1f, Telegraph = 0.3f };
            var def = new BossDefinition
            {
                Id = "b", Name = "B",
                Phases =
                {
                    new BossPhase { HealthAtOrBelow = 0.33f, Attacks = { p3 }, InvulnerableOnEnter = 1f },
                    new BossPhase { HealthAtOrBelow = 1f, Attacks = { p1 }, InvulnerableOnEnter = 0f },
                    new BossPhase { HealthAtOrBelow = 0.66f, Attacks = { p2 }, InvulnerableOnEnter = 1.5f },
                },
            };
            var boss = new BossController(def, EnemyScaling.Compute(def.Body, 0, null, false), new SeededRandom(9));
            var started = new List<int>();
            boss.PhaseStarted += (p, i) => started.Add(i);
            Assert.That(boss.PhaseIndex, Is.EqualTo(0));
            Assert.That(boss.IsInvulnerable, Is.False);
            Assert.That(boss.UpdateHealth(0.9f), Is.False);

            // a burst from 90% to 10% must play both transitions, one per call
            Assert.That(boss.UpdateHealth(0.1f), Is.True);
            Assert.That(boss.PhaseIndex, Is.EqualTo(1));
            Assert.That(boss.IsInvulnerable, Is.True);
            boss.Tick(2f);
            Assert.That(boss.IsInvulnerable, Is.False);
            Assert.That(boss.UpdateHealth(0.1f), Is.True);
            Assert.That(boss.PhaseIndex, Is.EqualTo(2));
            Assert.That(boss.UpdateHealth(0.05f), Is.False);
            CollectionAssert.AreEqual(new[] { 1, 2 }, started);

            boss.Tick(5f);
            var fired = new HashSet<string>();
            var s = new FakeSensors();
            for (int i = 0; i < 100; i++) { var it = boss.Brain.Step(0.05f, s); if (it.AttackFired != null) fired.Add(it.AttackFired.Id); }
            CollectionAssert.AreEquivalent(new[] { "p3" }, fired, "only the current phase's attacks are used");
        }

        [Test]
        public void AttackPatternDirections()
        {
            var rng = new SeededRandom(1);
            var res = new List<Vec2>();
            AttackPatterns.Directions(ShotPattern.Ring, new Vec2(1f, 0f), 8, 0f, 0f, rng, res);
            Assert.That(res.Count, Is.EqualTo(8));
            Vec2 sum = default; foreach (var d in res) sum = sum + d;
            Assert.That(sum.Length, Is.LessThan(1e-3f), "a ring is balanced");

            AttackPatterns.Directions(ShotPattern.Spread, new Vec2(0f, 1f), 3, 20f, 0f, rng, res);
            Assert.That(res[1].Y, Is.EqualTo(1f).Within(1e-4f), "middle shot is on the aim line");
            Assert.That(res[0].X, Is.EqualTo(-res[2].X).Within(1e-4f), "spread is symmetric");

            AttackPatterns.Directions(ShotPattern.Cross, new Vec2(0.3f, 0.7f), 99, 0f, 0f, rng, res);
            Assert.That(res.Count, Is.EqualTo(4));
            AttackPatterns.Directions(ShotPattern.Aimed, default, 1, 0f, 0f, rng, res);
            Assert.That(res[0].Length, Is.EqualTo(1f).Within(1e-4f), "zero aim falls back to a unit vector");

            AttackPatterns.Directions(ShotPattern.RandomBurst, new Vec2(1f, 0f), 50, 30f, 0f, rng, res);
            foreach (var d in res) Assert.That(d.X, Is.GreaterThan(0.86f), "random burst stays within spread");

            var off = AttackPatterns.WallOffset(new Vec2(1f, 0f), 0, 3, 1f);
            Assert.That(off.Y, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void ScalingCombinesFloorDifficultyAndElite()
        {
            var def = new EnemyDefinition { Health = 10f, HealthPerFloor = 0.2f };
            var n0 = EnemyScaling.Compute(def, 0, DifficultyDefinition.Normal(), false);
            var n5 = EnemyScaling.Compute(def, 5, DifficultyDefinition.Normal(), false);
            var h5e = EnemyScaling.Compute(def, 5, DifficultyDefinition.Hard(), true);
            Assert.That(n0.MaxHealth, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(n5.MaxHealth, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(h5e.MaxHealth, Is.EqualTo(20f * 1.15f * EnemyScaling.EliteHealth).Within(1e-3f));
            Assert.That(h5e.ExtraProjectiles, Is.EqualTo(2));
            Assert.That(h5e.CooldownMultiplier, Is.LessThan(n5.CooldownMultiplier));
            var story = EnemyScaling.Compute(def, 0, DifficultyDefinition.Story(), false);
            Assert.That(story.TelegraphMultiplier, Is.GreaterThan(1f), "story gives longer warnings");
        }

        [Test]
        public void ShippedEnemiesAndBossesLoad()
        {
            var db = TestContent.Database;
            Assert.That(db.Enemies.ContainsKey("husk_shambler"));
            Assert.That(db.Bosses["pyre_warden"].Phases.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(db.Bosses["ossuary_brute"].Tier, Is.EqualTo(BossTier.Miniboss));
            var imp = db.Enemies["cinder_imp"];
            Assert.That(imp.Attacks.Single(a => a.Id == "ember_fan").Pattern, Is.EqualTo(ShotPattern.Spread));
            foreach (var floor in db.Floors.Values)
                foreach (var e in floor.Enemies) Assert.That(db.Enemies.ContainsKey(e.EnemyId), e.EnemyId);
        }
    }
}
