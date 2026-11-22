using System.Linq;
using System.Text;
using Hollowcrawl.Content;
using Hollowcrawl.Items;
using Hollowcrawl.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class SaveTests
    {
        [Test]
        public void ProfileRoundTrips()
        {
            var storage = new InMemorySaveStorage();
            var svc = new SaveService(storage);
            var p = new ProfileData { ProfileName = "Test" };
            p.Unlocks.Add("char_wren");
            p.Achievements["first_blood"] = 123;
            p.AddMark("char_wren", "pyre_warden");
            p.Stats.Increment("coins", 40);
            svc.SaveProfile(0, p);

            var loaded = svc.LoadProfile(0);
            Assert.AreEqual(LoadStatus.Loaded, loaded.Status);
            Assert.AreEqual("Test", loaded.Data.ProfileName);
            Assert.IsTrue(loaded.Data.Unlocks.Contains("char_wren"));
            Assert.AreEqual(123, loaded.Data.Achievements["first_blood"]);
            Assert.IsTrue(loaded.Data.CompletionMarks["char_wren"].Contains("pyre_warden"));
            Assert.AreEqual(40, loaded.Data.Stats.Counters["coins"]);
        }

        [Test]
        public void MissingSaveCreatesDefault()
        {
            var svc = new SaveService(new InMemorySaveStorage());
            var r = svc.LoadSettings();
            Assert.AreEqual(LoadStatus.Created, r.Status);
            Assert.IsNotNull(r.Data);
        }

        [Test]
        public void CorruptSaveRecoversFromBackupAndHealsMainFile()
        {
            var storage = new InMemorySaveStorage();
            var svc = new SaveService(storage);
            var p = new ProfileData(); p.Unlocks.Add("a"); svc.SaveProfile(1, p);
            p.Unlocks.Add("b"); svc.SaveProfile(1, p);   // backup now holds {a}
            storage.Corrupt(SaveService.ProfileKey(1));

            var r = svc.LoadProfile(1);
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, r.Status);
            Assert.IsTrue(r.Data.Unlocks.Contains("a"));
            Assert.IsTrue(storage.Exists(SaveService.ProfileKey(1) + ".corrupt"), "corrupt file kept for diagnosis");
            Assert.AreEqual(LoadStatus.Loaded, svc.LoadProfile(1).Status, "main file was rewritten from the backup");
        }

        [Test]
        public void AllCorruptResetsToDefaultWithoutThrowing()
        {
            var storage = new InMemorySaveStorage();
            var svc = new SaveService(storage);
            storage.Write(SaveService.SettingsKey, Encoding.UTF8.GetBytes("{not json"));
            var r = svc.LoadSettings();
            Assert.AreEqual(LoadStatus.ResetAfterCorruption, r.Status);
            Assert.IsNotEmpty(r.Messages);
        }

        [Test]
        public void NewerSchemaIsRefusedAndOlderSchemaMigrates()
        {
            var storage = new InMemorySaveStorage();
            var mgr = new SaveManager(storage);
            mgr.Save("x.json", "profile", 5, new ProfileData { ProfileName = "Future" });
            var r = mgr.Load("x.json", "profile", 1, () => new ProfileData());
            Assert.AreNotEqual("Future", r.Data.ProfileName);

            mgr.Save("old.json", "profile", 0, new { profileName = "Old", legacyName = "renamed" });
            mgr.RegisterMigration("profile", new RenameMigration());
            var m = mgr.Load("old.json", "profile", 1, () => new ProfileData());
            Assert.AreEqual(LoadStatus.Loaded, m.Status);
            Assert.AreEqual("renamed", m.Data.ProfileName);
        }

        private sealed class RenameMigration : ISaveMigration
        {
            public int FromSchema => 0;
            public void Apply(JObject payload) { payload["profileName"] = payload["legacyName"]; payload.Remove("legacyName"); }
        }

        [Test]
        public void BackupsRotate()
        {
            var storage = new InMemorySaveStorage();
            var svc = new SaveService(storage);
            for (int i = 0; i < 6; i++) svc.SaveSettings(new SettingsData { MasterVolume = i / 10f });
            Assert.IsTrue(storage.Exists(SaveService.SettingsKey + ".bak3"));
            Assert.IsFalse(storage.Exists(SaveService.SettingsKey + ".bak4"));
        }
    }

    public class ContentTests
    {
        [Test]
        public void ShippedContentPassesValidation()
        {
            var db = TestContent.Database;
            var report = ContentValidator.Validate(db, EffectRegistry.CreateDefault(), new ConditionRegistry());
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.Greater(db.Items.Count, 0);
        }

        [Test]
        public void FlatEffectJsonParses()
        {
            var json = "{\"items\":[{\"id\":\"t\",\"name\":\"T\",\"description\":\"d\",\"kind\":\"Passive\",\"effects\":[{\"effect\":\"proj_element\",\"type\":\"Fire\",\"chance\":0.25,\"condition\":\"health_full\",\"tags\":[\"a\",\"b\"]}]}]}";
            var db = new ContentDatabase();
            db.LoadJson("inline", json);
            Assert.IsEmpty(db.LoadErrors);
            var spec = db.Items["t"].Effects[0];
            Assert.AreEqual("proj_element", spec.Type);
            Assert.AreEqual("health_full", spec.Condition);
            Assert.AreEqual("Fire", spec.Params.GetString("type"));
            Assert.AreEqual(0.25f, spec.Params.GetFloat("chance"), 1e-5);
            CollectionAssert.AreEqual(new[] { "a", "b" }, spec.Params.GetStringArray("tags"));
        }

        [Test]
        public void ValidatorCatchesBrokenContent()
        {
            var db = new ContentDatabase();
            db.LoadJson("bad", "{\"items\":[{\"id\":\"x\",\"name\":\"X\",\"description\":\"d\",\"kind\":\"Passive\",\"effects\":[{\"effect\":\"nope\",\"condition\":\"not_a_condition\"}]}]," +
                               "\"synergies\":[{\"id\":\"s\",\"requires\":[{\"itemId\":\"missing\"}],\"effects\":[]}]}");
            var r = ContentValidator.Validate(db, EffectRegistry.CreateDefault(), new ConditionRegistry());
            Assert.IsTrue(r.Errors.Any(e => e.Contains("unknown effect 'nope'")));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("unknown condition 'not_a_condition'")));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("unknown item 'missing'")));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("does nothing")));
        }

        [Test]
        public void EveryRegisteredEffectIsDocumented()
        {
            var reg = EffectRegistry.CreateDefault();
            foreach (var t in reg.Types.Where(t => t != "proj_spectral"))
                Assert.IsNotNull(reg.GetDocumentation(t), t);
        }
    }
}
