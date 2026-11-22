using System;
using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Bootstrap;
using Hollowcrawl.Input;
using Hollowcrawl.Menus;
using Hollowcrawl.Progression;
using Hollowcrawl.Rng;
using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Placeholder front end (IMGUI): title, mode select, character and difficulty select, challenges,
    /// statistics/achievements and the run summary. Fully usable with keyboard or controller through
    /// <see cref="MenuNavigator"/>; mouse clicks also work. Replaced by the final uGUI menus in the UI phase.
    /// </summary>
    public sealed class FrontEnd : MonoBehaviour
    {
        public enum Screen { Title, CharacterSelect, Challenges, Stats, Summary, Hidden, Settings, Controls, Pause, Inventory }

        public RunDirector Director;
        public Screen Current = Screen.Title;
        public ProgressionResult LastResult;

        private readonly MenuNavigator _nav = new MenuNavigator();
        private int _index, _characterIndex, _difficultyIndex = 1;
        private GameMode _pendingMode = GameMode.Standard;
        private string _seedText = "";
        private GUIStyle _title, _item, _small;
        private Vector2 _scroll;

        private static readonly string[] Difficulties = { "story", "normal", "hard", "nightmare" };
        private static readonly string[] TitleEntries = { "New Run", "Daily Descent", "Challenges", "Practice (no progress)", "Custom Seed", "Statistics", "Settings", "Quit" };
        private static readonly string[] PauseEntries = { "Resume", "Inventory", "Settings", "Give up this run" };
        private static readonly GameAction[] RebindableActions =
            { GameAction.Fire, GameAction.ActiveItem, GameAction.Bomb, GameAction.Consumable, GameAction.SwapConsumable, GameAction.DropTrinket, GameAction.Interact, GameAction.Map, GameAction.Pause };

        /// <summary>Frame of the last screen change, so one key press is never handled twice (menu + pause toggle).</summary>
        public int LastScreenChangeFrame { get; private set; } = -1;
        private Screen _settingsReturn = Screen.Title;
        private int _controlsDevice; // 0 keyboard/mouse, 1 gamepad
        private bool _rebinding;
        private bool _settingsDirty;

        private void Update()
        {
            if (Current == Screen.Hidden && Director != null && Director.Paused) Go(Screen.Pause);
            if ((Current == Screen.Pause || Current == Screen.Inventory || (Current == Screen.Settings && _settingsReturn == Screen.Pause)) && Director != null && !Director.Paused) Go(Screen.Hidden);
            if (Current == Screen.Hidden || _rebinding) return;
            var src = GameServices.PlayerInput as IMenuInputSource;
            var cmd = src != null ? _nav.Update(src.ReadMenuFrame(), Time.unscaledDeltaTime) : MenuCommand.None;
            if (cmd != MenuCommand.None)
            {
                AudioService.Ensure().PlayUi(cmd == MenuCommand.Submit ? "ui_select" : cmd == MenuCommand.Cancel ? "ui_back" : "ui_move");
                Handle(cmd);
            }
        }

        private void Handle(MenuCommand cmd)
        {
            var content = GameServices.Content;
            var profile = GameServices.Profile;
            switch (Current)
            {
                case Screen.Title:
                    if (cmd == MenuCommand.Submit) ChooseTitle(_index);
                    else if (cmd == MenuCommand.Cancel) { }
                    else _index = MenuNavigator.Step(_index, TitleEntries.Length, cmd);
                    break;
                case Screen.CharacterSelect:
                {
                    var chars = content.CharacterOrder;
                    if (cmd == MenuCommand.Left || cmd == MenuCommand.Right || cmd == MenuCommand.TabLeft || cmd == MenuCommand.TabRight)
                        _characterIndex = MenuNavigator.Step(_characterIndex, chars.Count, cmd == MenuCommand.TabLeft ? MenuCommand.Left : cmd == MenuCommand.TabRight ? MenuCommand.Right : cmd, horizontal: true);
                    else if (cmd == MenuCommand.Up || cmd == MenuCommand.Down) _difficultyIndex = Mathf.Clamp(_difficultyIndex + (cmd == MenuCommand.Down ? 1 : -1), 0, Difficulties.Length - 1);
                    else if (cmd == MenuCommand.Cancel) Go(Screen.Title);
                    else if (cmd == MenuCommand.Submit)
                    {
                        var c = content.Characters[chars[_characterIndex]];
                        if (!RunSetupFactory.IsCharacterAvailable(c, profile)) break;
                        var seed = _pendingMode == GameMode.Custom && RunSeed.TryParse(_seedText.Trim().ToUpperInvariant(), out var typed) ? typed : RunSeed.FromEntropy();
                        var diff = Difficulties[_difficultyIndex];
                        var setup = _pendingMode == GameMode.Practice ? RunSetupFactory.Practice(content, profile, c.Id, seed, diff)
                            : _pendingMode == GameMode.Custom ? RunSetupFactory.Custom(content, profile, c.Id, seed, diff)
                            : RunSetupFactory.Standard(content, profile, c.Id, seed, diff);
                        Launch(setup);
                    }
                    break;
                }
                case Screen.Challenges:
                {
                    var list = content.Challenges.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
                    if (cmd == MenuCommand.Cancel) Go(Screen.Title);
                    else if (cmd == MenuCommand.Submit && list.Count > 0)
                    {
                        var ch = list[_index];
                        if (profile.IsUnlocked(ch.UnlockId)) Launch(RunSetupFactory.Challenge(content, profile, ch.Id, RunSeed.FromEntropy()));
                    }
                    else _index = MenuNavigator.Step(_index, list.Count, cmd);
                    break;
                }
                case Screen.Stats:
                    if (cmd == MenuCommand.Cancel || cmd == MenuCommand.Submit) Go(Screen.Title);
                    else if (cmd == MenuCommand.Up) _scroll.y -= 40f;
                    else if (cmd == MenuCommand.Down) _scroll.y += 40f;
                    break;
                case Screen.Summary:
                    if (cmd == MenuCommand.Submit || cmd == MenuCommand.Cancel) { Director.ReturnToMenu(); Go(Screen.Title); }
                    break;
                case Screen.Pause:
                    if (cmd == MenuCommand.Cancel) Director.Resume();
                    else if (cmd == MenuCommand.Submit) ChoosePause(_index);
                    else _index = MenuNavigator.Step(_index, PauseEntries.Length, cmd);
                    break;
                case Screen.Inventory:
                    if (cmd == MenuCommand.Cancel || cmd == MenuCommand.Submit) Go(Screen.Pause, 1);
                    else if (cmd == MenuCommand.Up) _scroll.y = Mathf.Max(0f, _scroll.y - 40f);
                    else if (cmd == MenuCommand.Down) _scroll.y += 40f;
                    break;
                case Screen.Settings:
                    HandleSettings(cmd);
                    break;
                case Screen.Controls:
                    HandleControls(cmd);
                    break;
            }
        }

        private void ChoosePause(int i)
        {
            switch (i)
            {
                case 0: Director.Resume(); break;
                case 1: Go(Screen.Inventory); break;
                case 2: OpenSettings(Screen.Pause); break;
                case 3: Director.GiveUp(); Go(Screen.Hidden); break;
            }
        }

        private void OpenSettings(Screen returnTo) { _settingsReturn = returnTo; _settingsDirty = false; Go(Screen.Settings); }

        private void HandleSettings(MenuCommand cmd)
        {
            var settings = GameServices.Settings;
            var entries = SettingsMenu.Entries;
            if (settings == null) { Go(_settingsReturn); return; }
            var e = entries[Mathf.Clamp(_index, 0, entries.Count - 1)];
            switch (cmd)
            {
                case MenuCommand.Cancel:
                    if (_settingsDirty) GameServices.SaveSettings();
                    Go(_settingsReturn, _settingsReturn == Screen.Pause ? 2 : TitleEntries.Length - 2);
                    break;
                case MenuCommand.Left:
                case MenuCommand.Right:
                    if (SettingsMenu.Adjust(settings, e, cmd == MenuCommand.Right ? 1 : -1)) { _settingsDirty = true; Bootstrap.SettingsApplier.Apply(settings); }
                    break;
                case MenuCommand.Submit:
                    if (e.Id == SettingsMenu.ControlsAction) Go(Screen.Controls);
                    else if (e.Id == SettingsMenu.ResetAction) { SettingsMenu.ResetToDefaults(settings); _settingsDirty = true; Bootstrap.SettingsApplier.Apply(settings); }
                    else if (SettingsMenu.Adjust(settings, e, 1)) { _settingsDirty = true; Bootstrap.SettingsApplier.Apply(settings); }
                    break;
                default:
                    _index = MenuNavigator.Step(_index, entries.Count, cmd);
                    break;
            }
        }

        private void HandleControls(MenuCommand cmd)
        {
            var rebinder = GameServices.Rebinder;
            int rows = RebindableActions.Length + 1; // + "reset bindings"
            switch (cmd)
            {
                case MenuCommand.Cancel: Go(Screen.Settings, SettingsMenu.Entries.Count - 2); break;
                case MenuCommand.Left:
                case MenuCommand.Right:
                case MenuCommand.TabLeft:
                case MenuCommand.TabRight:
                    _controlsDevice = 1 - _controlsDevice; break;
                case MenuCommand.Submit:
                    if (rebinder == null) break;
                    if (_index == RebindableActions.Length) { rebinder.ResetAll(); _settingsDirty = true; GameServices.SaveSettings(); break; }
                    _rebinding = true;
                    rebinder.StartRebind(RebindableActions[_index], _controlsDevice == 0 ? InputDeviceKind.KeyboardMouse : InputDeviceKind.Gamepad, ok =>
                    {
                        _rebinding = false;
                        if (ok) GameServices.SaveSettings();
                    });
                    break;
                default: _index = MenuNavigator.Step(_index, rows, cmd); break;
            }
        }

        private void ChooseTitle(int i)
        {
            switch (i)
            {
                case 0: _pendingMode = GameMode.Standard; Go(Screen.CharacterSelect); break;
                case 1: Launch(RunSetupFactory.Daily(GameServices.Content, GameServices.Profile, DateTime.UtcNow.Date)); break;
                case 2: Go(Screen.Challenges); break;
                case 3: _pendingMode = GameMode.Practice; Go(Screen.CharacterSelect); break;
                case 4: _pendingMode = GameMode.Custom; Go(Screen.CharacterSelect); break;
                case 5: Go(Screen.Stats); break;
                case 6: OpenSettings(Screen.Title); break;
                case 7: GameServices.SaveProfile(); Application.Quit(); break;
            }
        }

        private void Launch(RunSetup setup) { Go(Screen.Hidden); Director.StartRun(setup); }

        public void Go(Screen s, int index = 0) { Current = s; _index = index; _scroll = Vector2.zero; LastScreenChangeFrame = Time.frameCount; }

        public void ShowSummary(ProgressionResult result) { LastResult = result; Go(Screen.Summary); }

        // ------------------------------------------------------------------ drawing

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 44, alignment = TextAnchor.MiddleCenter, richText = true };
            _item = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperCenter, richText = true, wordWrap = true };
        }

        private void OnGUI()
        {
            if (Current == Screen.Hidden || GameServices.Content == null) return;
            EnsureStyles();
            float scale = UnityEngine.Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = UnityEngine.Screen.width / scale;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Current == Screen.Summary ? 0.75f : 0.92f);
            GUI.DrawTexture(new Rect(0, 0, w, 720f), Texture2D.whiteTexture);
            GUI.color = old;

            switch (Current)
            {
                case Screen.Title: DrawTitle(w); break;
                case Screen.CharacterSelect: DrawCharacters(w); break;
                case Screen.Challenges: DrawChallenges(w); break;
                case Screen.Stats: DrawStats(w); break;
                case Screen.Summary: DrawSummary(w); break;
                case Screen.Pause: DrawPause(w); break;
                case Screen.Inventory: DrawInventory(w); break;
                case Screen.Settings: DrawSettings(w); break;
                case Screen.Controls: DrawControls(w); break;
            }
        }

        private void DrawPause(float w)
        {
            GUI.Label(new Rect(0, 110, w, 60), "<b>Paused</b>", _title);
            for (int i = 0; i < PauseEntries.Length; i++)
                if (Entry(new Rect(w * 0.5f - 200f, 220f + i * 46f, 400f, 40f), PauseEntries[i], i == _index)) { _index = i; ChoosePause(i); }
            GUI.Label(new Rect(0, 650, w, 30), "<color=#999999>Back / Pause: resume</color>", _small);
        }

        private void DrawInventory(float w)
        {
            GUI.Label(new Rect(0, 30, w, 50), "<b>Inventory</b>", _title);
            var sections = InventoryView.Build(Director.Run);
            float colW = Mathf.Min(560f, w * 0.45f);
            var left = new Rect(w * 0.5f - colW - 10f, 100f, colW, 560f);
            var right = new Rect(w * 0.5f + 10f, 100f, colW, 560f);
            float yl = 0f, yr = 0f;
            foreach (var sec in sections)
            {
                bool onLeft = sec.Title == "Stats" || sec.Title == "Active" || sec.Title == "This floor" || sec.Title == "Consumables";
                var col = onLeft ? left : right;
                ref float y = ref (onLeft ? ref yl : ref yr);
                float sy = y - (onLeft ? 0f : _scroll.y);
                if (sy > -30f && sy < col.height) GUI.Label(new Rect(col.x, col.y + sy, col.width, 26), "<b><color=#ffd36b>" + sec.Title + "</color></b>", _small);
                y += 28f;
                foreach (var row in sec.Rows)
                {
                    sy = y - (onLeft ? 0f : _scroll.y);
                    if (sy > -30f && sy < col.height)
                        GUI.Label(new Rect(col.x, col.y + sy, col.width, 40), "<b>" + row.Name + "</b>" + (string.IsNullOrEmpty(row.Detail) ? "" : "  <color=#bbbbbb>" + row.Detail + "</color>"), _small);
                    y += string.IsNullOrEmpty(row.Detail) || row.Detail.Length < 50 ? 22f : 38f;
                }
                y += 10f;
            }
            GUI.Label(new Rect(0, 670, w, 30), "<color=#999999>Up/Down: scroll items    Back: return</color>", _small);
        }

        private void DrawSettings(float w)
        {
            GUI.Label(new Rect(0, 24, w, 50), "<b>Settings</b>", _title);
            var settings = GameServices.Settings;
            if (settings == null) return;
            var entries = SettingsMenu.Entries;
            float rowH = 26f, top = 90f;
            int visible = 21, first = Mathf.Clamp(_index - visible / 2, 0, Mathf.Max(0, entries.Count - visible));
            string section = null;
            float y = top;
            for (int i = first; i < Mathf.Min(entries.Count, first + visible); i++)
            {
                var e = entries[i];
                if (e.Section != section) { section = e.Section; GUI.Label(new Rect(w * 0.5f - 300f, y, 600f, rowH), "<color=#888888>" + section + "</color>", _small); y += rowH; }
                string label = i == _index ? "<color=#ffd36b>" + e.Label + "</color>" : e.Label;
                string value = e.Kind == SettingKind.Action ? "" : "< " + SettingsMenu.Display(settings, e) + " >";
                if (GUI.Button(new Rect(w * 0.5f - 300f, y, 400f, rowH), label, _small)) { _index = i; HandleSettings(MenuCommand.Submit); }
                if (GUI.Button(new Rect(w * 0.5f + 100f, y, 200f, rowH), i == _index ? "<color=#ffd36b>" + value + "</color>" : value, _small)) { _index = i; HandleSettings(MenuCommand.Right); }
                y += rowH;
            }
            GUI.Label(new Rect(0, 680, w, 30), "<color=#999999>Left/Right: change    Confirm: open/toggle    Back: save and return</color>", _small);
        }

        private void DrawControls(float w)
        {
            GUI.Label(new Rect(0, 30, w, 50), "<b>Controls</b>", _title);
            var rebinder = GameServices.Rebinder;
            var device = _controlsDevice == 0 ? InputDeviceKind.KeyboardMouse : InputDeviceKind.Gamepad;
            GUI.Label(new Rect(0, 90, w, 30), (_controlsDevice == 0 ? "<color=#ffd36b>Keyboard and mouse</color>   Controller" : "Keyboard and mouse   <color=#ffd36b>Controller</color>") + "   <color=#999999>(Left/Right to switch)</color>", _small);
            for (int i = 0; i <= RebindableActions.Length; i++)
            {
                float y = 130f + i * 34f;
                bool sel = i == _index;
                string label = i == RebindableActions.Length ? "Reset all bindings" : RebindableActions[i].ToString();
                string bind = i == RebindableActions.Length || rebinder == null ? "" : rebinder.GetBindingDisplay(RebindableActions[i], device);
                if (sel && _rebinding) bind = "<color=#ff9060>press a button...</color>";
                string text = (sel ? "<color=#ffd36b>" : "") + label + (sel ? "</color>" : "") + (bind.Length > 0 ? "    " + bind : "");
                if (GUI.Button(new Rect(w * 0.5f - 300f, y, 600f, 30f), text, _small) && !_rebinding) { _index = i; HandleControls(MenuCommand.Submit); }
            }
            GUI.Label(new Rect(0, 660, w, 30), "<color=#999999>Confirm: rebind    Back: return</color>", _small);
        }

        private bool Entry(Rect r, string text, bool selected, bool enabled = true)
        {
            string shown = !enabled ? "<color=#666666>" + text + "</color>" : selected ? "<color=#ffd36b>> " + text + " <</color>" : text;
            return GUI.Button(r, shown, _item) && enabled;
        }

        private void DrawTitle(float w)
        {
            GUI.Label(new Rect(0, 90, w, 70), "<b>HOLLOWCRAWL</b>", _title);
            for (int i = 0; i < TitleEntries.Length; i++)
                if (Entry(new Rect(w * 0.5f - 200f, 220f + i * 46f, 400f, 40f), TitleEntries[i], i == _index)) { _index = i; ChooseTitle(i); }
            GUI.Label(new Rect(0, 660, w, 40), "Navigate: WASD / arrows / stick    Confirm: Enter / A    Back: Esc / B", _small);
        }

        private void DrawCharacters(float w)
        {
            var content = GameServices.Content;
            var profile = GameServices.Profile;
            var chars = content.CharacterOrder;
            if (chars.Count == 0) { GUI.Label(new Rect(0, 300, w, 40), "No characters defined", _item); return; }
            var c = content.Characters[chars[_characterIndex]];
            bool available = RunSetupFactory.IsCharacterAvailable(c, profile);
            string modeName = _pendingMode == GameMode.Practice ? "Practice" : _pendingMode == GameMode.Custom ? "Custom seed" : "New run";
            GUI.Label(new Rect(0, 40, w, 50), "<b>" + modeName + "</b>", _title);
            GUI.Label(new Rect(0, 130, w, 40), "<  " + (available ? "<b>" + c.Name + "</b>" : "???") + "  >", _item);
            GUI.Label(new Rect(w * 0.5f - 320f, 175, 640, 120), available ? "<i>" + c.Title + "</i>\n" + c.Description + "\n\n" + c.Trait : "Locked. " + (c.UnlockHint ?? ""), _small);
            if (available && profile.CompletionMarks.TryGetValue(c.Id, out var marks) && marks.Count > 0)
                GUI.Label(new Rect(w * 0.5f - 320f, 300, 640, 30), "Marks: " + string.Join(", ", marks.OrderBy(m => m)), _small);
            for (int i = 0; i < Difficulties.Length; i++)
                if (Entry(new Rect(w * 0.5f - 150f, 360f + i * 40f, 300f, 36f), Cap(Difficulties[i]), i == _difficultyIndex)) _difficultyIndex = i;
            if (_pendingMode == GameMode.Custom)
            {
                GUI.Label(new Rect(w * 0.5f - 150f, 530, 300, 24), "Seed (8 characters, blank = random)", _small);
                _seedText = GUI.TextField(new Rect(w * 0.5f - 100f, 556, 200, 30), _seedText, 8);
            }
            if (Entry(new Rect(w * 0.5f - 150f, 600f, 300f, 40f), "Start", false, available)) Handle(MenuCommand.Submit);
            if (GUI.Button(new Rect(w * 0.5f - 330f, 125f, 60f, 50f), "<", _item)) Handle(MenuCommand.Left);
            if (GUI.Button(new Rect(w * 0.5f + 270f, 125f, 60f, 50f), ">", _item)) Handle(MenuCommand.Right);
        }

        private void DrawChallenges(float w)
        {
            var profile = GameServices.Profile;
            var list = GameServices.Content.Challenges.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            GUI.Label(new Rect(0, 40, w, 50), "<b>Challenges</b>", _title);
            for (int i = 0; i < list.Count; i++)
            {
                var ch = list[i];
                bool unlocked = profile.IsUnlocked(ch.UnlockId);
                string done = profile.ChallengesCompleted.Contains(ch.Id) ? "  <color=#7fdc7f>(done)</color>" : "";
                if (Entry(new Rect(w * 0.5f - 300f, 120f + i * 40f, 600f, 36f), (unlocked ? ch.Name : "???") + done, i == _index, unlocked)) { _index = i; Handle(MenuCommand.Submit); }
            }
            if (list.Count > 0)
            {
                var sel = list[Mathf.Clamp(_index, 0, list.Count - 1)];
                GUI.Label(new Rect(w * 0.5f - 320f, 600, 640, 80), profile.IsUnlocked(sel.UnlockId) ? sel.Description : "Locked.", _small);
            }
        }

        private void DrawStats(float w)
        {
            var p = GameServices.Profile;
            var st = p.Stats;
            var content = GameServices.Content;
            GUI.Label(new Rect(0, 30, w, 50), "<b>Statistics</b>", _title);
            GUI.Label(new Rect(w * 0.5f - 320f, 90, 640, 90),
                $"Runs {st.TotalRuns}   Wins {st.Wins}   Deaths {st.Deaths}   Best streak {st.BestStreak}\n" +
                $"Enemies {st.EnemiesDefeated}   Bosses {st.BossesDefeated}   Items {st.ItemsCollected}   Secrets {st.SecretsFound}\n" +
                $"Items discovered {p.DiscoveredItems.Count}/{content.Items.Count}   Synergies {p.DiscoveredSynergies.Count}/{content.Synergies.Count}   Time {TimeSpan.FromSeconds(st.SecondsPlayed):hh\\:mm}", _small);
            GUI.Label(new Rect(0, 185, w, 30), $"Achievements {p.Achievements.Count}/{content.Achievements.Count}", _item);
            var view = new Rect(w * 0.5f - 330f, 225, 660, 440);
            var all = content.Achievements.Values.OrderBy(a => p.Achievements.ContainsKey(a.Id) ? 0 : 1).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, 640, all.Count * 26f));
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                bool got = p.Achievements.ContainsKey(a.Id);
                string text = got ? "<color=#ffd36b>" + a.Name + "</color>  " + a.Description : a.Hidden ? "<color=#777777>Hidden achievement</color>" : "<color=#999999>" + a.Name + "  " + a.Description + "</color>";
                GUI.Label(new Rect(0, i * 26f, 640, 24), text, _small);
            }
            GUI.EndScrollView();
        }

        private void DrawSummary(float w)
        {
            var run = Director.Run;
            if (run == null) return;
            bool won = run.State == RunState.Won;
            GUI.Label(new Rect(0, 60, w, 60), won ? "<b>Victory</b>" : "<b>You fell</b>", _title);
            var s = run.Stats;
            string mode = run.Setup.Mode == GameMode.Challenge ? "Challenge: " + run.Setup.ChallengeId : run.Setup.Mode.ToString();
            GUI.Label(new Rect(w * 0.5f - 320f, 140, 640, 130),
                $"{mode}   Character: {run.Setup.CharacterId}   Seed {run.Setup.Seed.ToCode()}\n" +
                $"Floor {run.FloorIndex + 1}   Rooms {s.RoomsCleared}   Kills {s.Kills}   Bosses {s.BossKills}   Items {s.ItemsCollected}\n" +
                $"Time {TimeSpan.FromSeconds(run.RunTime):mm\\:ss}   Score {LastResult?.Score ?? 0}" +
                (LastResult != null && !LastResult.Recorded ? "\n<color=#999999>Practice run: no progress recorded</color>" : ""), _small);
            float y = 290f;
            if (LastResult?.Ending != null)
            {
                var ending = LastResult.Ending;
                GUI.Label(new Rect(0, y, w, 30), "<b>" + ending.Name + "</b>" + (LastResult.EndingIsNew ? "  <color=#ffd36b>(new ending)</color>" : ""), _small);
                y += 32f;
                foreach (var line in ending.Lines) { GUI.Label(new Rect(w * 0.5f - 360f, y, 720f, 24), "<i>" + line + "</i>", _small); y += 24f; }
                y += 12f;
            }
            if (LastResult != null)
            {
                foreach (var a in LastResult.NewAchievements.Take(8)) { GUI.Label(new Rect(0, y, w, 26), "<color=#ffd36b>Achievement: " + a.Name + "</color>", _small); y += 26f; }
                foreach (var u in LastResult.NewUnlocks.Take(6)) { GUI.Label(new Rect(0, y, w, 26), "<color=#7fdc7f>Unlocked: " + DescribeUnlock(u) + "</color>", _small); y += 26f; }
            }
            if (Entry(new Rect(w * 0.5f - 150f, 640f, 300f, 40f), "Continue", true)) Handle(MenuCommand.Submit);
        }

        private static string DescribeUnlock(string id)
        {
            var c = GameServices.Content.Characters.Values.FirstOrDefault(x => x.UnlockId == id);
            if (c != null) return c.Name;
            var ch = GameServices.Content.Challenges.Values.FirstOrDefault(x => x.UnlockId == id);
            if (ch != null) return "Challenge " + ch.Name;
            var it = GameServices.Content.Items.Values.FirstOrDefault(x => x.UnlockId == id);
            if (it != null) return it.Name;
            var en = GameServices.Content.Endings.Values.FirstOrDefault(x => x.Unlocks.Contains(id));
            return en != null ? "Ending " + en.Name : id;
        }

        private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
