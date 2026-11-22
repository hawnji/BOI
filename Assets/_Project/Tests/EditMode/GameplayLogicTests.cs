using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.Items;
using Hollowcrawl.Movement;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Rooms;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class MovementTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void ReachesTopSpeedQuicklyAndStopsQuickly()
        {
            var m = new MovementModel();
            var t = new MovementTuning();
            int frames = 0;
            while (m.Velocity.Length < t.BaseSpeed * 0.95f && frames < 120) { m.Step(Dt, new Vec2(1, 0), 1f, 1f, SurfaceModifier.Normal); frames++; }
            Assert.LessOrEqual(frames, 6, "~0.1 s to reach full speed");
            frames = 0;
            while (m.Velocity.Length > 0.01f && frames < 120) { m.Step(Dt, default, 1f, 1f, SurfaceModifier.Normal); frames++; }
            Assert.LessOrEqual(frames, 5, "no sliding after release");
        }

        [Test]
        public void ReversalIsFasterThanStartingFromRest()
        {
            var m = new MovementModel();
            for (int i = 0; i < 30; i++) m.Step(Dt, new Vec2(1, 0), 1f, 1f, SurfaceModifier.Normal);
            int frames = 0;
            while (m.Velocity.X > -4f && frames < 120) { m.Step(Dt, new Vec2(-1, 0), 1f, 1f, SurfaceModifier.Normal); frames++; }
            Assert.LessOrEqual(frames, 8);
        }

        [Test]
        public void DiagonalSpeedIsNotFaster()
        {
            var m = new MovementModel();
            for (int i = 0; i < 60; i++) m.Step(Dt, new Vec2(1, 1), 1f, 1f, SurfaceModifier.Normal);
            Assert.AreEqual(new MovementTuning().BaseSpeed, m.Velocity.Length, 0.01f);
        }

        [Test]
        public void ModifiersRootsLocksAndKnockback()
        {
            var m = new MovementModel();
            for (int i = 0; i < 60; i++) m.Step(Dt, new Vec2(1, 0), 1.5f, 0.5f, new SurfaceModifier { SpeedMultiplier = 0.8f, TractionMultiplier = 1f });
            Assert.AreEqual(5.2f * 1.5f * 0.5f * 0.8f, m.Velocity.Length, 0.01f);

            m.Root(0.5f);
            for (int i = 0; i < 10; i++) m.Step(Dt, new Vec2(1, 0), 1f, 1f, SurfaceModifier.Normal);
            Assert.Less(m.Velocity.Length, 0.01f);

            var k = new MovementModel();
            k.AddKnockback(new Vec2(10, 0));
            k.LockInput(0.2f);
            k.Step(Dt, new Vec2(-1, 0), 1f, 1f, SurfaceModifier.Normal);
            Assert.Greater(k.Velocity.X, 5f, "input is ignored while locked; knockback carries");
            for (int i = 0; i < 60; i++) k.Step(Dt, default, 1f, 1f, SurfaceModifier.Normal);
            Assert.AreEqual(0f, k.Knockback.Length, 1e-3);
        }

        [Test]
        public void IceIsSlippery()
        {
            var normal = new MovementModel(); var ice = new MovementModel();
            var iceSurface = new SurfaceModifier { SpeedMultiplier = 1f, TractionMultiplier = 0.15f };
            for (int i = 0; i < 30; i++) { normal.Step(Dt, new Vec2(1, 0), 1, 1, SurfaceModifier.Normal); ice.Step(Dt, new Vec2(1, 0), 1, 1, iceSurface); }
            for (int i = 0; i < 6; i++) { normal.Step(Dt, default, 1, 1, SurfaceModifier.Normal); ice.Step(Dt, default, 1, 1, iceSurface); }
            Assert.AreEqual(0f, normal.Velocity.Length, 1e-3);
            Assert.Greater(ice.Velocity.Length, 2f);
        }

        [Test]
        public void SnapTo8()
        {
            var s = MovementModel.SnapTo8(new Vec2(0.9f, 0.3f));
            Assert.AreEqual(1f, s.X, 1e-4); Assert.AreEqual(0f, s.Y, 1e-4);
            s = MovementModel.SnapTo8(new Vec2(0.7f, 0.65f));
            Assert.AreEqual(0.7071f, s.X, 1e-3); Assert.AreEqual(0.7071f, s.Y, 1e-3);
        }
    }

    public class FireControllerTests
    {
        [Test]
        public void FireRateIsRespectedWithoutBanking()
        {
            var f = new FireController();
            int shots = 0;
            for (int i = 0; i < 600; i++) if (f.Update(1f / 60f, true, 3f, 0f, out _)) shots++;
            Assert.AreEqual(30, shots, 1, "3 shots/s for 10 s");
            for (int i = 0; i < 600; i++) f.Update(1f / 60f, false, 3f, 0f, out _); // idle
            int burst = 0;
            for (int i = 0; i < 6; i++) if (f.Update(1f / 60f, true, 3f, 0f, out _)) burst++;
            Assert.AreEqual(1, burst, "idling does not bank extra shots");
        }

        [Test]
        public void ChargeShotsFireOnRelease()
        {
            var f = new FireController();
            for (int i = 0; i < 120; i++) Assert.IsFalse(f.Update(1f / 60f, true, 2.73f, 1f, out _));
            Assert.AreEqual(1f, f.ChargeFraction, 1e-3);
            Assert.IsTrue(f.Update(1f / 60f, false, 2.73f, 1f, out float charge));
            Assert.AreEqual(1f, charge, 1e-3);
            f.Update(1f / 60f, true, 2.73f, 1f, out _);
            Assert.IsFalse(f.Update(1f / 60f, false, 2.73f, 1f, out _), "a tap is too short to fire");
        }

        [Test]
        public void VolleySpreadIsSymmetric()
        {
            var list = new List<Vec2>();
            FireController.VolleyDirections(new Vec2(1, 0), 3, 15f, list);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0f, list[1].Y, 1e-4);
            Assert.AreEqual(-list[0].Y, list[2].Y, 1e-4);
            FireController.VolleyDirections(new Vec2(1, 0), 40, 15f, list);
            Assert.AreEqual(ProjectileSpec.MaxShotsPerVolley, list.Count);
        }
    }

    public class ProjectileSimulationTests
    {
        private sealed class Q : IProjectileQueries
        {
            public readonly Dictionary<int, Vec2> Enemies = new Dictionary<int, Vec2>();
            public Vec2 ShooterPosition { get; set; }
            public int FindNearestEnemies(Vec2 point, float range, int excludeId, int[] results)
            {
                var list = Enemies.Where(e => e.Key != excludeId && (e.Value - point).Length <= range).OrderBy(e => (e.Value - point).Length).Take(results.Length).ToList();
                for (int i = 0; i < list.Count; i++) results[i] = list[i].Key;
                return list.Count;
            }
            public bool TryGetEnemyPosition(int id, out Vec2 p) => Enemies.TryGetValue(id, out p);
        }

        private static ProjectileSpec Basic()
        {
            var s = new ProjectileSpec(); s.Reset();
            s.Damage = 4f; s.Speed = 10f; s.Range = 5f; s.Size = 1f; s.CritChance = 0f;
            return s;
        }

        [Test]
        public void ExpiresAtRange()
        {
            var sim = new ProjectileSimulation();
            sim.Launch(Basic(), new Vec2(0, 0), new Vec2(1, 0));
            int steps = 0;
            while (sim.Step(0.02f, null)) steps++;
            Assert.AreEqual(24, steps, 1);
            Assert.AreEqual(5f, sim.Position.X, 0.25f);
        }

        [Test]
        public void PiercingNeverDoubleHitsAndStopsAfterCount()
        {
            var spec = Basic(); spec.PierceCount = 1;
            var sim = new ProjectileSimulation();
            sim.Launch(spec, default, new Vec2(1, 0));
            var rng = new SeededRandom(1);
            Assert.IsTrue(sim.HitEnemy(1, default, rng, 0, null));
            Assert.IsFalse(sim.HitEnemy(1, default, rng, 0, null), "same enemy again");
            Assert.IsFalse(sim.Outcome.Destroyed);
            Assert.IsTrue(sim.HitEnemy(2, default, rng, 0, null));
            Assert.IsTrue(sim.Outcome.Destroyed);
            Assert.IsFalse(sim.Alive);
        }

        [Test]
        public void BouncesReflectThenStop()
        {
            var spec = Basic(); spec.Bounces = 1;
            var sim = new ProjectileSimulation();
            sim.Launch(spec, default, new Vec2(1, 0));
            sim.HitWall(new Vec2(-1, 0), true);
            Assert.IsTrue(sim.Alive);
            Assert.AreEqual(-1f, sim.Direction.X, 1e-4);
            sim.HitWall(new Vec2(1, 0), true);
            Assert.IsFalse(sim.Alive);
        }

        [Test]
        public void SpectralPassesObstaclesButNotRoomWalls()
        {
            var spec = Basic(); spec.Spectral = true;
            var sim = new ProjectileSimulation();
            sim.Launch(spec, default, new Vec2(1, 0));
            sim.HitWall(new Vec2(-1, 0), false);
            Assert.IsTrue(sim.Alive);
            sim.HitWall(new Vec2(-1, 0), true);
            Assert.IsFalse(sim.Alive);
        }

        [Test]
        public void HomingTurnsTowardEnemies()
        {
            var spec = Basic(); spec.HomingStrength = 1f; spec.Range = 20f;
            var q = new Q(); q.Enemies[7] = new Vec2(0, 5);
            var sim = new ProjectileSimulation();
            sim.Launch(spec, default, new Vec2(1, 0));
            for (int i = 0; i < 20; i++) sim.Step(0.02f, q);
            Assert.Greater(sim.Direction.Y, 0.5f);
        }

        [Test]
        public void SplitExplodeAndChainOutcomes()
        {
            var spec = Basic(); spec.SplitCount = 3; spec.ExplosionRadius = 1f; spec.ChainJumps = 2;
            var q = new Q(); q.Enemies[1] = new Vec2(0, 0); q.Enemies[2] = new Vec2(1, 0); q.Enemies[3] = new Vec2(2, 0); q.Enemies[4] = new Vec2(9, 0);
            var sim = new ProjectileSimulation();
            sim.Launch(spec, default, new Vec2(1, 0));
            Assert.IsTrue(sim.HitEnemy(1, q.Enemies[1], new SeededRandom(1), 0, q));
            var o = sim.Outcome;
            Assert.IsTrue(o.Destroyed && o.Explode);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, o.ChainTargets);
            Assert.AreEqual(3, o.Spawns.Count);
            Assert.IsTrue(o.Spawns.All(s => s.Spec.Generation == 1 && s.Spec.Damage < spec.Damage));
        }

        [Test]
        public void SplitGenerationsTerminate()
        {
            var spec = Basic(); spec.SplitCount = 4;
            var frontier = new List<ProjectileSpec> { spec };
            int total = 0;
            for (int gen = 0; gen < 10 && frontier.Count > 0; gen++)
            {
                var next = new List<ProjectileSpec>();
                foreach (var s in frontier)
                {
                    var sim = new ProjectileSimulation();
                    sim.Launch(s, default, new Vec2(1, 0));
                    sim.HitEnemy(total++, default, new SeededRandom(1), 0, null);
                    next.AddRange(sim.Outcome.Spawns.Select(x => x.Spec));
                }
                frontier = next;
            }
            Assert.AreEqual(1 + 4 + 16 + 64, total, "splitting stops at the generation limit");
        }

        [Test]
        public void BoomerangReturnsAndLobbedLands()
        {
            var spec = Basic(); spec.Delivery = ProjectileDelivery.Boomerang; spec.Range = 4f;
            var q = new Q { ShooterPosition = default };
            var sim = new ProjectileSimulation();
            sim.Launch(spec, default, new Vec2(1, 0));
            float maxX = 0f;
            for (int i = 0; i < 200 && sim.Step(0.02f, q); i++) maxX = System.Math.Max(maxX, sim.Position.X);
            Assert.IsFalse(sim.Alive);
            Assert.AreEqual(2f, maxX, 0.3f);
            Assert.Less(sim.Position.Length, 0.6f, "caught near the shooter");

            var lob = Basic(); lob.Delivery = ProjectileDelivery.Lobbed; lob.ExplosionRadius = 1.5f;
            sim.Launch(lob, default, new Vec2(1, 0));
            Assert.IsFalse(sim.HitEnemy(1, default, new SeededRandom(1), 0, null), "airborne shots pass over enemies");
            while (sim.Step(0.02f, null)) { }
            Assert.IsTrue(sim.Outcome.Explode, "lands and detonates at range");
        }
    }

    public class FloorStateTests
    {
        private static FloorState NewFloor(int floorIndex, uint seed)
        {
            var lib = TestContent.Database.BuildRoomLibrary(out _);
            var layout = new FloorGenerator(lib).Generate(TestContent.Database.Floors["ashen_crypt"], floorIndex, new RunSeed(seed), new GenerationHistory());
            return new FloorState(layout);
        }

        [Test]
        public void CombatLocksDoorsUntilCleared()
        {
            var f = NewFloor(0, 99);
            var start = f.Current;
            Assert.IsTrue(start.Cleared);
            var door = start.Node.Doors.First(d => d.Kind == DoorKind.Normal && f.Rooms[d.ToRoom].Node.Type == RoomType.Combat);
            Assert.AreEqual(DoorUseResult.Passed, f.TryUseDoor(door, new PlayerResources(), new HealthPool(3, 6)));
            var combat = f.Current;
            Assert.IsFalse(combat.Cleared);
            var back = combat.Node.Doors.First(d => d.ToRoom == start.Node.Index);
            Assert.AreEqual(DoorUseResult.BlockedByCombat, f.TryUseDoor(back, null, null));
            bool clearedEvent = false; f.RoomCleared += r => clearedEvent = true;
            for (int i = 0; i < combat.Node.Enemies.Count; i++) f.NotifyEnemyKilled();
            Assert.IsTrue(clearedEvent && combat.Cleared);
            Assert.AreEqual(DoorUseResult.Passed, f.TryUseDoor(back, null, null));
            Assert.AreEqual(start.Node.Index, f.CurrentRoom);
        }

        [Test]
        public void LockedDoorsSpendOneKeyAndStayOpen()
        {
            var f = NewFloor(1, 4242);
            var treasure = f.Rooms.First(r => r.Node.Type == RoomType.Treasure);
            var lockedDoor = treasure.Node.Doors.First(d => d.Kind == DoorKind.Locked);
            int neighbour = lockedDoor.ToRoom;
            f.TeleportTo(neighbour);
            f.MarkCleared();
            var door = f.Current.Node.Doors.First(d => d.ToRoom == treasure.Node.Index);
            var res = new PlayerResources();
            Assert.AreEqual(DoorUseResult.NeedsKey, f.TryUseDoor(door, res, null));
            res.Add(ResourceKind.Keys, 2);
            Assert.AreEqual(DoorUseResult.KeyUsed, f.TryUseDoor(door, res, null));
            Assert.AreEqual(1, res.Get(ResourceKind.Keys));
            Assert.AreEqual(DoorUseResult.Passed, f.TryUseDoor(door, res, null));
            Assert.AreEqual(1, res.Get(ResourceKind.Keys), "already unlocked");
            Assert.AreEqual(treasure.Node.Index, f.CurrentRoom);
        }

        [Test]
        public void SecretDoorsNeedRevealing()
        {
            for (uint s = 1; s < 50; s++)
            {
                var f = NewFloor(0, s);
                var secret = f.Rooms.FirstOrDefault(r => r.Node.Type == RoomType.Secret);
                if (secret == null) continue;
                var outside = secret.Node.Doors[0].ToRoom;
                f.TeleportTo(outside); f.MarkCleared();
                var door = f.Current.Node.Doors.First(d => d.ToRoom == secret.Node.Index);
                Assert.AreEqual(DoorUseResult.Hidden, f.TryUseDoor(door, null, null));
                Assert.IsFalse(secret.Revealed, "secret rooms are not revealed by visiting neighbours");
                Assert.IsTrue(f.RevealSecretDoor(door));
                Assert.AreEqual(DoorUseResult.Passed, f.TryUseDoor(door, null, null));
                return;
            }
            Assert.Fail("no floor with a secret room found");
        }

        [Test]
        public void MapRevealRespectsSecrets()
        {
            var f = NewFloor(0, 7);
            f.RevealMap(includeSecrets: false);
            Assert.IsTrue(f.Rooms.Where(r => r.Node.Type != RoomType.Secret && r.Node.Type != RoomType.HiddenVault).All(r => r.Revealed));
            Assert.IsTrue(f.Rooms.Where(r => r.Node.Type == RoomType.Secret).All(r => !r.Revealed));
        }
    }
}
