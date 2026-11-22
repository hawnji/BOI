using System;
using System.Collections.Generic;
using System.Globalization;
using Hollowcrawl.Save;

namespace Hollowcrawl.Menus
{
    public enum SettingKind { Slider, Toggle, Choice, Action }

    /// <summary>One row of the settings screen. Pure data plus accessors so the menu is unit-tested headless.</summary>
    public sealed class SettingEntry
    {
        public string Id;
        public string Label;
        public string Section;
        public SettingKind Kind;
        public float Min, Max = 1f, Step = 0.1f;
        public bool Percent;
        public string[] Choices;
        public Func<SettingsData, float> Get;
        public Action<SettingsData, float> Set;
    }

    /// <summary>
    /// The settings screen as data: audio, graphics, accessibility and input rows. Left/Right adjusts the
    /// selected row (sliders step and clamp, choices wrap, toggles flip). Unity applies the result through
    /// SettingsApplier; this class never touches the engine.
    /// </summary>
    public static class SettingsMenu
    {
        public const string ControlsAction = "controls";
        public const string ResetAction = "reset";

        public static readonly int[] FrameRates = { 0, 30, 60, 120, 144, 240 };
        public static readonly string[] Palettes = { "default", "deuteranopia", "protanopia", "tritanopia", "high_contrast" };

        public static readonly IReadOnlyList<SettingEntry> Entries = Build();

        private static List<SettingEntry> Build()
        {
            var l = new List<SettingEntry>();
            void Slider(string id, string label, string section, float min, float max, float step, Func<SettingsData, float> get, Action<SettingsData, float> set, bool percent = true)
                => l.Add(new SettingEntry { Id = id, Label = label, Section = section, Kind = SettingKind.Slider, Min = min, Max = max, Step = step, Percent = percent, Get = get, Set = set });
            void Toggle(string id, string label, string section, Func<SettingsData, bool> get, Action<SettingsData, bool> set)
                => l.Add(new SettingEntry { Id = id, Label = label, Section = section, Kind = SettingKind.Toggle, Get = s => get(s) ? 1f : 0f, Set = (s, v) => set(s, v > 0.5f) });
            void Choice(string id, string label, string section, string[] choices, Func<SettingsData, int> get, Action<SettingsData, int> set)
                => l.Add(new SettingEntry { Id = id, Label = label, Section = section, Kind = SettingKind.Choice, Choices = choices, Get = s => get(s), Set = (s, v) => set(s, (int)v) });

            Slider("master", "Master volume", "Audio", 0f, 1f, 0.05f, s => s.MasterVolume, (s, v) => s.MasterVolume = v);
            Slider("music", "Music volume", "Audio", 0f, 1f, 0.05f, s => s.MusicVolume, (s, v) => s.MusicVolume = v);
            Slider("sfx", "Effects volume", "Audio", 0f, 1f, 0.05f, s => s.SfxVolume, (s, v) => s.SfxVolume = v);
            Slider("ui", "Interface volume", "Audio", 0f, 1f, 0.05f, s => s.UiVolume, (s, v) => s.UiVolume = v);
            Toggle("mute_unfocused", "Mute when unfocused", "Audio", s => s.MuteWhenUnfocused, (s, v) => s.MuteWhenUnfocused = v);

            Choice("window", "Window mode", "Graphics", new[] { "Fullscreen", "Borderless", "Windowed" }, s => (int)s.WindowMode, (s, v) => s.WindowMode = (WindowMode)v);
            Toggle("vsync", "V-sync", "Graphics", s => s.VSync, (s, v) => s.VSync = v);
            Choice("fps", "Frame rate cap", "Graphics", new[] { "Unlimited", "30", "60", "120", "144", "240" },
                s => Math.Max(0, Array.IndexOf(FrameRates, s.TargetFrameRate)), (s, v) => s.TargetFrameRate = FrameRates[v]);
            Slider("ui_scale", "Interface scale", "Graphics", 0.75f, 1.5f, 0.05f, s => s.UiScale, (s, v) => s.UiScale = v);

            Slider("shake", "Screen shake", "Accessibility", 0f, 1f, 0.1f, s => s.ScreenShake, (s, v) => s.ScreenShake = v);
            Toggle("flashing", "Reduce flashing", "Accessibility", s => s.ReduceFlashing, (s, v) => s.ReduceFlashing = v);
            Slider("text_scale", "Text size", "Accessibility", 0.8f, 1.6f, 0.1f, s => s.TextScale, (s, v) => s.TextScale = v);
            Choice("palette", "Colour palette", "Accessibility", new[] { "Default", "Deuteranopia", "Protanopia", "Tritanopia", "High contrast" },
                s => Math.Max(0, Array.IndexOf(Palettes, s.ColorPalette)), (s, v) => s.ColorPalette = Palettes[v]);
            Toggle("damage_numbers", "Damage numbers", "Accessibility", s => s.ShowDamageNumbers, (s, v) => s.ShowDamageNumbers = v);
            Slider("aim_assist", "Aim assist (controller)", "Accessibility", 0f, 1f, 0.05f, s => s.AimAssist, (s, v) => s.AimAssist = v);

            var modes = new[] { "Hold to fire", "Toggle auto-fire", "Aim to fire" };
            Choice("pad_shoot", "Controller shooting", "Input", modes, s => (int)s.GamepadShootMode, (s, v) => s.GamepadShootMode = (ShootMode)v);
            Choice("mouse_shoot", "Mouse shooting", "Input", modes, s => (int)s.MouseShootMode, (s, v) => s.MouseShootMode = (ShootMode)v);
            Slider("deadzone", "Stick deadzone", "Input", 0.05f, 0.5f, 0.05f, s => s.StickDeadzone, (s, v) => s.StickDeadzone = v);
            Toggle("vibration", "Vibration", "Input", s => s.VibrationEnabled, (s, v) => s.VibrationEnabled = v);
            l.Add(new SettingEntry { Id = ControlsAction, Label = "Rebind controls...", Section = "Input", Kind = SettingKind.Action });
            l.Add(new SettingEntry { Id = ResetAction, Label = "Reset all settings", Section = "Input", Kind = SettingKind.Action });
            return l;
        }

