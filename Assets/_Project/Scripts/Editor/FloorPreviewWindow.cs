using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Content;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Rng;
using UnityEditor;
using UnityEngine;

namespace Hollowcrawl.EditorTools
{
    /// <summary>
    /// Generates floors from any seed with the real generator, draws the room graph, shows the chosen
    /// template for each room, and batch-tests hundreds of seeds for validity and variety.
    /// </summary>
    public sealed class FloorPreviewWindow : EditorWindow
    {
        private ContentDatabase _db;
        private RoomTemplateLibrary _rooms;
        private string[] _floorIds = new string[0];
        private int _floorPick;
        private string _seedCode = "";
        private int _floorIndex;
        private FloorLayout _layout;
        private string _error;
        private int _selectedRoom = -1;
        private Vector2 _scroll, _templateScroll;
        private string _batchResult;

        [MenuItem("Hollowcrawl/Floor Previewer", priority = 10)]
        public static void Open() => GetWindow<FloorPreviewWindow>("Floor Previewer");

        private void OnEnable() { Reload(); if (string.IsNullOrEmpty(_seedCode)) _seedCode = RunSeed.FromEntropy().ToCode(); }

        private void Reload()
        {
            _db = EditorContent.Load(out _rooms, out var report);
            _floorIds = _db.Floors.Keys.OrderBy(k => k).ToArray();
            _error = report.IsValid ? null : report.Errors.Count + " room template errors (see Validate All Content).";
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload Content", EditorStyles.toolbarButton, GUILayout.Width(110))) Reload();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_rooms?.Count ?? 0} templates", EditorStyles.miniLabel);
            }
            if (_floorIds.Length == 0) { EditorGUILayout.HelpBox("No floors found in StreamingAssets/Content.", MessageType.Warning); return; }

