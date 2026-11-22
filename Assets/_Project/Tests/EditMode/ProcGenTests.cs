using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class RoomTemplateTests
    {
        [Test]
        public void AllShippedTemplatesAreValid()
        {
            var lib = TestContent.Database.BuildRoomLibrary(out var report);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(TestContent.Database.RoomTemplates.Count, lib.Count);
        }

        [Test]
        public void ValidatorRejectsBadLayouts()
        {
            var blockedDoor = new RoomTemplate
            {
                Id = "bad_door", Types = new[] { RoomType.Combat },
                Layout = new[]
                {
                    ".......#.......",
                    ".......#.......",
                    "...............",
                    "...............",
                    "#......E.......",
                    "...............",
                    "...............",
                    "...............",
                    "...............",
                }
            };
            var r = RoomTemplateValidator.Validate(blockedDoor);
            Assert.IsFalse(r.IsValid);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("0,0:N")));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("0,0:W")));

            var walledIn = new RoomTemplate
            {
                Id = "walled", Types = new[] { RoomType.Combat },
                Layout = new[]
                {
                    "...............",
                    "...............",
                    "XXXXXXXXXXXXXXX",
                    "...............",
                    "...............",
                    "...............",
                    "XXXXXXXXXXXXXXX",
                    ".......E.......",
                    "...............",
                }
            };
            r = RoomTemplateValidator.Validate(walledIn);
            Assert.IsFalse(r.IsValid, "north and south halves are disconnected");

            var wrongSize = new RoomTemplate { Id = "small", Types = new[] { RoomType.Combat }, Layout = new[] { "..." } };
            Assert.IsFalse(RoomTemplateValidator.Validate(wrongSize).IsValid);
        }

        [Test]
        public void DuplicateAndMirroredLayoutsAreFlagged()
        {
            var a = TestContent.Database.RoomTemplates.First(t => t.Id == "combat_rubble_lanes");
            var mirrored = new RoomTemplate
            {
                Id = "mirror_copy", Shape = a.Shape, Types = a.Types,
                Layout = a.Layout.Select(row => new string(row.Reverse().ToArray())).ToArray()
            };
            var dupes = RoomTemplateValidator.FindDuplicates(new List<RoomTemplate> { a, mirrored });
            Assert.AreEqual(1, dupes.Count);
        }
    }

    public class FloorGenerationTests
    {
        private static FloorGenerator Generator(out FloorDefinition def)
        {
            var lib = TestContent.Database.BuildRoomLibrary(out _);
            def = TestContent.Database.Floors["ashen_crypt"];
            return new FloorGenerator(lib);
        }

        private static string Fingerprint(FloorLayout f)
            => FloorAsciiRenderer.Render(f) + "|" + string.Join(",", f.Rooms.Select(r => r.TemplateId + ":" + string.Join(".", r.Enemies.Select(e => e.EnemyId + e.TileX + e.TileY))));

        [Test]
        public void GeneratesValidFloorsAcrossManySeeds()
        {
            var gen = Generator(out var def);
            var lib = TestContent.Database.BuildRoomLibrary(out _);
            var rng = new SeededRandom(2026);
            int secrets = 0;
            for (int i = 0; i < 300; i++)
            {
                var seed = new RunSeed(rng.NextUInt());
                for (int floor = 0; floor < 2; floor++)
                {
                    var layout = gen.Generate(def, floor, seed, new GenerationHistory());
                    var report = FloorValidator.Validate(layout, lib);
                    Assert.IsTrue(report.IsValid, $"seed {seed} floor {floor}: {report}\n{FloorAsciiRenderer.Render(layout)}");
                    Assert.AreEqual(1, layout.Count(RoomType.Boss));
                    Assert.GreaterOrEqual(layout.Count(RoomType.Treasure), 1);
                    Assert.GreaterOrEqual(layout.Count(RoomType.Shop), 1);
                    Assert.GreaterOrEqual(layout.Rooms[layout.BossRoom].DistanceFromStart, 2);
                    Assert.IsNotNull(layout.BossId);
                    foreach (var room in layout.Rooms.Where(r => r.Type == RoomType.Combat))
                        Assert.Greater(room.Enemies.Count, 0, $"combat room {room.Index} empty");
                    secrets += layout.Count(RoomType.Secret);
                }
            }
            Assert.Greater(secrets, 500, "secret rooms should almost always fit");
        }

        [Test]
        public void SameSeedReproducesFloorExactly()
        {
            var gen = Generator(out var def);
            var seed = new RunSeed(0xC0FFEE);
            var a = gen.Generate(def, 0, seed, new GenerationHistory());
            var b = gen.Generate(def, 0, seed, new GenerationHistory());
            Assert.AreEqual(Fingerprint(a), Fingerprint(b));

            // Generating floor 1 must not depend on what other code did with randomness beforehand.
            var h1 = new GenerationHistory(); gen.Generate(def, 0, seed, h1);
            var h2 = new GenerationHistory(); gen.Generate(def, 0, seed, h2);
            Assert.AreEqual(Fingerprint(gen.Generate(def, 1, seed, h1)), Fingerprint(gen.Generate(def, 1, seed, h2)));
        }

        [Test]
        public void DifferentSeedsProduceVariety()
        {
            var gen = Generator(out var def);
            var prints = new HashSet<string>();
            for (uint s = 1; s <= 50; s++) prints.Add(FloorAsciiRenderer.Render(gen.Generate(def, 0, new RunSeed(s * 7919), new GenerationHistory())));
            Assert.Greater(prints.Count, 45);
        }

        [Test]
        public void AntiRepetitionSpreadsTemplateUse()
        {
            var gen = Generator(out var def);
            var history = new GenerationHistory();
            var seed = new RunSeed(31337);
            var counts = new Dictionary<string, int>();
            for (int floor = 0; floor < 6; floor++)
            {
                var f = gen.Generate(def, floor, seed, history);
                foreach (var r in f.Rooms.Where(r => r.Type == RoomType.Combat && r.Shape == RoomShape.Single))
                    counts[r.TemplateId] = counts.TryGetValue(r.TemplateId, out var n) ? n + 1 : 1;
            }
            int total = counts.Values.Sum();
            int max = counts.Values.Max();
            Assert.Greater(counts.Count, 3, "several different combat layouts used");
            Assert.Less(max, total * 0.5, "no single template dominates");
        }

        [Test]
        public void SecretRoomsUseSecretDoorsAndAvoidBoss()
        {
            var gen = Generator(out var def);
            for (uint s = 1; s <= 100; s++)
            {
                var f = gen.Generate(def, 0, new RunSeed(s), new GenerationHistory());
                foreach (var secret in f.Rooms.Where(r => r.Type == RoomType.Secret || r.Type == RoomType.HiddenVault))
                {
                    Assert.IsTrue(secret.Doors.All(d => d.Kind == DoorKind.Secret));
                    Assert.IsFalse(secret.Doors.Any(d => f.Rooms[d.ToRoom].Type == RoomType.Boss));
                }
                var boss = f.Rooms[f.BossRoom];
                Assert.IsTrue(boss.Doors.Where(d => d.Kind != DoorKind.Secret).All(d => d.Kind == DoorKind.Boss));
            }
        }

        [Test]
        public void LockedRoomsAppearFromSecondFloorAndAreDeadEnds()
        {
            var gen = Generator(out var def);
            var f = gen.Generate(def, 1, new RunSeed(4242), new GenerationHistory());
            var treasure = f.OfType(RoomType.Treasure).First();
            Assert.IsTrue(treasure.Doors.Where(d => d.Kind != DoorKind.Secret).All(d => d.Kind == DoorKind.Locked));
            Assert.AreEqual(1, treasure.Doors.Count(d => d.Kind != DoorKind.Secret));
            var f0 = gen.Generate(def, 0, new RunSeed(4242), new GenerationHistory());
            Assert.IsFalse(f0.OfType(RoomType.Treasure).First().Doors.Any(d => d.Kind == DoorKind.Locked));
        }
    }
}
