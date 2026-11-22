using Hollowcrawl.Economy;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Placeholder HUD drawn with IMGUI: health, resources, active charge, floor and seed, boss bar,
    /// minimap, item pop-ups, pause and run-end panels. The final uGUI HUD (phase 10) reads the same data.
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        public RunDirector Director;
        private GUIStyle _label, _big, _small;
        private string _popup;
        private float _popupTime;

        public void OnSimEvent(SimEvent e)
        {
            var run = Director != null ? Director.Run : null;
            if (run == null) return;
            if (e.Kind == SimEventKind.ItemCollected && e.Key != null && run.Content.Items.TryGetValue(e.Key, out var item))
                ShowPopup(item.Name + (string.IsNullOrEmpty(item.Description) ? "" : "\n<size=14>" + item.Description + "</size>"));
            else if (e.Kind == SimEventKind.FloorStarted && run.Floor != null) ShowPopup(run.Floor.Name);
            else if (e.Kind == SimEventKind.Feedback && e.Key == "door_needs_key") ShowPopup("<size=16>Locked: needs a key</size>");
            else if (e.Kind == SimEventKind.ConsumableUsed && run.Content.Consumables.TryGetValue(e.Key, out var c)) ShowPopup(c.Name + "\n<size=14>" + c.Description + "</size>");
            else if (e.Kind == SimEventKind.ShrineUsed && e.Key != null) ShowPopup("<size=18>" + e.Key + "</size>");
            else if (e.Kind == SimEventKind.HexApplied && run.Content.Hexes.TryGetValue(e.Key, out var hx)) ShowPopup("<color=#c080ff>" + hx.Name + "</color>\n<size=14>" + hx.Description + "</size>");
            else if (e.Kind == SimEventKind.PurchaseFailed) ShowPopup("<size=16>You can't afford that</size>");
        }

        private void ShowPopup(string text) { _popup = text; _popupTime = 3f; }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _big = new GUIStyle(_label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _small = new GUIStyle(_label) { fontSize = 13 };
        }

        private void OnGUI()
        {
            var run = Director != null ? Director.Run : null;
            if (run == null) return;
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;

            DrawHealth(run, 16f, 12f);
            var r = run.Engine.Resources;
            GUI.Label(new Rect(16f, 44f, 300f, 24f), $"Coins {r.Get(ResourceKind.Coins):00}   Bombs {r.Get(ResourceKind.Bombs):00}   Keys {r.Get(ResourceKind.Keys):00}", _label);
            DrawActive(run, 16f, 72f);
            if (run.ConsumableSlots.Count > 0)
                GUI.Label(new Rect(16f, 108f, 360f, 22f), "Held: " + run.ConsumableDisplayName(run.ConsumableSlots[0]) + (run.ConsumableSlots.Count > 1 ? $"  (+{run.ConsumableSlots.Count - 1})" : ""), _small);
            if (run.ActiveHex != null) GUI.Label(new Rect(w - 316f, 52f, 300f, 20f), "<color=#c080ff>" + run.ActiveHex.Name + "</color>", _small);
            GUI.Label(new Rect(w - 316f, 12f, 300f, 24f), $"<b>{run.Floor?.Name}</b>  {run.FloorIndex + 1}/{run.TotalFloors}", _label);
            GUI.Label(new Rect(w - 316f, 36f, 300f, 20f), "Seed " + Director.SeedCode, _small);
            DrawMinimap(run, w - 170f, 76f);
            DrawBossBar(run, w);

            if (_popupTime > 0f)
            {
                _popupTime -= Time.unscaledDeltaTime;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_popupTime));
                GUI.Label(new Rect(w * 0.5f - 300f, 120f, 600f, 90f), _popup, _big);
                GUI.color = Color.white;
            }

            if (Director.Paused) Panel(w, "Paused", "Press Pause to resume");
            else if (run.State == RunState.Won) Panel(w, "Victory", $"Floors {run.FloorIndex + 1}  Rooms cleared {run.RoomsCleared}  Kills {run.EnemiesKilled}\nPress Interact to start a new run");
            else if (run.State == RunState.Lost) Panel(w, "You fell", $"Floor {run.FloorIndex + 1}  Rooms cleared {run.RoomsCleared}  Kills {run.EnemiesKilled}  Time {run.RunTime:0}s\nPress Interact to start a new run");
        }

        private void DrawHealth(RunSimulation run, float x, float y)
        {
            var h = run.Engine.Health;
            for (int i = 0; i < h.Containers; i++)
            {
                int filled = Mathf.Clamp(h.Vigor - i * 2, 0, 2);
                Box(new Rect(x + i * 22f, y, 18f, 18f), new Color(0.25f, 0.05f, 0.07f));
                if (filled > 0) Box(new Rect(x + i * 22f, y, filled == 2 ? 18f : 9f, 18f), new Color(0.85f, 0.12f, 0.18f));
            }
            float ox = x + h.Containers * 22f + 6f;
            for (int i = 0; i < (h.Ward + 1) / 2; i++) Box(new Rect(ox + i * 22f, y, (i * 2 + 1 < h.Ward) ? 18f : 9f, 18f), new Color(0.45f, 0.65f, 1f));
            ox += (h.Ward + 1) / 2 * 22f;
            for (int i = 0; i < (h.Umbral + 1) / 2; i++) Box(new Rect(ox + i * 22f, y, (i * 2 + 1 < h.Umbral) ? 18f : 9f, 18f), new Color(0.35f, 0.2f, 0.5f));
        }

        private void DrawActive(RunSimulation run, float x, float y)
        {
            var e = run.Engine;
            if (e.ActiveItem == null) return;
            GUI.Label(new Rect(x, y, 260f, 22f), e.ActiveItem.Definition.Name, _small);
            float frac = e.ActiveMaxCharge > 0 ? Mathf.Clamp01(e.ActiveCharge / (float)e.ActiveMaxCharge) : 1f;
            Box(new Rect(x, y + 20f, 120f, 8f), new Color(0.15f, 0.15f, 0.15f));
            Box(new Rect(x, y + 20f, 120f * frac, 8f), e.ActiveReady ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 0.6f, 0.6f));
        }

        private void DrawBossBar(RunSimulation run, float w)
        {
            var room = run.CurrentRoom;
            if (room == null) return;
            foreach (var e in room.Enemies)
            {
                if (!e.IsBoss) continue;
                float bw = 420f, x = w * 0.5f - bw * 0.5f, y = 670f;
                GUI.Label(new Rect(x, y - 24f, bw, 22f), "<b>" + e.Boss.Name + "</b>" + (e.Boss.Title != null ? "  <size=13>" + e.Boss.Title + "</size>" : ""), _label);
                Box(new Rect(x, y, bw, 14f), new Color(0.15f, 0.05f, 0.05f));
                Box(new Rect(x, y, bw * Mathf.Clamp01(e.HealthFraction), 14f), e.BossController != null && e.BossController.IsInvulnerable ? Color.white : new Color(0.75f, 0.1f, 0.1f));
                return;
            }
        }

        private void DrawMinimap(RunSimulation run, float x, float y)
        {
            const float cell = 11f;
            var layout = run.Layout;
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in layout.CellToRoom.Keys) { if (c.X < minX) minX = c.X; if (c.Y < minY) minY = c.Y; }
            foreach (var kv in layout.CellToRoom)
            {
                var rt = run.FloorState.Rooms[kv.Value];
                bool current = kv.Value == run.FloorState.CurrentRoom;
                bool known = rt.Visited || (rt.Revealed && !run.MapHidden);
                if (!known && !current) continue;
                var col = current ? Color.white : rt.Visited ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.3f, 0.3f, 0.33f);
                Box(new Rect(x + (kv.Key.X - minX) * cell, y + (kv.Key.Y - minY) * cell, cell - 1f, cell - 1f), col);
                if (rt.Node.Type != RoomType.Combat && rt.Node.Type != RoomType.Start && (rt.Visited || rt.Revealed))
                    Box(new Rect(x + (kv.Key.X - minX) * cell + 3f, y + (kv.Key.Y - minY) * cell + 3f, cell - 7f, cell - 7f), RoomColor(rt.Node.Type));
            }
        }

        private static Color RoomColor(RoomType t)
        {
            switch (t)
            {
                case RoomType.Boss: return new Color(0.8f, 0.1f, 0.1f);
                case RoomType.Treasure: return new Color(1f, 0.85f, 0.2f);
                case RoomType.Shop: return new Color(0.3f, 0.8f, 0.3f);
                case RoomType.Secret:
                case RoomType.HiddenVault: return new Color(0.5f, 0.3f, 0.8f);
                case RoomType.Curse: return new Color(0.5f, 0.1f, 0.3f);
                default: return new Color(0.4f, 0.6f, 0.9f);
            }
        }

        private void Panel(float w, string title, string body)
        {
            Box(new Rect(0f, 0f, w, 720f), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(w * 0.5f - 300f, 250f, 600f, 60f), "<b>" + title + "</b>", _big);
            GUI.Label(new Rect(w * 0.5f - 300f, 320f, 600f, 80f), "<size=18>" + body + "</size>", _big);
        }

        private static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
