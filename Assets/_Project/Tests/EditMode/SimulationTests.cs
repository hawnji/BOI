using System;
using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Economy;
using Hollowcrawl.Enemies;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Rooms;
using Hollowcrawl.Simulation;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class SimulationTests
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

        private static RunSimulation NewRun(uint seed = 77, Action<RunSetup> configure = null)
        {
            var setup = new RunSetup { Seed = new RunSeed(seed) };
            configure?.Invoke(setup);
            return new RunSimulation(setup, TestContent.Database, Library, EffectRegistry.CreateDefault());
        }

        private static void StepFor(RunSimulation run, float seconds, PlayerInputFrame input = default)
        {
            int frames = (int)Math.Ceiling(seconds / RunSimulation.FixedStep);
            for (int i = 0; i < frames && run.State == RunState.Playing; i++) run.Step(input);
        }

        private static PlayerInputFrame Move(Vec2 dir) => new PlayerInputFrame { Move = dir.Normalized };

        /// <summary>Walks the player out through a door of the current room (teleports next to it first).</summary>
        private static void WalkThrough(RunSimulation run, RoomDoor door)
        {
            var room = run.CurrentRoom;
            RoomGrid.DoorEntryTile(door.Socket, out int x, out int r);
            room.Player.Position = room.Grid.TileCenter(x, r);
            RoomGrid.DoorTile(door.Socket, out int dx, out int dr);
            var dir = room.Grid.TileCenter(dx, dr) - room.Player.Position;
            int from = run.FloorState.CurrentRoom;
            for (int i = 0; i < 120 && run.FloorState.CurrentRoom == from; i++) run.Step(Move(dir));
        }

        private static void KillAllEnemies(RunSimulation run)
        {
            for (int guard = 0; guard < 20 && run.CurrentRoom.LivingEnemies > 0; guard++)
            {
                foreach (var e in run.CurrentRoom.Enemies.ToList())
                {
                    if (e.BossController != null) e.BossController.Tick(10f);
                    run.CurrentRoom.DamageEnemy(e, 100000f, DamageType.Physical, false, default, 0f, null, 0, false);
                }
                run.Step(default);
            }
        }

        // ------------------------------------------------------------------ grid

        [Test]
        public void GridKeepsCirclesInsideWallsAndSlides()
        {
            var g = new RoomGrid(15, 9);
            var p = g.MoveCircle(new Vec2(1f, 4.5f), 0.3f, new Vec2(-5f, 0f), false, out bool hit);
            Assert.That(hit, Is.True);
            Assert.That(p.X, Is.EqualTo(0.3f).Within(0.01f));
            // diagonal into the wall slides along it
            p = g.MoveCircle(new Vec2(0.5f, 4.5f), 0.3f, new Vec2(-1f, 1f), false, out _);
            Assert.That(p.Y, Is.GreaterThan(5.4f));
            // fast movers don't tunnel through a single rock
            g.Set(7, 4, TileKind.Rock);
            var rockLeft = g.MoveCircle(new Vec2(5f, 4.5f), 0.3f, new Vec2(10f, 0f), false, out _);
            Assert.That(rockLeft.X, Is.LessThan(7f));
            // flyers pass over rocks and pits, not walls
            var flown = g.MoveCircle(new Vec2(5f, 4.5f), 0.3f, new Vec2(3f, 0f), true, out _);
            Assert.That(flown.X, Is.EqualTo(8f).Within(0.01f));
        }

        [Test]
        public void DoorTilesSitOnTheBorderAndOpenWhenSet()
        {
            var g = new RoomGrid(30, 18);
            var south = new DoorSocket(new Cell(1, 1), Direction.South);
            RoomGrid.DoorTile(south, out int x, out int r);
            Assert.That((x, r), Is.EqualTo((22, 18)));
            Assert.That(g.Get(x, r), Is.EqualTo(TileKind.Wall));
            g.SetDoor(south, true);
            Assert.That(g.Get(x, r), Is.EqualTo(TileKind.DoorOpen));
            var west = new DoorSocket(new Cell(0, 0), Direction.West);
            RoomGrid.DoorEntryTile(west, out int ex, out int er);
            Assert.That((ex, er), Is.EqualTo((0, 4)));
        }

        [Test]
        public void LShapedRoomsTreatTheMissingCellAsVoid()
        {
            var g = RoomGrid.FromTemplate(null, RoomShape.LNoNE);
            Assert.That(g.Get(20, 2), Is.EqualTo(TileKind.Void));
            Assert.That(g.Get(5, 2), Is.EqualTo(TileKind.Floor));
            Assert.That(g.Get(20, 12), Is.EqualTo(TileKind.Floor));
            var p = g.MoveCircle(new Vec2(14f, 15f), 0.3f, new Vec2(3f, 0f), false, out bool hit);
            Assert.That(hit, Is.True, "can't walk into the void corner");
        }

        [Test]
        public void LineOfSightIsBlockedByRocksButNotPits()
        {
            var g = new RoomGrid(15, 9);
            g.Set(7, 4, TileKind.Pit);
            Assert.That(g.LineOfSight(new Vec2(2f, 4.5f), new Vec2(12f, 4.5f)), Is.True);
            g.Set(7, 4, TileKind.Rock);
            Assert.That(g.LineOfSight(new Vec2(2f, 4.5f), new Vec2(12f, 4.5f)), Is.False);
        }

        // ------------------------------------------------------------------ run

        [Test]
        public void RunStartsInAClearedStartRoomWithOpenDoors()
        {
            var run = NewRun();
            Assert.That(run.State, Is.EqualTo(RunState.Playing));
            Assert.That(run.FloorIndex, Is.EqualTo(0));
            Assert.That(run.FloorState.Current.Node.Type, Is.EqualTo(RoomType.Start));
            Assert.That(run.FloorState.Current.Cleared, Is.True);
            var room = run.CurrentRoom;
            foreach (var d in room.Node.Doors.Where(d => d.Kind != DoorKind.Secret && d.Kind != DoorKind.Locked))
            {
                RoomGrid.DoorTile(d.Socket, out int x, out int r);
                Assert.That(room.Grid.Get(x, r), Is.EqualTo(TileKind.DoorOpen), d.ToString());
            }
            Assert.That(run.Engine.Health.Vigor, Is.EqualTo(6));
        }

        [Test]
        public void WalkingThroughADoorEntersTheNeighbourAtTheMatchingDoor()
        {
            var run = NewRun();
            var start = run.FloorState.CurrentRoom;
            var door = run.CurrentRoom.Node.Doors.First(d => d.Kind == DoorKind.Normal);
            WalkThrough(run, door);
            Assert.That(run.FloorState.CurrentRoom, Is.EqualTo(door.ToRoom));
            var back = RunSimulation.MatchingDoor(run.Layout.Rooms[start], door, run.Layout.Rooms[door.ToRoom]);
            Assert.That(back, Is.Not.Null);
            RoomGrid.DoorEntryTile(back.Socket, out int x, out int r);
            var expected = run.CurrentRoom.Grid.TileCenter(x, r);
            Assert.That((run.Player.Position - expected).Length, Is.LessThan(1.5f));
        }

        [Test]
        public void CombatRoomsLockUntilClearedThenReward()
        {
            for (uint seed = 1; seed < 40; seed++)
            {
                var run = NewRun(seed);
                var door = run.CurrentRoom.Node.Doors.FirstOrDefault(d => d.Kind == DoorKind.Normal && run.Layout.Rooms[d.ToRoom].Type == RoomType.Combat && run.Layout.Rooms[d.ToRoom].Enemies.Count > 0);
                if (door == null) continue;
                WalkThrough(run, door);
                var room = run.CurrentRoom;
                Assert.That(run.FloorState.Current.Cleared, Is.False);
                Assert.That(room.LivingEnemies, Is.GreaterThan(0));
                var anyDoor = room.Node.Doors.First(d => d.Kind == DoorKind.Normal);
                RoomGrid.DoorTile(anyDoor.Socket, out int x, out int r);
                Assert.That(room.Grid.Get(x, r), Is.EqualTo(TileKind.DoorClosed));
                KillAllEnemies(run);
                Assert.That(run.FloorState.Current.Cleared, Is.True);
                Assert.That(room.Grid.Get(x, r), Is.EqualTo(TileKind.DoorOpen));
                Assert.That(run.EnemiesKilled, Is.GreaterThan(0));
                Assert.That(run.Events.Events.Any(e => e.Kind == SimEventKind.RoomCleared) || run.RoomsCleared > 0);
                return;
            }
            Assert.Fail("no seed produced a combat room next to the start");
        }

        [Test]
        public void PlayerShotsKillAnEnemyInFront()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            run.Player.Position = new Vec2(3f, 4.5f);
            var e = room.SpawnEnemy("husk_crawler", new Vec2(8f, 4.5f), false);
            Assert.That(e, Is.Not.Null);
            e.Brain.Activate();
            var aimRight = new PlayerInputFrame { Aim = new Vec2(1f, 0f) };
            for (int i = 0; i < 600 && !e.Removed; i++) run.Step(aimRight);
            Assert.That(e.Removed, Is.True, "5hp crawler should die to 3.5 damage shots");
        }

        [Test]
        public void EnemyContactHurtsAndDeathEndsTheRun()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            run.Player.Position = new Vec2(4f, 4.5f);
            for (int i = 0; i < 4; i++) { var e = room.SpawnEnemy("husk_shambler", new Vec2(4.6f + i * 0.2f, 4.5f), false); e.Brain.Activate(); }
            StepFor(run, 30f);
            Assert.That(run.State, Is.EqualTo(RunState.Lost));
            Assert.That(run.Engine.Health.IsDead, Is.True);
            Assert.That(run.Events.Events.Count, Is.LessThanOrEqualTo(SimEventQueue.Capacity));
        }

        [Test]
        public void EnemyProjectilesHitThePlayerAndRespectInvulnerability()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            run.Player.Position = new Vec2(7.5f, 4.5f);
            for (int i = 0; i < 5; i++) room.SpawnEnemyShot(new Vec2(4f, 4.5f), new Vec2(1f, 0f), 8f, 1);
            StepFor(run, 1f);
            Assert.That(run.Engine.Health.Vigor, Is.EqualTo(5), "only the first shot lands during invulnerability frames");
        }

        [Test]
        public void BombsBreakRocksDamageEnemiesAndHurtThePlayer()
        {
            var run = NewRun(configure: s => s.StartingBombs = 2);
            var room = run.CurrentRoom;
            room.Grid.Set(5, 4, TileKind.Rock);
            run.Player.Position = new Vec2(5.5f, 3.4f);
            var e = room.SpawnEnemy("husk_shambler", new Vec2(6.5f, 3.6f), false);
            run.Step(new PlayerInputFrame { BombPressed = true });
            Assert.That(run.Engine.Resources.Get(ResourceKind.Bombs), Is.EqualTo(1));
            StepFor(run, 2f);
            Assert.That(room.Grid.Get(5, 4), Is.EqualTo(TileKind.Floor), "rock destroyed");
            Assert.That(e.Removed || e.Health < e.MaxHealth, Is.True);
            Assert.That(run.Engine.Health.Vigor, Is.EqualTo(4), "standing on your own bomb costs a full heart");
        }

        [Test]
        public void BombsRevealSecretDoors()
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                var run = NewRun(seed, s => s.StartingBombs = 5);
                var secret = run.CurrentRoom.Node.Doors.FirstOrDefault(d => d.Kind == DoorKind.Secret);
                if (secret == null) continue;
                var room = run.CurrentRoom;
                RoomGrid.DoorTile(secret.Socket, out int x, out int r);
                Assert.That(room.Grid.Get(x, r), Is.EqualTo(TileKind.Wall), "hidden");
                RoomGrid.DoorEntryTile(secret.Socket, out int ex, out int er);
                run.Player.Position = room.Grid.TileCenter(ex, er);
                run.Step(new PlayerInputFrame { BombPressed = true });
                run.Player.Position = room.Grid.Center;
                StepFor(run, 2f);
                Assert.That(room.Grid.Get(x, r), Is.EqualTo(TileKind.DoorOpen));
                Assert.That(run.FloorState.GetDoorState(secret), Is.EqualTo(DoorState.Open));
                return;
            }
            Assert.Fail("no start room with a secret door in 200 seeds");
        }

        [Test]
        public void PickupsAreCollectedOnlyWhenUseful()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            run.Player.Position = room.Grid.Center;
            var coin = room.SpawnPickupActor(PickupKind.Coin, room.Grid.Center, scatter: false);
            var heart = room.SpawnPickupActor(PickupKind.VigorHalf, room.Grid.Center, scatter: false);
            StepFor(run, 0.6f);
            Assert.That(coin.Removed, Is.True);
            Assert.That(run.Engine.Resources.Get(ResourceKind.Coins), Is.EqualTo(1));
            Assert.That(heart.Removed, Is.False, "full health leaves hearts on the floor");
            room.HurtPlayer(1, DamageType.Physical, room.Grid.Center + new Vec2(3f, 0f));
            run.Player.Position = room.Grid.Center;
            StepFor(run, 0.3f);
            Assert.That(heart.Removed, Is.True);
            Assert.That(run.Engine.Health.Vigor, Is.EqualTo(6));
        }

        [Test]
        public void PedestalsGrantItemsAndShopsChargeCoins()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            var ped = room.SpawnPickupActor(PickupKind.Item, room.Grid.Center, "brazier_crown", pedestal: true, scatter: false);
            run.Player.Position = room.Grid.Center;
            StepFor(run, 0.6f);
            Assert.That(ped.Removed, Is.True);
            Assert.That(run.Engine.HasItem("brazier_crown"), Is.True);

            var priced = room.SpawnPickupActor(PickupKind.Key, room.Grid.Center + new Vec2(3f, 0f), price: 5, pedestal: true, scatter: false);
            run.Player.Position = priced.Position;
            StepFor(run, 0.6f);
            Assert.That(priced.Removed, Is.False);
            Assert.That(run.Events.Events.Any(e => e.Kind == SimEventKind.PurchaseFailed));
            run.Engine.Resources.Add(ResourceKind.Coins, 7);
            StepFor(run, 1f);
            Assert.That(priced.Removed, Is.True);
            Assert.That(run.Engine.Resources.Get(ResourceKind.Coins), Is.EqualTo(2));
            Assert.That(run.Engine.Resources.Get(ResourceKind.Keys), Is.EqualTo(1));
        }

        [Test]
        public void SpecialRoomsArePopulated()
        {
            int treasure = 0, shop = 0;
            for (uint seed = 1; seed <= 15; seed++)
            {
                var run = NewRun(seed);
                foreach (var node in run.Layout.Rooms)
                {
                    if (node.Type != RoomType.Treasure && node.Type != RoomType.Shop) continue;
                    run.DebugEnterRoom(node.Index);
                    var pickups = run.CurrentRoom.Pickups;
                    if (node.Type == RoomType.Treasure) { Assert.That(pickups.Any(p => p.Pedestal && p.ContentId != null), seed.ToString()); treasure++; }
                    else { Assert.That(pickups.Count(p => p.Price > 0), Is.GreaterThanOrEqualTo(3)); shop++; }
                }
            }
            Assert.That(treasure, Is.GreaterThan(0));
            Assert.That(shop, Is.GreaterThan(0));
        }

        [Test]
        public void BeatingTheBossOpensTheExitAndTheLastBossWinsTheRun()
        {
            var run = NewRun(5, s => s.FloorCount = 2);
            for (int floor = 0; floor < 2; floor++)
            {
                Assert.That(run.FloorIndex, Is.EqualTo(floor));
                var boss = run.Layout.BossRoom;
                run.DebugEnterRoom(boss);
                Assert.That(run.CurrentRoom.Enemies.Any(e => e.IsBoss), Is.True, "boss spawned");
                KillAllEnemies(run);
                Assert.That(run.FloorState.Current.Cleared, Is.True);
                var exit = run.CurrentRoom.Exits.Single();
                Assert.That(exit.IsVictory, Is.EqualTo(floor == 1));
                Assert.That(run.CurrentRoom.Pickups.Any(p => p.Pedestal), Is.True, "boss reward pedestal");
                run.Player.Position = exit.Position;
                run.Step(default);
                run.Step(default);
            }
            Assert.That(run.State, Is.EqualTo(RunState.Won));
            Assert.That(run.BossesKilled, Is.EqualTo(2));
        }

        [Test]
        public void ItemTeleportsAreDeferredToTheEndOfTheStep()
        {
            var run = NewRun();
            var start = run.FloorState.CurrentRoom;
            run.CurrentRoom.TeleportPlayer(TeleportTarget.RandomRoom);
            Assert.That(run.FloorState.CurrentRoom, Is.EqualTo(start));
            run.Step(default);
            Assert.That(run.FloorState.CurrentRoom, Is.Not.EqualTo(start));
        }

        [Test]
        public void RunsAreDeterministicForTheSameSeedAndInput()
        {
            string Play(uint seed)
            {
                var run = NewRun(seed);
                var input = new SeededRandom(99);
                var trace = new System.Text.StringBuilder();
                for (int i = 0; i < 1800 && run.State == RunState.Playing; i++)
                {
                    var frame = new PlayerInputFrame
                    {
                        Move = new Vec2(input.Range(-1f, 1f), input.Range(-1f, 1f)),
                        Aim = i % 3 == 0 ? new Vec2(1f, 0f).Rotated(input.Range(0, 4) * 90f) : default,
                        BombPressed = i % 400 == 5,
                    };
                    run.Step(frame);
                    if (i % 60 == 0) trace.Append($"{run.FloorState.CurrentRoom}:{run.Player.Position}:{run.Engine.Health.Vigor}:{run.CurrentRoom.Shots.Count}|");
                }
                return trace.ToString();
            }
            Assert.That(Play(11), Is.EqualTo(Play(11)));
        }

        [Test]
        public void RandomPlaySoakKeepsInvariants()
        {
            for (uint seed = 1; seed <= 6; seed++)
            {
                var run = NewRun(seed, s => { s.StartingBombs = 10; s.StartingKeys = 3; });
                var input = new SeededRandom(seed * 31);
                Vec2 heading = new Vec2(1f, 0f);
                for (int i = 0; i < 6000 && run.State == RunState.Playing; i++)
                {
                    if (i % 45 == 0) heading = new Vec2(1f, 0f).Rotated(input.Range(0f, 360f));
                    if (i % 900 == 0 && run.CurrentRoom.LivingEnemies > 0) KillAllEnemies(run);
                    var room = run.CurrentRoom;
                    var frame = new PlayerInputFrame { Move = heading, Aim = new Vec2(0f, 1f).Rotated(i * 7f), BombPressed = i % 700 == 3 };
                    run.Step(frame);
                    var p = run.Player.Position;
                    var g = run.CurrentRoom.Grid;
                    Assert.That(p.X, Is.InRange(-1f, g.Width + 1f), $"seed {seed} frame {i}");
                    Assert.That(p.Y, Is.InRange(-1f, g.Height + 1f), $"seed {seed} frame {i}");
                    Assert.That(run.CurrentRoom.Shots.Count, Is.LessThanOrEqualTo(RoomSimulation.MaxShots));
                    run.Events.Clear();
                }
                Assert.That(run.FloorState.Rooms.Count(r => r.Visited), Is.GreaterThanOrEqualTo(1));
            }
        }
    }
}
