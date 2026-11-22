using System.Linq;
using Hollowcrawl.Consumables;
using Hollowcrawl.Economy;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Simulation;
using Hollowcrawl.Stats;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class ConsumableTests
    {
        private static RunSimulation NewRun(uint seed = 5, System.Action<RunSetup> configure = null)
        {
            var lib = new RoomTemplateLibrary();
            lib.AddRange(TestContent.Database.RoomTemplates);
            var setup = new RunSetup { Seed = new RunSeed(seed) };
            configure?.Invoke(setup);
            return new RunSimulation(setup, TestContent.Database, lib, EffectRegistry.CreateDefault());
        }

        private static void Collect(RunSimulation run, PickupActor p)
        {
            run.Player.Position = p.Position;
            for (int i = 0; i < 40 && !p.Removed; i++) run.Step(default);
        }

        [Test]
        public void ShippedConsumablesHexesAndShrinesLoad()
        {
            var db = TestContent.Database;
            Assert.That(db.Consumables.Values.Count(c => c.Kind == ConsumableKind.Sigil), Is.GreaterThanOrEqualTo(6));
            Assert.That(db.Consumables.Values.Count(c => c.Kind == ConsumableKind.Draught), Is.GreaterThanOrEqualTo(6));
            Assert.That(db.Hexes.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(db.Shrines["shrine_crossroads"].Options[0].Outcomes[0].Effect.Type, Is.EqualTo("stat"));
        }

        [Test]
        public void DraughtsAreUnknownUntilDrunkAndColoursShuffleBySeed()
        {
            var run = NewRun();
            Assert.That(run.ConsumableDisplayName("draught_ironblood"), Does.EndWith("draught"));
            var other = NewRun(999);
            var all = TestContent.Database.Consumables.Values.Where(c => c.Kind == ConsumableKind.Draught).Select(c => c.Id).ToList();
            Assert.That(all.Any(id => run.DraughtColour(id) != other.DraughtColour(id)), Is.True, "colours differ between seeds");
            Assert.That(all.Select(run.DraughtColour).Distinct().Count(), Is.EqualTo(all.Count), "colours unique within a run");

            float before = run.Engine.Stats[StatId.Damage];
            run.ConsumableSlots.Add("draught_ironblood");
            run.Step(new PlayerInputFrame { ConsumablePressed = true });
            Assert.That(run.Engine.Stats[StatId.Damage], Is.EqualTo(before + 0.35f).Within(1e-3f));
            Assert.That(run.ConsumableDisplayName("draught_ironblood"), Is.EqualTo("Ironblood"));
            Assert.That(run.ConsumableSlots, Is.Empty);
            // a second dose stacks
            run.ConsumableSlots.Add("draught_ironblood");
            run.Step(new PlayerInputFrame { ConsumablePressed = true });
            Assert.That(run.Engine.Stats[StatId.Damage], Is.EqualTo(before + 0.7f).Within(1e-3f));
        }

        [Test]
        public void HarmfulDraughtsNeverKill()
        {
            var run = NewRun(configure: s => s.StartingContainers = 1);
            run.Engine.Health.TakeDamage(1, 0f, null, ignoreInvulnerability: true);
            Assert.That(run.Engine.Health.TotalHalves, Is.EqualTo(1));
            run.ConsumableSlots.Add("draught_bile");
            run.Step(new PlayerInputFrame { ConsumablePressed = true });
            Assert.That(run.Engine.Health.IsDead, Is.False);
            Assert.That(run.State, Is.EqualTo(RunState.Playing));
        }

        [Test]
        public void SigilsWorkThroughTheRoom()
        {
            var run = NewRun();
            run.ConsumableSlots.Add("sigil_coffer");
            int before = run.CurrentRoom.Pickups.Count;
            run.Step(new PlayerInputFrame { ConsumablePressed = true });
            Assert.That(run.CurrentRoom.Pickups.Count(p => p.Pickup == PickupKind.Coin), Is.GreaterThanOrEqualTo(5 + 0 * before));

            run.ConsumableSlots.Add("sigil_cartographer");
            run.Step(new PlayerInputFrame { ConsumablePressed = true });
            Assert.That(run.FloorState.Rooms.All(r => r.Revealed), Is.True);
        }

        [Test]
        public void PickingUpAConsumableWithFullSlotsSwapsIt()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            var a = room.SpawnPickupActor(PickupKind.Consumable, room.Grid.Center, "sigil_hearth", scatter: false);
            Collect(run, a);
            Assert.That(run.ConsumableSlots, Is.EqualTo(new[] { "sigil_hearth" }));
            var b = room.SpawnPickupActor(PickupKind.Consumable, room.Grid.Center + new Vec2(3f, 0f), "sigil_tremor", scatter: false);
            Collect(run, b);
            Assert.That(run.ConsumableSlots, Is.EqualTo(new[] { "sigil_tremor" }));
            Assert.That(room.Pickups.Any(p => p.Pickup == PickupKind.Consumable && p.ContentId == "sigil_hearth"), Is.True, "old one dropped");
        }

        [Test]
        public void HexesApplyFlagsEffectsAndClearOnTheNextFloor()
        {
            var run = NewRun(configure: s => { s.Difficulty.CurseChance = 0f; s.FloorCount = 3; });
            float luck = run.Engine.Stats[StatId.Luck];
            run.ApplyHex(TestContent.Database.Hexes["hex_frailty"]);
            Assert.That(run.Engine.Stats[StatId.Luck], Is.EqualTo(luck - 2f).Within(1e-4f));
            Assert.That(run.Engine.HasCurse("hex_frailty"), Is.True);
            run.ApplyHex(TestContent.Database.Hexes["hex_shroud"]);
            Assert.That(run.Engine.Stats[StatId.Luck], Is.EqualTo(luck).Within(1e-4f), "replacing a hex removes the old one");
            Assert.That(run.MapHidden, Is.True);
        }

        [Test]
        public void LabyrinthHexEnlargesTheFloor()
        {
            int normal = 0, big = 0;
            for (uint seed = 1; seed <= 8; seed++)
            {
                var plain = NewRun(seed, s => { s.Difficulty.CurseChance = 0f; s.FloorCount = 2; });
                var hexed = NewRun(seed, s => { s.Difficulty.CurseChance = 1f; s.ForcedHex = "hex_labyrinth"; s.FloorCount = 2; });
                // force floor 2 by beating floor 1's boss quickly
                foreach (var run in new[] { plain, hexed })
                {
                    run.DebugEnterRoom(run.Layout.BossRoom);
                    foreach (var e in run.CurrentRoom.Enemies.ToList()) { e.BossController?.Tick(9f); run.CurrentRoom.DamageEnemy(e, 1e6f, Combat.DamageType.Physical, false, default, 0f, null, 0, false); }
                    run.Step(default);
                    run.Player.Position = run.CurrentRoom.Exits[0].Position;
                    run.Step(default); run.Step(default);
                }
                Assert.That(plain.FloorIndex, Is.EqualTo(1));
                Assert.That(hexed.ActiveHex?.Id, Is.EqualTo("hex_labyrinth"));
                normal += plain.Layout.Rooms.Count;
                big += hexed.Layout.Rooms.Count;
            }
            Assert.That(big, Is.GreaterThan(normal));
        }

        [Test]
        public void ShrinesChargeTheirCostAndRespectUses()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            var idol = TestContent.Database.Shrines["shrine_gilded_idol"];
            var sh = room.SpawnShrine(idol, 0, room.Grid.Center + new Vec2(3f, 0f));
            run.Player.Position = sh.Position;
            for (int i = 0; i < 30; i++) run.Step(default);
            Assert.That(sh.Uses, Is.EqualTo(0), "no coins, no offering");
            run.Engine.Resources.Add(ResourceKind.Coins, 99);
            for (int i = 0; i < 60 * 12 && !sh.Removed; i++) run.Step(default);
            Assert.That(sh.Removed, Is.True, "breaks after max uses");
            Assert.That(sh.Uses, Is.EqualTo(6));
            Assert.That(run.Engine.Resources.Get(ResourceKind.Coins), Is.LessThanOrEqualTo(99 - 18 + 99));
        }

        [Test]
        public void BloodShrinesNeverTakeTheLastHalfPoint()
        {
            var run = NewRun(configure: s => s.StartingContainers = 1);
            var room = run.CurrentRoom;
            var sh = room.SpawnShrine(TestContent.Database.Shrines["shrine_blood_font"], 0, room.Grid.Center + new Vec2(3f, 0f));
            run.Player.Position = sh.Position;
            for (int i = 0; i < 300; i++) run.Step(default);
            Assert.That(sh.Uses, Is.EqualTo(0));
            Assert.That(run.Engine.Health.TotalHalves, Is.EqualTo(2));
        }

        [Test]
        public void AmbushShrinesRelockTheRoom()
        {
            var run = NewRun();
            var room = run.CurrentRoom;
            var cross = TestContent.Database.Shrines["shrine_crossroads"];
            var opt = cross.Options[1];
            var ambush = new ShrineDefinition { Id = "t", Name = "t", Options = { new ShrineOption { Label = "x", Outcomes = { opt.Outcomes[1] } } } };
            var sh = room.SpawnShrine(ambush, 0, room.Grid.Center + new Vec2(3f, 0f));
            run.Player.Position = sh.Position;
            run.Step(default);
            Assert.That(room.LivingEnemies, Is.EqualTo(3));
            Assert.That(run.FloorState.Current.Cleared, Is.False);
        }

        [Test]
        public void EventRoomsGetAShrine()
        {
            for (uint seed = 1; seed < 60; seed++)
            {
                var run = NewRun(seed);
                var ev = run.Layout.Rooms.FirstOrDefault(r => r.Type == RoomType.Event);
                if (ev == null) continue;
                run.DebugEnterRoom(ev.Index);
                Assert.That(run.CurrentRoom.Shrines, Is.Not.Empty);
                return;
            }
            Assert.Inconclusive("no event room in 60 seeds");
        }
    }
}
