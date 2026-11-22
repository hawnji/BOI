using System;
using System.Linq;
using Hollowcrawl.Input;
using Hollowcrawl.ProcGen;
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
    }
}
