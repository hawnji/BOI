using System;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Rooms;
using Hollowcrawl.Simulation;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class RoomModifierTests
    {
        private static RoomTemplateLibrary _library;
        private static RoomTemplateLibrary Library
        {
            get
            {
                if (_library != null) return _library;
                _library = new RoomTemplateLibrary();
                _library.AddRange(TestContent.Database.RoomTemplates);
                return _library;
            }
        }

        private static RunSimulation NewRun(uint seed = 41, float chance = -1f)
            => new RunSimulation(new RunSetup { Seed = new RunSeed(seed), RoomModifierChanceOverride = chance, StartingContainers = 12 }, TestContent.Database, Library, EffectRegistry.CreateDefault());

        private static RoomModifierDefinition Mod(string id) => TestContent.Database.RoomModifiers[id];

        private static RoomNode FirstCombat(RunSimulation run) => run.Layout.Rooms.First(n => n.Type == RoomType.Combat && n.Enemies.Count > 0);

        [Test]
        public void ShippedModifiersLoad()
        {
            Assert.That(TestContent.Database.RoomModifiers.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void ChanceOneAlwaysRollsAndAnnouncesAModifier()
        {
            var run = NewRun(chance: 1f);
            run.DebugEnterRoom(FirstCombat(run).Index);
            Assert.That(run.CurrentRoom.Modifier, Is.Not.Null);
            Assert.That(run.Events.Events.Any(e => e.Kind == SimEventKind.RoomModifierApplied && e.Key == run.CurrentRoom.Modifier.Id), Is.True);
        }

        [Test]
        public void ChanceZeroNeverRolls()
        {
            var run = NewRun(chance: 0f);
            foreach (var n in run.Layout.Rooms.Where(n => n.Type == RoomType.Combat))
            {
                run.DebugEnterRoom(n.Index);
                Assert.That(run.CurrentRoom.Modifier, Is.Null);
            }
        }

        [Test]
        public void ModifierRollsAreDeterministicPerSeed()
        {
            string Roll(uint seed)
            {
                var run = NewRun(seed, 1f);
                return string.Join(",", run.Layout.Rooms.Where(n => n.Type == RoomType.Combat).Select(n => { run.DebugEnterRoom(n.Index); return run.CurrentRoom.Modifier?.Id; }));
            }
            Assert.That(Roll(7), Is.EqualTo(Roll(7)));
        }

        [Test]
        public void EliteGuardMakesEverySpawnElite()
        {
            var run = NewRun(chance: 0f);
            var room = run.CurrentRoom;
            room.Modifier = Mod("mod_elite_guard");
            var e = room.SpawnEnemy("husk_shambler", new Vec2(9f, 4.5f), false);
            Assert.That(e.Elite, Is.True);
        }

        [Test]
        public void IronHideReducesDamageTaken()
        {
            var run = NewRun(chance: 0f);
            var room = run.CurrentRoom;
            var plain = room.SpawnEnemy("marrow_brute", new Vec2(9f, 4.5f), false);
            room.DamageEnemy(plain, 6f, DamageType.Physical, false, default, 0f, null, 0, false);
            room.Modifier = Mod("mod_iron_hide");
            var armoured = room.SpawnEnemy("marrow_brute", new Vec2(9f, 2.5f), false);
            room.DamageEnemy(armoured, 6f, DamageType.Physical, false, default, 0f, null, 0, false);
            Assert.That(armoured.MaxHealth - armoured.Health, Is.LessThan(plain.MaxHealth - plain.Health));
        }

        [Test]
        public void GiantsHaveMoreHealth()
        {
            var run = NewRun(chance: 0f);
            var room = run.CurrentRoom;
            var plain = room.SpawnEnemy("husk_shambler", new Vec2(9f, 4.5f), false);
            room.Modifier = Mod("mod_giants");
            var giant = room.SpawnEnemy("husk_shambler", new Vec2(9f, 2.5f), false);
            Assert.That(giant.MaxHealth, Is.EqualTo(plain.MaxHealth * 1.5f).Within(0.01f));
        }

        [Test]
        public void VolatileFoesHurtThePlayerWhenTheyDieNextToThem()
        {
            var run = NewRun(chance: 0f);
            var room = run.CurrentRoom;
            room.Modifier = Mod("mod_volatile");
            run.Player.Position = new Vec2(7f, 4.5f);
            var e = room.SpawnEnemy("husk_crawler", new Vec2(7.8f, 4.5f), false);
            int vigor = run.Engine.Health.Vigor;
            room.DamageEnemy(e, 1000f, DamageType.Physical, false, default, 0f, null, 0, false);
            run.Step(default);
            Assert.That(run.Engine.Health.Vigor, Is.LessThan(vigor));
        }

        [Test]
        public void MendingFoesRegenerate()
        {
            var run = NewRun(chance: 0f);
            var room = run.CurrentRoom;
            room.Modifier = Mod("mod_mending");
            run.Player.Position = new Vec2(2f, 2f);
            var e = room.SpawnEnemy("bone_reliquary", new Vec2(12f, 6.5f), false);
            room.DamageEnemy(e, e.MaxHealth * 0.5f, DamageType.Physical, false, default, 0f, null, 0, false);
            float hurt = e.Health;
            for (int i = 0; i < 120; i++) run.Step(default);
            Assert.That(e.Health, Is.GreaterThan(hurt));
        }

        [Test]
        public void RolledModifierAddsItsSpawnsAndPaysItsRewardOnClear()
        {
            var forced = NewRun(chance: 1f);
            int planned = FirstCombat(forced).Enemies.Count;
            forced.DebugEnterRoom(FirstCombat(forced).Index);
            var mod = forced.CurrentRoom.Modifier;
            Assert.That(mod, Is.Not.Null);
            int expected = planned + (int)Math.Round(planned * mod.ExtraEnemies);
            Assert.That(forced.CurrentRoom.Enemies.Count(e => e.SummonedBy == null), Is.GreaterThanOrEqualTo(Math.Min(expected, RoomSimulation.MaxEnemies)));
            int before = forced.CurrentRoom.Pickups.Count;
            for (int guard = 0; guard < 30 && forced.CurrentRoom.LivingEnemies > 0; guard++)
            {
                foreach (var e in forced.CurrentRoom.Enemies.ToList()) forced.CurrentRoom.DamageEnemy(e, 1e6f, DamageType.Physical, false, default, 0f, null, 0, false);
                forced.Step(default);
            }
            forced.Step(default);
            Assert.That(forced.FloorState.Current.Cleared, Is.True);
            Assert.That(forced.CurrentRoom.Pickups.Count, Is.GreaterThanOrEqualTo(before + mod.RewardPickups));
        }
    }
}
