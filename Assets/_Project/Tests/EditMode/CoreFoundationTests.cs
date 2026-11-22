using System.Collections.Generic;
using Hollowcrawl.Combat;
using Hollowcrawl.Events;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;
using Hollowcrawl.Save;
using Hollowcrawl.Stats;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var a = new SeededRandom(42); var b = new SeededRandom(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void KnownValuesAreStableAcrossPlatforms()
        {
            // Guards against accidental algorithm changes, which would break every shared seed.
            var r = new SeededRandom(12345);
            var first = new[] { r.NextUInt(), r.NextUInt(), r.NextUInt() };
            var again = new SeededRandom(12345);
            Assert.AreEqual(first, new[] { again.NextUInt(), again.NextUInt(), again.NextUInt() });
            Assert.AreNotEqual(first[0], first[1]);
        }

        [Test]
        public void ForksAreIndependentAndReproducible()
        {
            var root = new SeededRandom(7);
            var layout1 = root.Fork("layout"); var loot1 = root.Fork("loot");
            var root2 = new SeededRandom(7);
            var loot2 = root2.Fork("loot");
            for (int i = 0; i < 50; i++) loot2.NextUInt(); // consuming loot must not change layout
            var layout2 = root2.Fork("layout");
            for (int i = 0; i < 100; i++) Assert.AreEqual(layout1.NextUInt(), layout2.NextUInt());
            Assert.AreNotEqual(new SeededRandom(7).Fork("layout").NextUInt(), loot1.NextUInt());
        }

        [Test]
        public void RangeStaysInBounds()
        {
            var r = new SeededRandom(99);
            var seen = new HashSet<int>();
            for (int i = 0; i < 5000; i++)
            {
                int v = r.Range(-3, 4);
                Assert.That(v, Is.InRange(-3, 3));
                seen.Add(v);
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
            Assert.AreEqual(7, seen.Count);
        }

        [Test]
        public void SeedCodesRoundTrip()
        {
            var r = new SeededRandom(5);
            for (int i = 0; i < 500; i++)
            {
                var s = new RunSeed(r.NextUInt());
                string code = s.ToCode();
                Assert.AreEqual(9, code.Length);
                Assert.IsTrue(RunSeed.TryParse(code, out var back));
                Assert.AreEqual(s, back);
                Assert.IsTrue(RunSeed.TryParse(code.ToLowerInvariant().Replace("-", " "), out back));
                Assert.AreEqual(s, back);
            }
            Assert.IsFalse(RunSeed.TryParse("0000-0000", out _)); // '0' is not in the alphabet
            Assert.IsFalse(RunSeed.TryParse("ABC", out _));
        }

        [Test]
        public void DailySeedIsStablePerDate()
        {
            var d = new System.DateTime(2026, 11, 22);
            Assert.AreEqual(RunSeed.ForDate(d), RunSeed.ForDate(d));
            Assert.AreNotEqual(RunSeed.ForDate(d), RunSeed.ForDate(d.AddDays(1)));
        }

        [Test]
        public void WeightedPickerRespectsWeights()
        {
            var p = new WeightedPicker<string>();
            p.Add("rare", 1); p.Add("common", 9); p.Add("never", 0);
            var r = new SeededRandom(3);
            int common = 0;
            for (int i = 0; i < 10000; i++) { var x = p.Pick(r); Assert.AreNotEqual("never", x); if (x == "common") common++; }
            Assert.That(common, Is.InRange(8700, 9300));
        }
    }

    public class EventBusTests
    {
        private struct Ping : IGameEvent { public int Value; }

        [Test]
        public void DuplicateSubscriptionsAreIgnored()
        {
            var bus = new EventBus();
            int calls = 0;
            System.Action<Ping> h = e => calls++;
            bus.Subscribe(h); bus.Subscribe(h);
            bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void HandlersCanUnsubscribeDuringDispatch()
        {
            var bus = new EventBus();
            int calls = 0;
            System.IDisposable sub = null;
            sub = bus.Subscribe<Ping>(e => { calls++; sub.Dispose(); });
            bus.Subscribe<Ping>(e => calls++);
            bus.Publish(new Ping());
            bus.Publish(new Ping());
            Assert.AreEqual(3, calls);
        }

        [Test]
        public void RecursivePublishingIsCapped()
        {
            var bus = new EventBus();
            int calls = 0, limitHits = 0;
            bus.RecursionLimitHit += t => limitHits++;
            bus.Subscribe<Ping>(e => { calls++; bus.Publish(new Ping { Value = e.Value + 1 }); });
            bus.Publish(new Ping());
            Assert.AreEqual(EventBus.MaxPublishDepth, calls);
            Assert.AreEqual(1, limitHits);
        }

        [Test]
        public void ThrowingHandlerDoesNotStopOthers()
        {
            var bus = new EventBus();
            int ok = 0, failures = 0;
            bus.HandlerFailed += ex => failures++;
            bus.Subscribe<Ping>(e => throw new System.Exception("boom"));
            bus.Subscribe<Ping>(e => ok++);
            bus.Publish(new Ping());
            Assert.AreEqual(1, ok); Assert.AreEqual(1, failures);
        }
    }

    public class StatTests
    {
        private sealed class Flag : IStatCondition { public bool On; public bool IsMet() => On; }
        private sealed class Provider : IStatValueProvider { public float V; public float GetValue() => V; }

        [Test]
        public void FormulaAppliesFlatThenPercentThenMultiply()
        {
            var s = new StatSheet();
            s.SetBase(StatId.Damage, 3.5f);
            s.Add(new StatModifier(StatId.Damage, ModifierOp.Flat, 1.5f, "a"));        // 5
            s.Add(new StatModifier(StatId.Damage, ModifierOp.PercentAdd, 0.2f, "b"));  // +20%
            s.Add(new StatModifier(StatId.Damage, ModifierOp.PercentAdd, 0.3f, "c"));  // +30% => x1.5 = 7.5
            s.Add(new StatModifier(StatId.Damage, ModifierOp.Multiply, 2f, "d"));      // 15
            Assert.AreEqual(15f, s[StatId.Damage], 1e-4);
        }

        [Test]
        public void GlobalCapsFloorsAndCeilingsApply()
        {
            var s = new StatSheet();
            s.Add(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 50f, "x"));
            Assert.AreEqual(StatTable.Get(StatId.MoveSpeed).Max, s[StatId.MoveSpeed], 1e-4);
            s.Add(new StatModifier(StatId.MoveSpeed, ModifierOp.Ceiling, 1.5f, "y"));
            Assert.AreEqual(1.5f, s[StatId.MoveSpeed], 1e-4);
            s.Add(new StatModifier(StatId.MoveSpeed, ModifierOp.Floor, 1.8f, "z"));
            Assert.AreEqual(1.8f, s[StatId.MoveSpeed], 1e-4, "a floor wins over a conflicting ceiling");
        }

        [Test]
        public void RemoveBySourceAndConditionalModifiers()
        {
            var s = new StatSheet();
            float baseDmg = s[StatId.Damage];
            var flag = new Flag();
            s.Add(new StatModifier(StatId.Damage, ModifierOp.Flat, 2f, "item", 0, flag));
            Assert.AreEqual(baseDmg, s[StatId.Damage], 1e-4);
            flag.On = true; s.Invalidate();
            Assert.AreEqual(baseDmg + 2f, s[StatId.Damage], 1e-4);
            Assert.AreEqual(1, s.RemoveAllFromSource("item"));
            Assert.AreEqual(baseDmg, s[StatId.Damage], 1e-4);
        }

        [Test]
        public void DynamicModifiersAndOverrideBase()
        {
            var s = new StatSheet();
            var p = new Provider { V = 1f };
            s.Add(new StatModifier(StatId.Luck, ModifierOp.Flat, p, "dyn"));
            Assert.AreEqual(1f, s[StatId.Luck], 1e-4);
            p.V = 3f; s.Invalidate();
            Assert.AreEqual(3f, s[StatId.Luck], 1e-4);
            s.Add(new StatModifier(StatId.Damage, ModifierOp.OverrideBase, 10f, "low", 1));
            s.Add(new StatModifier(StatId.Damage, ModifierOp.OverrideBase, 20f, "high", 5));
            Assert.AreEqual(20f, s[StatId.Damage], 1e-4);
        }

        [Test]
        public void ModifierCountIsCapped()
        {
            var s = new StatSheet { MaxModifiersPerStat = 4 };
            for (int i = 0; i < 4; i++) Assert.IsTrue(s.Add(new StatModifier(StatId.Luck, ModifierOp.Flat, 1, i)));
            Assert.IsFalse(s.Add(new StatModifier(StatId.Luck, ModifierOp.Flat, 1, 99)));
        }
    }

    public class HealthTests
    {
        [Test]
        public void DamageOrderIsWardThenUmbralThenVigor()
        {
            var h = new HealthPool(3, 6, ward: 1, umbral: 2) { HitInvulnerability = 0f };
            int bursts = 0; h.UmbralBurst += () => bursts++;
            h.TakeDamage(2, 0f, null);
            Assert.AreEqual(0, h.Ward); Assert.AreEqual(1, h.Umbral); Assert.AreEqual(6, h.Vigor);
            Assert.AreEqual(0, bursts);
            h.TakeDamage(2, 0f, null);
            Assert.AreEqual(0, h.Umbral); Assert.AreEqual(5, h.Vigor); Assert.AreEqual(1, bursts);
        }

        [Test]
        public void InvulnerabilityBlocksAndExpires()
        {
            var h = new HealthPool(3, 6);
            h.TakeDamage(1, 0f, null);
            Assert.AreEqual(0, h.TakeDamage(1, 0f, null));
            h.Tick(2f);
            Assert.AreEqual(1, h.TakeDamage(1, 0f, null));
            Assert.AreEqual(4, h.Vigor);
        }

        [Test]
        public void LethalHitCanBePreventedByRevive()
        {
            var h = new HealthPool(1, 1) { HitInvulnerability = 0f };
            bool died = false; h.Died += () => died = true;
            h.LethalHit += a => { a.Prevent = true; h.Restore(2); };
            h.TakeDamage(5, 0f, null);
            Assert.IsFalse(died); Assert.AreEqual(2, h.Vigor); Assert.IsFalse(h.IsDead);
        }

        [Test]
        public void DeathWithoutRevive()
        {
            var h = new HealthPool(1, 2) { HitInvulnerability = 0f };
            bool died = false; h.Died += () => died = true;
            h.TakeDamage(2, 0f, null);
            Assert.IsTrue(died); Assert.IsTrue(h.IsDead);
            Assert.AreEqual(0, h.Heal(2), "the dead cannot be healed");
        }

        [Test]
        public void HealingIsCappedByContainers()
        {
            var h = new HealthPool(2, 1);
            Assert.AreEqual(3, h.Heal(10));
            Assert.AreEqual(4, h.Vigor);
        }

        [Test]
        public void DefenseCanNegateHits()
        {
            var h = new HealthPool(6, 12) { HitInvulnerability = 0f };
            var rng = new SeededRandom(1);
            int blocked = 0;
            for (int i = 0; i < 400; i++) { if (h.TakeDamage(1, 6f, rng) == 0) blocked++; h.Tick(5f); h.Heal(1); }
            Assert.That(blocked, Is.InRange(150, 240), "6 defense = 48% block chance");
        }
    }

    public class StatusTests
    {
        private static StatusEffectDefinition Burn() => new StatusEffectDefinition { Id = "burn", DamagePerTick = 2f, TickInterval = 0.5f, MaxDuration = 6f, Cancels = new[] { "chill" } };
        private static StatusEffectDefinition Chill() => new StatusEffectDefinition { Id = "chill", MoveMultiplier = 0.5f, MaxDuration = 5f };

        [Test]
        public void DamageTicksOverTime()
        {
            var c = new StatusController();
            float total = 0f; c.DamageTick += d => total += d.Amount;
            c.Apply(Burn(), 2f, 1f);
            for (int i = 0; i < 8; i++) c.Tick(0.25f);
            Assert.AreEqual(8f, total, 1e-3);
            Assert.IsFalse(c.Has("burn"));
        }

        [Test]
        public void IntensifyStacksUpToMax()
        {
            var poison = new StatusEffectDefinition { Id = "poison", DamagePerTick = 1f, TickInterval = 1f, Stacking = StatusStacking.Intensify, MaxStacks = 3 };
            var c = new StatusController();
            for (int i = 0; i < 5; i++) c.Apply(poison, 3f, 1f);
            Assert.AreEqual(3, c.Active[0].Stacks);
        }

        [Test]
        public void CancelsAndBossImmunity()
        {
            var c = new StatusController();
            c.Apply(Chill(), 3f, 1f);
            Assert.AreEqual(0.5f, c.MoveMultiplier, 1e-4);
            c.Apply(Burn(), 3f, 1f);
            Assert.IsFalse(c.Has("chill"));
            var boss = new StatusController(isBoss: true);
            Assert.IsFalse(boss.Apply(new StatusEffectDefinition { Id = "freeze", CanAffectBosses = false }, 2f, 1f));
        }
    }

    public class InputTests
    {
        [Test]
        public void ShootModes()
        {
            var settings = new SettingsData { GamepadShootMode = ShootMode.AimToFire, MouseShootMode = ShootMode.ToggleAutoFire };
            var s = new ShootingIntent();
            var pad = new PlayerInputFrame { Device = InputDeviceKind.Gamepad, Aim = new Vec2(0.1f, 0f) };
            Assert.IsFalse(s.Resolve(pad, settings, out _), "inside deadzone");
            pad.Aim = new Vec2(0f, 0.9f);
            Assert.IsTrue(s.Resolve(pad, settings, out var dir));
            Assert.AreEqual(1f, dir.Y, 1e-4);

            var mouse = new PlayerInputFrame { Device = InputDeviceKind.KeyboardMouse, Aim = new Vec2(1f, 0f), FirePressed = true };
            Assert.IsTrue(s.Resolve(mouse, settings, out _));
            mouse.FirePressed = false;
            Assert.IsTrue(s.Resolve(mouse, settings, out _), "auto-fire stays on");
            mouse.FirePressed = true;
            Assert.IsFalse(s.Resolve(mouse, settings, out _));
        }
    }
}