        /// <summary>Applies a Left (-1) or Right (+1) press to a row. Returns true when the value changed.</summary>
        public static bool Adjust(SettingsData s, SettingEntry e, int direction)
        {
            if (e.Kind == SettingKind.Action || direction == 0) return false;
            float before = e.Get(s);
            switch (e.Kind)
            {
                case SettingKind.Toggle: e.Set(s, before > 0.5f ? 0f : 1f); break;
                case SettingKind.Choice:
                {
                    int n = e.Choices.Length;
                    e.Set(s, (((int)before + direction) % n + n) % n);
                    break;
                }
                default:
                {
                    float v = (float)Math.Round((before + direction * e.Step) / e.Step) * e.Step;
                    e.Set(s, Math.Max(e.Min, Math.Min(e.Max, v)));
                    break;
                }
            }
            return Math.Abs(e.Get(s) - before) > 1e-5f;
        }

        public static string Display(SettingsData s, SettingEntry e)
        {
            switch (e.Kind)
            {
                case SettingKind.Toggle: return e.Get(s) > 0.5f ? "On" : "Off";
                case SettingKind.Choice: return e.Choices[Math.Max(0, Math.Min(e.Choices.Length - 1, (int)e.Get(s)))];
                case SettingKind.Slider:
                    return e.Percent && e.Max <= 1f ? Math.Round(e.Get(s) * 100f).ToString(CultureInfo.InvariantCulture) + "%"
                        : e.Get(s).ToString("0.00", CultureInfo.InvariantCulture) + "x";
                default: return "";
            }
        }

        /// <summary>Restores defaults but keeps binding overrides, language and the last profile slot.</summary>
        public static void ResetToDefaults(SettingsData s)
        {
            var d = new SettingsData();
            foreach (var e in Entries) if (e.Kind != SettingKind.Action) e.Set(s, e.Get(d));
            s.ResolutionWidth = d.ResolutionWidth;
            s.ResolutionHeight = d.ResolutionHeight;
        }

        /// <summary>Sanitises loaded settings (hand-edited or older files) into the ranges the menu allows.</summary>
        public static void Clamp(SettingsData s)
        {
            foreach (var e in Entries)
            {
                if (e.Kind == SettingKind.Slider) e.Set(s, Math.Max(e.Min, Math.Min(e.Max, e.Get(s))));
                else if (e.Kind == SettingKind.Choice) { int v = (int)e.Get(s); if (v < 0 || v >= e.Choices.Length) e.Set(s, 0); }
            }
            if (Array.IndexOf(FrameRates, s.TargetFrameRate) < 0) s.TargetFrameRate = 0;
            if (Array.IndexOf(Palettes, s.ColorPalette) < 0) s.ColorPalette = "default";
        }
    }
}
