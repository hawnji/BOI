using System;
using System.Linq;
using Hollowcrawl.Combat;
using Hollowcrawl.Items;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Progression;
using Hollowcrawl.Rng;
using Hollowcrawl.Save;
using Hollowcrawl.Simulation;
using Hollowcrawl.Stats;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class ProgressionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

        private static RoomTemplateLibrary Lib()
        {
            var lib = new RoomTemplateLibrary();
            lib.AddRange(TestContent.Database.RoomTemplates);
            return lib;
        }

        private static RunSimulation Start(RunSetup setup) => new RunSimulation(setup, TestContent.Database, Lib(), EffectRegistry.CreateDefault());

        /// <summary>Beats every floor's boss and walks through the exits until the run ends.</summary>
        private static void WinRun(RunSimulation run)
        {
            for (int guard = 0; guard < 20 && run.State == RunState.Playing; guard++)
            {
                run.DebugEnterRoom(run.Layout.BossRoom);
                foreach (var e in run.CurrentRoom.Enemies.ToList()) { e.BossController?.Tick(9f); run.CurrentRoom.DamageEnemy(e, 1e7f, DamageType.Physical, false, default, 0f, null, 0, false); }
                for (int i = 0; i < 5 && run.CurrentRoom.LivingEnemies > 0; i++)
                {
                    foreach (var e in run.CurrentRoom.Enemies.ToList()) run.CurrentRoom.DamageEnemy(e, 1e7f, DamageType.Physical, false, default, 0f, null, 0, false);
                    run.Step(default);
                }
                run.Step(default);
                run.Player.Position = run.CurrentRoom.Exits[0].Position;
                run.Step(default); run.Step(default);
            }
            Assert.That(run.State, Is.EqualTo(RunState.Won));
        }

        [Test]
        public void ShippedCharactersAreDistinctAndOnlyTheWandererStartsUnlocked()
        {
            var db = TestContent.Database;
            Assert.That(db.Characters.Count, Is.GreaterThanOrEqualTo(12));
            var profile = new ProfileData();
            var available = db.Characters.Values.Where(c => RunSetupFactory.IsCharacterAvailable(c, profile)).Select(c => c.Id).ToList();
            Assert.That(available, Is.EqualTo(new[] { "wanderer" }));
            Assert.That(db.CharacterOrder.First(), Is.EqualTo("wanderer"));
        }

        [Test]
        public void CharacterSetupAppliesHealthItemsAndStats()
        {
            var db = TestContent.Database;
            var run = Start(RunSetupFactory.Standard(db, null, "bellwarden", new RunSeed(3)));
            Assert.That(run.Engine.Health.Containers, Is.EqualTo(4));
            Assert.That(run.Engine.ActiveItem?.Id, Is.EqualTo("storm_bell"));
            Assert.That(run.Engine.Stats[StatId.MoveSpeed], Is.EqualTo(StatTable.Get(StatId.MoveSpeed).Base - 0.15f).Within(1e-4f));
            run.Step(default);
            Assert.That(run.Engine.Health.Containers, Is.EqualTo(4), "sync keeps the character's container count");

            var saint = Start(RunSetupFactory.Standard(db, null, "hollow_saint", new RunSeed(3)));
            saint.Step(default);
            Assert.That(saint.Engine.Health.Containers, Is.EqualTo(0));
            Assert.That(saint.Engine.Health.Umbral, Is.EqualTo(6));
            Assert.That(saint.State, Is.EqualTo(RunState.Playing));
            saint.Engine.AddItem(TestContent.Item("iron_heart"));
            saint.Step(default);
            Assert.That(saint.Engine.Health.Containers, Is.EqualTo(0), "never gains containers");
            Assert.That(saint.Engine.Health.Umbral, Is.GreaterThan(6), "container gain became Umbral");

            var seer = Start(RunSetupFactory.Standard(db, null, "moth_seer", new RunSeed(3)));
            Assert.That(seer.ConsumableCapacity, Is.EqualTo(2));
            var lantern = Start(RunSetupFactory.Standard(db, null, "lanternbearer", new RunSeed(3)));
            Assert.That(lantern.ConsumableSlots, Is.EqualTo(new[] { "sigil_hearth" }));
        }

        [Test]
        public void DailyRunsAreTheSameForEveryone()
        {
            var db = TestContent.Database;
            var a = Start(RunSetupFactory.Daily(db, new ProfileData(), Now));
            var p = new ProfileData(); p.Unlocks.Add("char_gambler");
            var b = Start(RunSetupFactory.Daily(db, p, Now));
            Assert.That(a.Setup.Seed.Value, Is.EqualTo(b.Setup.Seed.Value));
            Assert.That(a.Layout.Rooms.Select(r => r.TemplateId), Is.EqualTo(b.Layout.Rooms.Select(r => r.TemplateId)));
            var tomorrow = Start(RunSetupFactory.Daily(db, p, Now.AddDays(1)));
            Assert.That(tomorrow.Setup.Seed.Value, Is.Not.EqualTo(a.Setup.Seed.Value));
        }

        [Test]
        public void ChallengeRulesApply()
        {
            var db = TestContent.Database;
            var pauper = Start(RunSetupFactory.Challenge(db, null, "ch_paupers_march", new RunSeed(9)));
            foreach (var node in pauper.Layout.Rooms.Where(r => r.Type == RoomType.Treasure || r.Type == RoomType.Shop))
            {
                pauper.DebugEnterRoom(node.Index);
                Assert.That(pauper.CurrentRoom.Pickups.Any(p => p.Pedestal), Is.False, node.Type.ToString());
            }
            var demo = Start(RunSetupFactory.Challenge(db, null, "ch_demolition", new RunSeed(9)));
            Assert.That(demo.Engine.Resources.Get(Economy.ResourceKind.Bombs), Is.GreaterThanOrEqualTo(30));
            Assert.That(demo.Setup.CharacterId, Is.EqualTo("tinker"));
        }

        [Test]
        public void WinningRecordsStatsMarksAchievementsAndUnlocks()
        {
            var db = TestContent.Database;
            var profile = new ProfileData();
            var run = Start(RunSetupFactory.Standard(db, profile, "wanderer", new RunSeed(21)));
            WinRun(run);
            var result = ProgressionService.RecordRun(profile, db, run, Now);
            Assert.That(result.Recorded, Is.True);
            Assert.That(profile.Stats.Wins, Is.EqualTo(1));
            Assert.That(profile.Stats.TotalRuns, Is.EqualTo(1));
            Assert.That(profile.Stats.BossesDefeated, Is.GreaterThanOrEqualTo(1));
            Assert.That(profile.CompletionMarks["wanderer"], Does.Contain(ProgressionService.MarkVictory));
            var ids = result.NewAchievements.Select(a => a.Id).ToList();
            Assert.That(ids, Does.Contain("first_steps"));
            Assert.That(ids, Does.Contain("first_light"));
            Assert.That(ids, Does.Contain("win_wanderer"));
            string bossAchievement = run.Stats.BossesDefeated.Contains("pyre_warden") ? "ashes_to_ashes" : "silence_the_choir";
            Assert.That(ids, Does.Contain(bossAchievement));
            Assert.That(profile.Unlocks, Does.Contain(bossAchievement == "ashes_to_ashes" ? "char_ashwright" : "char_bellwarden"));
            Assert.That(result.Score, Is.GreaterThan(5000));

            // achievements are only granted once
            var again = ProgressionService.RecordRun(profile, db, run, Now);
            Assert.That(again.NewAchievements.Select(a => a.Id), Does.Not.Contain("first_light"));
        }

        [Test]
        public void PracticeRecordsNothingAndCustomRecordsOnlyStatistics()
        {
            var db = TestContent.Database;
            var profile = new ProfileData();
            var practice = Start(RunSetupFactory.Practice(db, profile, "wanderer", new RunSeed(21)));
            WinRun(practice);
            Assert.That(ProgressionService.RecordRun(profile, db, practice, Now).Recorded, Is.False);
            Assert.That(profile.Stats.TotalRuns, Is.EqualTo(0));

            var custom = Start(RunSetupFactory.Custom(db, profile, "wanderer", new RunSeed(21)));
            WinRun(custom);
            var r = ProgressionService.RecordRun(profile, db, custom, Now);
            Assert.That(profile.Stats.Wins, Is.EqualTo(1));
            Assert.That(r.NewAchievements, Is.Empty);
            Assert.That(profile.Achievements, Is.Empty);
        }

        [Test]
        public void DeathBreaksTheStreakAndDailyKeepsTheBestScore()
        {
            var db = TestContent.Database;
            var profile = new ProfileData();
            profile.Stats.CurrentStreak = 2;
            var run = Start(RunSetupFactory.Daily(db, profile, Now));
            run.Engine.Health.TakeDamage(99, 0f, null, ignoreInvulnerability: true);
            Assert.That(run.State, Is.EqualTo(RunState.Lost));
            ProgressionService.RecordRun(profile, db, run, Now);
            Assert.That(profile.Stats.CurrentStreak, Is.EqualTo(0));
            Assert.That(profile.Stats.Deaths, Is.EqualTo(1));
            Assert.That(profile.DailyResults.ContainsKey("2026-11-22"), Is.True);
        }

        [Test]
        public void ChallengeCompletionUnlocksItsAchievement()
        {
            var db = TestContent.Database;
            var profile = new ProfileData();
            var run = Start(RunSetupFactory.Challenge(db, profile, "ch_glass_road", new RunSeed(4)));
            WinRun(run);
            var r = ProgressionService.RecordRun(profile, db, run, Now);
            Assert.That(profile.ChallengesCompleted, Does.Contain("ch_glass_road"));
            Assert.That(r.NewAchievements.Select(a => a.Id), Does.Contain("done_ch_glass_road"));
            Assert.That(profile.Stats.ChallengeCompletions, Is.EqualTo(1));
        }

        [Test]
        public void CounterAchievementsTriggerAtTheirThreshold()
        {
            var db = TestContent.Database;
            var profile = new ProfileData();
            profile.Stats.BombsUsed = 39;
            var res = new ProgressionResult();
            ProgressionService.EvaluateAchievements(profile, db, null, false, res, Now);
            Assert.That(profile.Achievements.ContainsKey("fuse_lit"), Is.False);
            profile.Stats.BombsUsed = 40;
            ProgressionService.EvaluateAchievements(profile, db, null, false, res, Now);
            Assert.That(profile.Achievements.ContainsKey("fuse_lit"), Is.True);
            Assert.That(profile.Unlocks, Does.Contain("char_tinker"));
        }

        [Test]
        public void ProfilesWithProgressionRoundTripThroughSaves()
        {
            var db = TestContent.Database;
            var profile = new ProfileData();
            var run = Start(RunSetupFactory.Standard(db, profile, "wanderer", new RunSeed(21)));
            WinRun(run);
            ProgressionService.RecordRun(profile, db, run, Now);
            var saves = new SaveService(new InMemorySaveStorage());
            saves.SaveProfile(0, profile);
            var loaded = saves.LoadProfile(0).Data;
            Assert.That(loaded.Achievements.Keys, Is.EquivalentTo(profile.Achievements.Keys));
            Assert.That(loaded.Unlocks, Is.EquivalentTo(profile.Unlocks));
            Assert.That(loaded.CompletionMarks["wanderer"], Is.EquivalentTo(profile.CompletionMarks["wanderer"]));
            Assert.That(loaded.Stats.WinsByCharacter["wanderer"], Is.EqualTo(1));
        }
    }
}
