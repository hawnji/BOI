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

        /// <summary>Per-view animation state: idle and move strips, playback time, facing.</summary>
        private sealed class Anim
        {
            public string Key;
            public Sprite[] Idle, Move;
            public float Time;
            public Vector2 LastPos;
            public bool FaceLeft, HasArt;
        }

        private readonly Dictionary<int, Anim> _anims = new Dictionary<int, Anim>();
        private const float IdleFps = 4f, MoveFps = 9f;

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
            foreach (var id in _stale) { var v = _live[id]; _live.Remove(id); _anims.Remove(id); v.gameObject.SetActive(false); _pool.Push(v); }
        }

        public void Clear()
        {
            foreach (var v in _live.Values) { v.gameObject.SetActive(false); _pool.Push(v); }
            _live.Clear();
            _anims.Clear();
        }

        private SpriteRenderer Show(Actor a, int order)
        {
            _seen.Add(a.Id);
            if (!_live.TryGetValue(a.Id, out var v))
            {
                v = _pool.Count > 0 ? _pool.Pop() : CreateView();
                v.gameObject.SetActive(true);
                v.gameObject.name = a.Kind + " " + a.ViewKey;
                _live[a.Id] = v;
            }
            var pos = new Vector2(a.Position.X, a.Position.Y);
            var anim = Animate(a.Id, a.ViewKey, a.Kind, pos);
            v.sprite = anim.HasArt ? Frame(anim, pos) : PlaceholderArt.Get(a.ViewKey, a.Kind);
            v.flipX = anim.HasArt && anim.FaceLeft && Faces(a.Kind);
            v.enabled = true;
            v.sortingOrder = order;
            v.color = anim.HasArt ? Color.white : PlaceholderArt.Tint(a.ViewKey ?? a.Kind.ToString(), a.Kind);
            // Pixel art is drawn at its native size (32 px per unit) and snapped to the pixel grid so it stays crisp;
            // placeholder shapes are scaled to the actor's collision size instead.
            v.transform.localPosition = anim.HasArt ? new Vector3(Snap(pos.x), Snap(pos.y), 0f) : new Vector3(pos.x, pos.y, 0f);
            float d = anim.HasArt ? 1f : a.Radius * 2f;
            v.transform.localScale = new Vector3(d, d, 1f);
            return v;
        }

        private static bool Faces(ActorKind k) => k == ActorKind.Player || k == ActorKind.Enemy || k == ActorKind.Companion;

        private static float Snap(float v) => Mathf.Round(v * PlaceholderArt.ArtPixelsPerUnit) / PlaceholderArt.ArtPixelsPerUnit;

        private Anim Animate(int id, string key, ActorKind kind, Vector2 pos)
        {
            if (!_anims.TryGetValue(id, out var anim) || anim.Key != key)
            {
                anim = new Anim { Key = key, LastPos = pos };
                anim.Idle = PlaceholderArt.LoadFrames(key);
                anim.Move = PlaceholderArt.LoadFrames(key + "_move");
                anim.HasArt = anim.Idle != null;
                anim.Time = (id * 0.37f) % 1f; // desync identical enemies
                _anims[id] = anim;
            }
            return anim;
        }

        private Sprite Frame(Anim anim, Vector2 pos)
        {
            float dt = Time.deltaTime;
            var delta = pos - anim.LastPos;
            anim.LastPos = pos;
            bool moving = dt > 0f && delta.sqrMagnitude > 1e-6f;
            if (Mathf.Abs(delta.x) > 0.002f) anim.FaceLeft = delta.x < 0f;
            var frames = moving && anim.Move != null ? anim.Move : anim.Idle;
            anim.Time += dt;
            int i = (int)(anim.Time * (moving && anim.Move != null ? MoveFps : IdleFps)) % frames.Length;
            return frames[i];
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
                v.gameObject.name = "Icon " + iconKey;
                _live[key] = v;
            }
            var art = PlaceholderArt.LoadFrames(iconKey);
            v.sprite = art != null ? art[(int)(_blink * IdleFps) % art.Length] : PlaceholderArt.Get(iconKey, ActorKind.Pickup);
            v.flipX = false;
            v.enabled = true;
            v.sortingOrder = 4;
            v.color = art != null ? Color.white : PlaceholderArt.Tint(iconKey, ActorKind.Pickup);
            float bob = Mathf.Round(2f * Mathf.Sin(_blink * 3f + p.Id)) / PlaceholderArt.ArtPixelsPerUnit;
            v.transform.localPosition = new Vector3(Snap(p.Position.X), Snap(p.Position.Y + 0.55f) + bob, 0f);
            float sc = art != null ? 1f : 0.7f;
            v.transform.localScale = new Vector3(sc, sc, 1f);
        }

        private SpriteRenderer CreateView()
        {
            var go = new GameObject("Actor");
            go.transform.SetParent(_root, false);
            return go.AddComponent<SpriteRenderer>();
        }
    }
}