            _floorPick = EditorGUILayout.Popup("Floor", Mathf.Clamp(_floorPick, 0, _floorIds.Length - 1), _floorIds);
            using (new EditorGUILayout.HorizontalScope())
            {
                _seedCode = EditorGUILayout.TextField("Seed", _seedCode);
                if (GUILayout.Button("Random", GUILayout.Width(70))) { _seedCode = RunSeed.FromEntropy().ToCode(); Generate(); }
            }
            _floorIndex = EditorGUILayout.IntSlider("Floor index", _floorIndex, 0, 14);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate")) Generate();
                if (GUILayout.Button("Batch test 500 seeds")) BatchTest(500);
            }
            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (_batchResult != null) EditorGUILayout.HelpBox(_batchResult, MessageType.Info);
            if (_layout == null) return;

            EditorGUILayout.LabelField($"Rooms: {_layout.Rooms.Count}   Boss: {_layout.BossId}   Attempts: {_layout.Attempts}   Notes: {string.Join("; ", _layout.Notes)}", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMap();
                DrawRoomDetails();
            }
        }

        private void Generate()
        {
            _error = null;
            _selectedRoom = -1;
            if (!RunSeed.TryParse(_seedCode, out var seed)) { _error = "Seed codes are 8 characters, e.g. K7QX-M2PA."; return; }
            try
            {
                var def = _db.Floors[_floorIds[_floorPick]];
                _layout = new FloorGenerator(_rooms).Generate(def, _floorIndex, seed, new GenerationHistory());
                var report = FloorValidator.Validate(_layout, _rooms);
                if (!report.IsValid) _error = report.ToString();
            }
            catch (System.Exception ex) { _layout = null; _error = ex.Message; }
        }

        private void BatchTest(int count)
        {
            var def = _db.Floors[_floorIds[_floorPick]];
            var gen = new FloorGenerator(_rooms);
            var rng = new SeededRandom((uint)System.Environment.TickCount);
            int failures = 0; long rooms = 0; int secrets = 0, vaults = 0;
            var distinct = new HashSet<string>();
            var templateUse = new Dictionary<string, int>();
            string firstFailure = null;
            for (int i = 0; i < count; i++)
            {
                var seed = new RunSeed(rng.NextUInt());
                try
                {
                    var f = gen.Generate(def, _floorIndex, seed, new GenerationHistory());
                    var r = FloorValidator.Validate(f, _rooms);
                    if (!r.IsValid) { failures++; firstFailure = firstFailure ?? seed + ": " + r.Errors[0]; }
                    rooms += f.Rooms.Count; secrets += f.Count(RoomType.Secret); vaults += f.Count(RoomType.HiddenVault);
                    distinct.Add(FloorAsciiRenderer.Render(f));
                    foreach (var room in f.Rooms) templateUse[room.TemplateId] = templateUse.TryGetValue(room.TemplateId, out var n) ? n + 1 : 1;
                }
                catch (System.Exception ex) { failures++; firstFailure = firstFailure ?? seed + ": " + ex.Message; }
            }
            var unused = _rooms.All.Where(t => !templateUse.ContainsKey(t.Id) && t.SuitsTheme(def.Theme)).Select(t => t.Id).ToList();
            _batchResult = $"{count} seeds: {failures} failures, avg {rooms / (float)count:0.0} rooms, {distinct.Count} distinct layouts, " +
                           $"secret rooms {secrets}, hidden vaults {vaults}.\nUnused templates for this theme: {(unused.Count == 0 ? "none" : string.Join(", ", unused))}" +
                           (firstFailure != null ? "\nFirst failure: " + firstFailure : "");
        }

        private static Color ColorFor(RoomType t)
        {
            switch (t)
            {
                case RoomType.Start: return new Color(0.35f, 0.75f, 0.4f);
                case RoomType.Boss: return new Color(0.85f, 0.25f, 0.25f);
                case RoomType.Treasure: return new Color(0.95f, 0.8f, 0.25f);
                case RoomType.Shop: return new Color(0.3f, 0.6f, 0.95f);
                case RoomType.Secret: case RoomType.HiddenVault: return new Color(0.55f, 0.4f, 0.75f);
                case RoomType.Combat: return new Color(0.45f, 0.45f, 0.45f);
                default: return new Color(0.9f, 0.55f, 0.2f);
            }
        }

        private void DrawMap()
        {
            const float cell = 30f, gap = 4f;
            var rect = GUILayoutUtility.GetRect(FloorGenerator.GridSize * cell, FloorGenerator.GridSize * cell, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            foreach (var room in _layout.Rooms)
            {
                var col = ColorFor(room.Type);
                if (room.Index == _selectedRoom) col = Color.Lerp(col, Color.white, 0.5f);
                foreach (var c in room.Cells)
                {
                    var r = new Rect(rect.x + c.X * cell + gap * 0.5f, rect.y + c.Y * cell + gap * 0.5f, cell - gap, cell - gap);
                    // merge visually with neighbour cells of the same room
                    if (room.Cells.Contains(new Cell(c.X + 1, c.Y))) r.width += gap;
                    if (room.Cells.Contains(new Cell(c.X, c.Y + 1))) r.height += gap;
                    EditorGUI.DrawRect(r, col);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { _selectedRoom = room.Index; Repaint(); }
                }
                var a = room.Cells[0];
                GUI.Label(new Rect(rect.x + a.X * cell + 8, rect.y + a.Y * cell + 6, cell, cell), FloorAsciiRenderer.Symbol(room.Type).ToString(), EditorStyles.whiteBoldLabel);
                foreach (var d in room.Doors)
                {
                    var wc = room.Anchor + d.Socket.LocalCell;
                    var center = new Vector2(rect.x + wc.X * cell + cell * 0.5f, rect.y + wc.Y * cell + cell * 0.5f);
                    Vector2 off = d.Socket.Side == Direction.North ? new Vector2(0, -cell * 0.5f) : d.Socket.Side == Direction.South ? new Vector2(0, cell * 0.5f)
                                : d.Socket.Side == Direction.West ? new Vector2(-cell * 0.5f, 0) : new Vector2(cell * 0.5f, 0);
                    var p = center + off;
                    var doorCol = d.Kind == DoorKind.Secret ? new Color(0.7f, 0.5f, 1f) : d.Kind == DoorKind.Locked ? Color.yellow : d.Kind == DoorKind.Boss ? Color.red : Color.white;
                    EditorGUI.DrawRect(new Rect(p.x - 3, p.y - 3, 6, 6), doorCol);
                }
            }
        }

        private void DrawRoomDetails()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
                foreach (var room in _layout.Rooms)
                {
                    string label = $"#{room.Index} {room.Type} {room.Shape} d={room.DistanceFromStart}{(room.OnMainPath ? " main" : "")}  {room.TemplateId}  enemies:{room.Enemies.Count}";
                    if (GUILayout.Toggle(_selectedRoom == room.Index, label, "Button")) _selectedRoom = room.Index;
                }
                EditorGUILayout.EndScrollView();

                if (_selectedRoom < 0 || _selectedRoom >= _layout.Rooms.Count) return;
                var sel = _layout.Rooms[_selectedRoom];
                var t = _rooms.Get(sel.TemplateId);
                if (t == null) return;
                var grid = t.Layout.Select(r => r.ToCharArray()).ToArray();
                foreach (var e in sel.Enemies) if (e.TileY < grid.Length && e.TileX < grid[e.TileY].Length) grid[e.TileY][e.TileX] = e.Elite ? '@' : 'e';
                _templateScroll = EditorGUILayout.BeginScrollView(_templateScroll);
                GUILayout.Label(string.Join("\n", grid.Select(r => new string(r))), EditorContent.Mono);
                GUILayout.Label("Doors: " + string.Join(", ", sel.Doors.Select(d => $"{d.Socket.Id}->{d.ToRoom} ({d.Kind})")), EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label("Enemies: " + string.Join(", ", sel.Enemies.Select(e => e.EnemyId + (e.Elite ? "*" : ""))), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
