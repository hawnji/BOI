using System.Linq;
using Hollowcrawl.Items;
using Hollowcrawl.Menus;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using Hollowcrawl.Save;
using Hollowcrawl.Simulation;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class MenuModelTests
    {
        private static SettingEntry Entry(string id) => SettingsMenu.Entries.Single(e => e.Id == id);

        [Test]
        public void SlidersStepAndClamp()
        {
            var s = new SettingsData { MasterVolume = 0.95f };
            Assert.That(SettingsMenu.Adjust(s, Entry("master"), +1), Is.True);
            Assert.That(s.MasterVolume, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(SettingsMenu.Adjust(s, Entry("master"), +1), Is.False, "already at max");
            for (int i = 0; i < 40; i++) SettingsMenu.Adjust(s, Entry("master"), -1);
            Assert.That(s.MasterVolume, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(SettingsMenu.Display(s, Entry("master")), Is.EqualTo("0%"));
        }

        [Test]
        public void ChoicesWrapAndTogglesFlip()
        {
            var s = new SettingsData { WindowMode = WindowMode.Windowed, VSync = true };
            SettingsMenu.Adjust(s, Entry("window"), +1);
            Assert.That(s.WindowMode, Is.EqualTo(WindowMode.Fullscreen));
            SettingsMenu.Adjust(s, Entry("window"), -1);
            Assert.That(s.WindowMode, Is.EqualTo(WindowMode.Windowed));
            SettingsMenu.Adjust(s, Entry("vsync"), +1);
            Assert.That(s.VSync, Is.False);
            Assert.That(SettingsMenu.Display(s, Entry("vsync")), Is.EqualTo("Off"));
            SettingsMenu.Adjust(s, Entry("fps"), +1);
            Assert.That(s.TargetFrameRate, Is.EqualTo(30));
            SettingsMenu.Adjust(s, Entry("palette"), -1);
            Assert.That(s.ColorPalette, Is.EqualTo("high_contrast"));
        }

        [Test]
        public void ResetKeepsBindingsAndClampRepairsBadValues()
        {
            var s = new SettingsData { MasterVolume = 0.1f, BindingOverridesJson = "{x}", ScreenShake = 0f, TargetFrameRate = 77, ColorPalette = "neon", UiScale = 9f };
            SettingsMenu.Clamp(s);
            Assert.That(s.TargetFrameRate, Is.EqualTo(0));
            Assert.That(s.ColorPalette, Is.EqualTo("default"));
            Assert.That(s.UiScale, Is.EqualTo(1.5f));
            SettingsMenu.ResetToDefaults(s);
            Assert.That(s.MasterVolume, Is.EqualTo(new SettingsData().MasterVolume));
            Assert.That(s.ScreenShake, Is.EqualTo(1f));
            Assert.That(s.BindingOverridesJson, Is.EqualTo("{x}"));
        }

        [Test]
        public void EveryRowHasALabelAndActionsDoNothingOnAdjust()
        {
            foreach (var e in SettingsMenu.Entries)
            {
                Assert.That(string.IsNullOrEmpty(e.Label), Is.False, e.Id);
                if (e.Kind == SettingKind.Action) Assert.That(SettingsMenu.Adjust(new SettingsData(), e, 1), Is.False);
                else Assert.That(SettingsMenu.Display(new SettingsData(), e), Is.Not.Empty, e.Id);
            }
        }

        [Test]
        public void InventoryListsStatsItemsTrinketsAndFloor()
        {
            var lib = new RoomTemplateLibrary();
            lib.AddRange(TestContent.Database.RoomTemplates);
            var run = new RunSimulation(new RunSetup { Seed = new RunSeed(3) }, TestContent.Database, lib, EffectRegistry.CreateDefault());
            run.Engine.AddItem(TestContent.Database.Items["needle_quill"]);
            run.Engine.AddItem(TestContent.Database.Items["storm_bell"]);
            run.Engine.AddItem(TestContent.Database.Items["brass_tally"]);
            var view = InventoryView.Build(run);
            Assert.That(view.Select(s => s.Title), Does.Contain("Stats").And.Contain("Items").And.Contain("Active").And.Contain("Trinkets").And.Contain("This floor"));
            Assert.That(view.Single(s => s.Title == "Items").Rows.Any(r => r.Name == "Needle Quill"), Is.True);
            Assert.That(view.Single(s => s.Title == "Active").Rows.Single().Name, Is.EqualTo("Storm Bell"));
            Assert.That(view.Single(s => s.Title == "Stats").Rows.Count, Is.EqualTo(8));
        }
    }
}
