using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Bootstrap;
using Hollowcrawl.Combat;
using Hollowcrawl.Content;
using Hollowcrawl.Diagnostics;
using Hollowcrawl.Economy;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;
using Hollowcrawl.Stats;
using UnityEditor;
using UnityEngine;

namespace Hollowcrawl.EditorTools
{
    /// <summary>
    /// Item interaction debugger. In play mode with a run active it inspects the live engine; otherwise
    /// it runs a sandbox engine where items can be toggled to see stats, the projectile pipeline,
    /// synergies, transformations, suppression and the proc log.
    /// </summary>
    public sealed class SynergyDebuggerWindow : EditorWindow
    {
        private ContentDatabase _db;
        private InteractionEngine _sandbox;
        private NullGameWorld _world;
        private readonly List<string> _picked = new List<string>();
        private string _filter = "";
        private Vector2 _left, _right;

        [MenuItem("Hollowcrawl/Synergy Debugger", priority = 11)]
        public static void Open() => GetWindow<SynergyDebuggerWindow>("Synergy Debugger");

        private void OnEnable() { _db = EditorContent.Load(out _, out _); RebuildSandbox(); }
        private void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        private void RebuildSandbox()
        {
            _world = new NullGameWorld();
            _sandbox = new InteractionEngine(EffectRegistry.CreateDefault(), new StatSheet(), new HealthPool(3, 6), new PlayerResources(), _world, new SeededRandom(1));
            _sandbox.SetCombos(_db.Synergies.Values, _db.Transformations.Values);
            foreach (var id in _picked) if (_db.Items.TryGetValue(id, out var def)) _sandbox.AddItem(def);
        }

        private void OnGUI()
        {
            var live = Application.isPlaying ? GameServices.CurrentRunEngine : null;
            var engine = live ?? _sandbox;
            EditorGUILayout.HelpBox(live != null ? "Inspecting the live run." : "Sandbox mode: toggle items to test combinations (no scene needed).", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (live == null) DrawItemPicker();
                DrawEngine(engine);
            }
        }

        private void DrawItemPicker()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(240)))
            {
                _filter = EditorGUILayout.TextField("Filter", _filter);
                if (GUILayout.Button("Clear all")) { _picked.Clear(); RebuildSandbox(); }
                _left = EditorGUILayout.BeginScrollView(_left);
                foreach (var item in _db.Items.Values.OrderBy(i => i.Kind).ThenBy(i => i.Name))
                {
                    if (_filter.Length > 0 && item.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        !item.Tags.Any(t => t.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    bool on = _picked.Contains(item.Id);
                    bool now = GUILayout.Toggle(on, $"{item.Name}  [{item.Kind}] {string.Join(",", item.Tags)}", "Button");
                    if (now == on) continue;
                    if (now) _picked.Add(item.Id); else _picked.Remove(item.Id);
                    RebuildSandbox();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawEngine(InteractionEngine engine)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                _right = EditorGUILayout.BeginScrollView(_right);
                var snap = engine.GetSnapshot();
                EditorGUILayout.LabelField("Synergies", snap.Synergies.Count == 0 ? "-" : string.Join(", ", snap.Synergies), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Transformations", snap.Transformations.Count == 0 ? "-" : string.Join(", ", snap.Transformations), EditorStyles.boldLabel);

                GUILayout.Label("Stats", EditorStyles.boldLabel);
                for (int i = 0; i < (int)StatId.Count; i++)
                {
                    var id = (StatId)i;
                    float v = engine.Stats[id], b = engine.Stats.GetBase(id);
                    EditorGUILayout.LabelField(id.ToString(), Mathf.Approximately(v, b) ? v.ToString("0.###") : $"{v:0.###}  (base {b:0.###})");
                }

                var spec = new ProjectileSpec();
                engine.BuildShot(spec, new ShotContext { ChargeFraction = 1f });
                GUILayout.Label("Resulting shot (fully charged)", EditorStyles.boldLabel);
                GUILayout.Label(
                    $"damage {spec.Damage:0.##}  speed {spec.Speed:0.#}  range {spec.Range:0.#}  size {spec.Size:0.##}  delivery {spec.Delivery} (from {spec.DeliverySource ?? "-"})\n" +
                    $"volley {spec.ShotsPerVolley} spread {spec.SpreadDegrees}  pierce {spec.PierceCount}  bounce {spec.Bounces}  homing {spec.HomingStrength:0.##}  split {spec.SplitCount}\n" +
                    $"explode r{spec.ExplosionRadius:0.##}  chain {spec.ChainJumps}  spectral {spec.Spectral}  charge {spec.ChargeTime:0.##}s  types {spec.Types}\n" +
                    $"statuses: {string.Join(", ", spec.Statuses.Select(s => $"{s.StatusId} {s.Chance:P0}"))}\nvisuals: {string.Join(", ", spec.VisualKeys)}",
                    EditorContent.Mono);

                GUILayout.Label("Projectile pipeline (order)", EditorStyles.boldLabel);
                foreach (var p in snap.ProjectilePipeline) GUILayout.Label(p, EditorContent.Mono);
                GUILayout.Label("Effects", EditorStyles.boldLabel);
                foreach (var e in snap.Effects) GUILayout.Label(e.ToString(), EditorContent.Mono);
                GUILayout.Label($"Log (errors {engine.Log.ErrorCount}, guard trips {engine.Log.GuardCount})", EditorStyles.boldLabel);
                foreach (var l in snap.Log.AsEnumerable().Reverse().Take(40)) GUILayout.Label(l.ToString(), EditorContent.Mono);
                if (_world != null && engine == _sandbox && _world.Calls.Count > 0)
                    GUILayout.Label("World calls: " + string.Join(", ", _world.Calls.Select(kv => kv.Key + " x" + kv.Value)), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
