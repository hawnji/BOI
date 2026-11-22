using System.Collections.Generic;
using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Mirrors simulation actors as pooled SpriteRenderers keyed by actor id. Views are created, moved and
    /// recycled every frame from the simulation's lists; nothing here affects gameplay.
    /// </summary>
    public sealed class ActorViews
    {
        private readonly Transform _root;
        private readonly Dictionary<int, SpriteRenderer> _live = new Dictionary<int, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private float _blink;

        public ActorViews(Transform root) { _root = root; }

        public void Sync(RoomSimulation room, IReadOnlyList<CompanionActor> companions, float dt)
        {
            _seen.Clear();
            _blink += dt;
            if (room != null)
            {
                if (room.Player != null)
                {
                    var health = room.Engine.Health;
                    bool invuln = health != null && health.InvulnerableTime > 0f;
                    var v = Show(room.Player, 10);
                    v.enabled = !invuln || ((int)(_blink * 16f) & 1) == 0;
                }
                foreach (var e in room.Enemies)
                {
                    var v = Show(e, 5);
                    if (e.FlashTimer > 0f) v.color = Color.white;
                    else if (e.LastIntent.State == Enemies.BrainState.Telegraphing) v.color = Color.Lerp(v.color, new Color(1f, 0.3f, 0.2f), 0.5f + 0.5f * Mathf.Sin(_blink * 30f));
                    v.enabled = !e.LastIntent.Invisible;
                    if (e.Elite) v.transform.localScale = v.transform.localScale * 1.15f;
                }
                foreach (var s in room.Shots) Show(s, s.Team == Combat.Team.Player ? 8 : 9);
                foreach (var p in room.Pickups)
                {
                    var v = Show(p, 2);
                    if (p.Veiled) v.color = new Color(0.4f, 0.4f, 0.45f);
                    else if (p.ContentId != null) ShowIcon(p);
                }
                foreach (var sh in room.Shrines) Show(sh, 1);
                foreach (var b in room.Bombs)
                {
                    var v = Show(b, 3);
                    float pulse = 1f + 0.12f * Mathf.Sin(_blink * (10f + 20f / Mathf.Max(0.2f, b.Fuse)));
                    v.transform.localScale = v.transform.localScale * pulse;
                }
                foreach (var h in room.Hazards) Show(h, 0);
                foreach (var x in room.Exits) Show(x, 1);
            }
            if (companions != null) foreach (var c in companions) Show(c, 7);

            _stale.Clear();
            foreach (var kv in _live) if (!_seen.Contains(kv.Key)) _stale.Add(kv.Key);
            foreach (var id in _stale) { var v = _live[id]; _live.Remove(id); v.gameObject.SetActive(false); _pool.Push(v); }
        }

        public void Clear()
        {
            foreach (var v in _live.Values) { v.gameObject.SetActive(false); _pool.Push(v); }
            _live.Clear();
        }

        private SpriteRenderer Show(Actor a, int order)
        {
            _seen.Add(a.Id);
            if (!_live.TryGetValue(a.Id, out var v))
            {
                v = _pool.Count > 0 ? _pool.Pop() : CreateView();
                v.gameObject.SetActive(true);
                v.sprite = PlaceholderArt.Get(a.ViewKey, a.Kind);
                v.gameObject.name = a.Kind + " " + a.ViewKey;
                _live[a.Id] = v;
            }
            v.enabled = true;
            v.sortingOrder = order;
            v.color = PlaceholderArt.Tint(a.ViewKey ?? a.Kind.ToString(), a.Kind);
            v.transform.localPosition = new Vector3(a.Position.X, a.Position.Y, 0f);
            float d = a.Radius * 2f;
            v.transform.localScale = new Vector3(d, d, 1f);
            return v;
        }

        /// <summary>Item or trinket icon floating over its pedestal (art hook: Resources/Sprites/item_&lt;id&gt; or the item's iconKey).</summary>
        private void ShowIcon(PickupActor p)
        {
            int key = -p.Id - 1; // icons share the view pool with negative ids
            string iconKey = "item_" + p.ContentId;
            var content = Bootstrap.GameServices.Content;
            if (content != null && content.Items.TryGetValue(p.ContentId, out var def) && !string.IsNullOrEmpty(def.IconKey)) iconKey = def.IconKey;
            _seen.Add(key);
            if (!_live.TryGetValue(key, out var v))
            {
                v = _pool.Count > 0 ? _pool.Pop() : CreateView();
                v.gameObject.SetActive(true);
                v.sprite = PlaceholderArt.Get(iconKey, ActorKind.Pickup);
                v.gameObject.name = "Icon " + iconKey;
                _live[key] = v;
            }
            v.enabled = true;
            v.sortingOrder = 4;
            v.color = PlaceholderArt.Tint(iconKey, ActorKind.Pickup);
            float bob = 0.08f * Mathf.Sin(_blink * 3f + p.Id);
            v.transform.localPosition = new Vector3(p.Position.X, p.Position.Y + 0.55f + bob, 0f);
            v.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        }

        private SpriteRenderer CreateView()
        {
            var go = new GameObject("Actor");
            go.transform.SetParent(_root, false);
            return go.AddComponent<SpriteRenderer>();
        }
    }
}
