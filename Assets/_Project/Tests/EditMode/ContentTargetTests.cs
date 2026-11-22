using System;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Input;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Progression;
using Hollowcrawl.Save;
using Hollowcrawl.Rng;
using Hollowcrawl.Rooms;
using Hollowcrawl.Simulation;
using Hollowcrawl.Items;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    /// <summary>Guards the content counts already reached so they never silently regress.</summary>
    public class ContentTargetTests
    {
        [Test]
        public void ItemTargetsAreMet()
        {
            var items = TestContent.Database.Items.Values.ToList();
            Assert.That(items.Count, Is.GreaterThanOrEqualTo(300));
            Assert.That(items.Count(i => i.Kind == ItemKind.Passive), Is.GreaterThanOrEqualTo(180));
            Assert.That(items.Count(i => i.Kind == ItemKind.Active), Is.GreaterThanOrEqualTo(45));
            Assert.That(items.Count(i => i.Kind == ItemKind.Trinket), Is.GreaterThanOrEqualTo(60));
        }

        [Test]
        public void TransformationAndCompanionTargetsAreMet()
        {
            Assert.That(TestContent.Database.Transformations.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(TestContent.Database.Companions.Count, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void EveryTransformationTagIsReachable()
        {
            var items = TestContent.Database.Items.Values.ToList();
            foreach (var t in TestContent.Database.Transformations.Values)
                foreach (var req in t.Requires.Where(r => r.Tag != null))
                    Assert.That(items.Count(i => i.HasTag(req.Tag)), Is.GreaterThanOrEqualTo(req.Count + 2), t.Id + " needs more '" + req.Tag + "' items");
        }

        [Test]
        public void EveryActiveItemCanBeUsedHeadless()
        {
            var registry = EffectRegistry.CreateDefault();
            foreach (var item in TestContent.Database.Items.Values.Where(i => i.Kind == ItemKind.Active))
                foreach (var spec in item.Active.OnUse)
                    Assert.That(registry.Create(spec), Is.InstanceOf<IActiveEffect>(), item.Id + " / " + spec.Type);
        }
    
        /// <summary>Every shipped item is held (and actives used) in a live room with enemies, firing, without exceptions.</summary>
        [Test]
        public void EveryItemSurvivesAShortFightHeadless()
        {
            var library = new RoomTemplateLibrary();
            library.AddRange(TestContent.Database.RoomTemplates);
            var fire = new PlayerInputFrame { Aim = new Vec2(1f, 0f), FireHeld = true };
            foreach (var item in TestContent.Database.Items.Values.OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                var run = new RunSimulation(new RunSetup { Seed = new RunSeed(5) }, TestContent.Database, library, EffectRegistry.CreateDefault());
                run.Player.Position = new Vec2(3f, 4.5f);
                try
                {
                    Assert.That(run.Engine.AddItem(item), Is.Not.EqualTo(AddItemResult.Invalid), item.Id);
                    run.CurrentRoom.SpawnEnemy("husk_shambler", new Vec2(9f, 4.5f), false);
                    run.CurrentRoom.SpawnEnemy("ash_bat", new Vec2(9f, 2.5f), false);
                    for (int i = 0; i < 90 && run.State == RunState.Playing; i++) run.Step(fire);
                    if (item.Kind == ItemKind.Active) { run.Step(new PlayerInputFrame { ActiveItemPressed = true }); run.Step(default); }
                    for (int i = 0; i < 60 && run.State == RunState.Playing; i++) run.Step(new PlayerInputFrame { BombPressed = i == 0, Aim = new Vec2(0f, 1f), FireHeld = true });
                }
                catch (Exception ex) { Assert.Fail(item.Id + ": " + ex); }
            }
        }
    
        [Test]
        public void EnemyBossAndFloorTargetsAreMet()
        {
            var db = TestContent.Database;
            Assert.That(db.Enemies.Values.Count(e => !e.IsBoss), Is.GreaterThanOrEqualTo(120));
            Assert.That(db.Bosses.Values.Count(b => b.Tier != Enemies.BossTier.Miniboss), Is.GreaterThanOrEqualTo(30));
            Assert.That(db.Bosses.Values.Count(b => b.Tier == Enemies.BossTier.Miniboss), Is.GreaterThanOrEqualTo(15));
            Assert.That(db.Floors.Count, Is.GreaterThanOrEqualTo(15));
            Assert.That(db.RoomTemplates.Count, Is.GreaterThanOrEqualTo(250));
        }

        /// <summary>Every enemy is spawned awake next to a firing player and fought for a few seconds.</summary>
        [Test]
        public void EveryEnemyFightsHeadless()
        {
            var library = new RoomTemplateLibrary();
            library.AddRange(TestContent.Database.RoomTemplates);
            var fire = new PlayerInputFrame { Aim = new Vec2(1f, 0f), FireHeld = true };
            foreach (var def in TestContent.Database.Enemies.Values.Where(e => !e.IsBoss).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var run = new RunSimulation(new RunSetup { Seed = new RunSeed(9), StartingContainers = 12 }, TestContent.Database, library, EffectRegistry.CreateDefault());
                run.Player.Position = new Vec2(3f, 4.5f);
                try
                {
                    var e = run.CurrentRoom.SpawnEnemy(def.Id, new Vec2(9f, 4.5f), false);
                    Assert.That(e, Is.Not.Null, def.Id);
                    e.Brain.Activate();
                    for (int i = 0; i < 360 && run.State == RunState.Playing; i++) run.Step(fire);
                }
                catch (Exception ex) { Assert.Fail(def.Id + ": " + ex); }
            }
        }

        /// <summary>Every floor generates, every room can be entered, and its boss can be fought and beaten.</summary>
        [Test]
        public void EveryFloorGeneratesAndItsBossFights()
        {
            var library = new RoomTemplateLibrary();
            library.AddRange(TestContent.Database.RoomTemplates);
            foreach (var floor in TestContent.Database.Floors.Values.OrderBy(f => f.Depth))
            {
                var setup = new RunSetup { Seed = new RunSeed(1234), StartingContainers = 12 };
                setup.FloorSequence.Add(floor.Id);
                var run = new RunSimulation(setup, TestContent.Database, library, EffectRegistry.CreateDefault());
                try
                {
                    Assert.That(run.Floor.Id, Is.EqualTo(floor.Id));
                    foreach (var node in run.Layout.Rooms) { run.DebugEnterRoom(node.Index); run.Step(default); }
                    var themed = run.Layout.Rooms.Count(n => n.TemplateId != null && TestContent.Database.RoomTemplates.Any(t => t.Id == n.TemplateId && t.Themes.Contains(floor.Theme)));
                    Assert.That(themed, Is.GreaterThan(0), floor.Id + " uses at least one room themed for it");
                    run.DebugEnterRoom(run.Layout.BossRoom);
                    var boss = run.CurrentRoom.Enemies.FirstOrDefault(e => e.IsBoss);
                    Assert.That(boss, Is.Not.Null, floor.Id + " boss");
                    var fire = new PlayerInputFrame { Aim = new Vec2(1f, 0f), FireHeld = true };
                    for (int i = 0; i < 1200 && run.State == RunState.Playing && !boss.Removed; i++)
                    {
                        if (i % 120 == 0 && !boss.Removed) run.CurrentRoom.DamageEnemy(boss, boss.MaxHealth * 0.25f, DamageType.Physical, false, default, 0f, null, 0, false);
                        run.Step(fire);
                    }
                    Assert.That(boss.Removed, Is.True, floor.Id + " boss can be beaten");
                    Assert.That(run.State, Is.EqualTo(RunState.Playing), floor.Id + " player survived with 12 containers");
                }
                catch (Exception ex) { Assert.Fail(floor.Id + ": " + ex); }
            }
        }
    
        [Test]
        public void ChallengeAndAchievementTargetsAreMet()
        {
            Assert.That(TestContent.Database.Challenges.Count, Is.GreaterThanOrEqualTo(30));
            Assert.That(TestContent.Database.Achievements.Count, Is.GreaterThanOrEqualTo(100));
        }

        /// <summary>Every challenge builds a run that starts and plays a few seconds.</summary>
        [Test]
        public void EveryChallengeStartsHeadless()
        {
            var library = new RoomTemplateLibrary();
            library.AddRange(TestContent.Database.RoomTemplates);
            var fire = new PlayerInputFrame { Aim = new Vec2(1f, 0f), FireHeld = true, Move = new Vec2(0.3f, 0.2f) };
            foreach (var ch in TestContent.Database.Challenges.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                try
                {
                    var setup = RunSetupFactory.Challenge(TestContent.Database, new ProfileData(), ch.Id, new RunSeed(3));
                    var run = new RunSimulation(setup, TestContent.Database, library, EffectRegistry.CreateDefault());
                    foreach (var id in ch.StartingItems) Assert.That(run.Engine.HasItem(id) || run.Engine.ActiveItem?.Id == id, Is.True, ch.Id + " starts with " + id);
                    for (int i = 0; i < 180 && run.State == RunState.Playing; i++) run.Step(fire);
                    Assert.That(run.State, Is.EqualTo(RunState.Playing), ch.Id);
                }
                catch (Exception ex) { Assert.Fail(ch.Id + ": " + ex); }
            }
        }
    }
}
