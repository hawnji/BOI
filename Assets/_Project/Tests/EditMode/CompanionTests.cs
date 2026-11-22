using System;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Rooms;
using Hollowcrawl.Simulation;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class CompanionTests
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

        private static RunSimulation NewRun(uint seed = 91)
            => new RunSimulation(new RunSetup { Seed = new RunSeed(seed) }, TestContent.Database, Library, EffectRegistry.CreateDefault());

        private static void StepFor(RunSimulation run, float seconds, PlayerInputFrame input = default)
        {
            int frames = (int)Math.Ceiling(seconds / RunSimulation.FixedStep);
            for (int i = 0; i < frames && run.State == RunState.Playing; i++) run.Step(input);
        }

        private static CompanionActor Spawn(RunSimulation run, string id)
        {
            run.SpawnCompanion(id, new object());
            return run.Companions.Last();
        }

        [Test]
        public void ShippedCompanionsCoverEveryBehaviourAndReferencedIds()
        {
            var db = TestContent.Database;
            Assert.That(db.Companions.Count, Is.GreaterThanOrEqualTo(30));
            foreach (CompanionBehavior b in Enum.GetValues(typeof(CompanionBehavior)))
                Assert.That(db.Companions.Values.Any(c => c.Behavior == b), b.ToString());
            foreach (var id in new[] { "wisp", "bone_sparrow", "drone_mite" })
                Assert.That(db.Companions.ContainsKey(id), id);
        }

        [Test]
        public void SpawnedCompanionCarriesItsDefinition()
        {
            var run = NewRun();
            var c = Spawn(run, "bramble_hound");
            Assert.That(c.Definition, Is.Not.Null);
            Assert.That(c.Definition.Behavior, Is.EqualTo(CompanionBehavior.Chaser));
            Assert.That(c.ViewKey, Is.EqualTo("companion_bramble_hound"));
            Assert.That(run.CurrentRoom.Companions, Does.Contain(c), "room shares the run's companion list");
        }

        [Test]
        public void ShooterHitsTheNearestEnemy()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(3f, 4.5f);
            Spawn(run, "wisp");
            var e = run.CurrentRoom.SpawnEnemy("husk_shambler", new Vec2(8f, 4.5f), false);
            StepFor(run, 4f);
            Assert.That(e.Removed || e.Health < e.MaxHealth, Is.True);
            Assert.That(run.CurrentRoom.Shots.Count(s => s.FromCompanion) + (e.Health < e.MaxHealth ? 1 : 0), Is.GreaterThan(0));
        }

        [Test]
        public void MimicOnlyFiresWhenThePlayerFires()
        {
            var run = NewRun();
            Spawn(run, "echo_shade");
            StepFor(run, 1f);
            Assert.That(run.CurrentRoom.Shots.Any(s => s.FromCompanion), Is.False, "idle player, idle mimic");
            bool seen = false;
            var fire = new PlayerInputFrame { Aim = new Vec2(1f, 0f), FireHeld = true };
            for (int i = 0; i < 60 && !seen; i++) { run.Step(fire); seen = run.CurrentRoom.Shots.Any(s => s.FromCompanion); }
            Assert.That(seen, Is.True);
        }

        [Test]
        public void OrbitalCirclesThePlayerAndDamagesOnContact()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(7f, 4.5f);
            var c = Spawn(run, "thorn_ring");
            var e = run.CurrentRoom.SpawnEnemy("husk_shambler", new Vec2(7f + c.Definition.OrbitRadius, 4.5f), false);
            StepFor(run, 2f);
            Assert.That((c.Position - run.Player.Position).Length, Is.EqualTo(c.Definition.OrbitRadius).Within(0.05f));
            Assert.That(e.Removed || e.Health < e.MaxHealth, Is.True);
        }

        [Test]
        public void ChaserRunsDownAnEnemy()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(2f, 4.5f);
            var c = Spawn(run, "gnaw_rat");
            var e = run.CurrentRoom.SpawnEnemy("husk_shambler", new Vec2(10f, 4.5f), false);
            StepFor(run, 4f);
            Assert.That(e.Removed || e.Health < e.MaxHealth, Is.True);
        }

        [Test]
        public void CollectorFetchesLoosePickupsButNotShopGoods()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(2f, 4.5f);
            Spawn(run, "magpie");
            var coin = run.CurrentRoom.SpawnPickupActor(PickupKind.Coin, new Vec2(7f, 4.5f), scatter: false);
            var ware = run.CurrentRoom.SpawnPickupActor(PickupKind.Bomb, new Vec2(7f, 6.5f), price: 5, scatter: false);
            int before = run.Engine.Resources.Get(ResourceKind.Coins);
            StepFor(run, 3f);
            Assert.That(coin.Removed, Is.True);
            Assert.That(run.Engine.Resources.Get(ResourceKind.Coins), Is.GreaterThanOrEqualTo(before + 1), "the start room may hold other coins the magpie also fetches");
            Assert.That(ware.Removed, Is.False, "companions never buy things");
        }

        [Test]
        public void BomberBombsNeverHurtThePlayer()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(3f, 4.5f);
            Spawn(run, "fuse_beetle");
            var e = run.CurrentRoom.SpawnEnemy("husk_shambler", new Vec2(8f, 4.5f), false);
            BombActor bomb = null;
            for (int i = 0; i < 300 && bomb == null; i++) { run.Step(default); bomb = run.CurrentRoom.Bombs.FirstOrDefault(b => !b.Removed); }
            Assert.That(bomb, Is.Not.Null, "beetle dropped a bomb");
            Assert.That(bomb.PlayerSafe, Is.True);
            e.Position = new Vec2(12f, 2f); // move the enemy away so only the bomb could hurt the player
            run.Player.Position = bomb.Position; // stand right on it
            int vigor = run.Engine.Health.Vigor;
            for (int i = 0; i < 90 && !bomb.Removed; i++) { run.Player.Position = bomb.Position; run.Step(default); }
            Assert.That(bomb.Removed, Is.True);
            Assert.That(run.Events.Events.Any(x => x.Kind == SimEventKind.Explosion), Is.True);
            Assert.That(run.Engine.Health.Vigor, Is.EqualTo(vigor));
        }

        [Test]
        public void BomberBombsDamageEnemies()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(3f, 4.5f);
            Spawn(run, "powder_crow");
            var e = run.CurrentRoom.SpawnEnemy("husk_shambler", new Vec2(9f, 4.5f), false);
            StepFor(run, 3f);
            Assert.That(e.Removed || e.Health < e.MaxHealth, Is.True);
        }

        [Test]
        public void GuardianBlocksIncomingShots()
        {
            var run = NewRun();
            run.Player.Position = new Vec2(8f, 4.5f);
            var c = Spawn(run, "ward_knight");
            StepFor(run, 0.5f);
            run.CurrentRoom.SpawnEnemyShot(new Vec2(3f, 4.5f), new Vec2(1f, 0f), 6f, 1);
            int vigor = run.Engine.Health.Vigor;
            StepFor(run, 2f);
            Assert.That(run.Engine.Health.Vigor, Is.EqualTo(vigor));
        }

        [Test]
        public void ProviderDropsAPickupEveryFewClearedRooms()
        {
            var run = NewRun();
            var c = Spawn(run, "coin_mole");
            Assert.That(c.Definition.ProvideEveryRooms, Is.EqualTo(2));
            var combat = run.Layout.Rooms.Where(n => n.Type == RoomType.Combat).Take(2).ToList();
            Assert.That(combat.Count, Is.EqualTo(2));
            int coinsSpawned = 0;
            for (int i = 0; i < 2; i++)
            {
                run.DebugEnterRoom(combat[i].Index);
                for (int guard = 0; guard < 20 && run.CurrentRoom.LivingEnemies > 0; guard++)
                {
                    foreach (var e in run.CurrentRoom.Enemies.ToList())
                        run.CurrentRoom.DamageEnemy(e, 100000f, DamageType.Physical, false, default, 0f, null, 0, false);
                    run.Step(default);
                }
                run.Step(default);
                Assert.That(run.FloorState.Current.Cleared, Is.True);
                if (i == 0) Assert.That(c.RoomsSinceProvide, Is.EqualTo(1));
                coinsSpawned = run.CurrentRoom.Pickups.Count(p => p.Pickup == PickupKind.Coin && (p.Position - c.Position).Length < 2.5f);
            }
            Assert.That(c.RoomsSinceProvide, Is.EqualTo(0), "counter resets after providing");
            Assert.That(coinsSpawned, Is.GreaterThan(0));
        }
    }
}
