using System;
using Hollowcrawl.Bootstrap;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;
using Hollowcrawl.Simulation;
using UnityEngine;

namespace Hollowcrawl.Presentation
{
    /// <summary>
    /// Runs a <see cref="RunSimulation"/> on a fixed 60 Hz step and presents it: room texture, pooled actor
    /// sprites, camera, feedback cues and the HUD. Contains no gameplay rules (ADR-009).
    /// If a scene has no RunDirector, one is created after bootstrap so pressing Play starts a run.
    /// </summary>
    public sealed class RunDirector : MonoBehaviour
    {
        public RunSimulation Run { get; private set; }
        public bool Paused { get; private set; }
        public string SeedCode { get; private set; }
        public const int MaxStepsPerFrame = 5;

        private Camera _camera;
        private Transform _world;
        private Transform _aimOrigin;
        private RoomRenderer _roomRenderer;
        private ActorViews _views;
        private HudOverlay _hud;
        private float _accumulator;
        private PlayerInputFrame _latched;
        private float _shake;
        private readonly System.Random _shakeRng = new System.Random();
        private bool _roomDirty;
        private SpriteRenderer _darkness;
        private readonly ShootingIntent _shooting = new ShootingIntent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (FindAnyObjectByType<RunDirector>() != null || FindAnyObjectByType<MainMenuMarker>() != null) return;
            new GameObject("[Run]").AddComponent<RunDirector>();
        }

        private void Start()
        {
            if (!GameServices.IsReady) { Debug.LogError("RunDirector: game services not ready; is GameBootstrap present?"); enabled = false; return; }
            _world = new GameObject("World").transform;
            _world.SetParent(transform, false);
            _roomRenderer = RoomRenderer.Create(_world);
            _views = new ActorViews(_world);
            _camera = Camera.main;
            if (_camera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = camGo.AddComponent<Camera>();
            }
            _camera.orthographic = true;
            _camera.orthographicSize = 6f;
            _camera.backgroundColor = new Color(0.04f, 0.035f, 0.04f);
            _aimOrigin = new GameObject("AimOrigin").transform;
            _aimOrigin.SetParent(_world, false);
            if (GameServices.PlayerInput is UnityPlayerInputSource src) { src.AimCamera = _camera; src.AimOrigin = _aimOrigin; }
            _hud = gameObject.AddComponent<HudOverlay>();
            _hud.Director = this;
            StartNewRun(RunSeed.FromEntropy());
        }

        public void StartNewRun(RunSeed seed)
        {
            var setup = new RunSetup { Seed = seed };
            SeedCode = seed.ToCode();
            Run = new RunSimulation(setup, GameServices.Content, GameServices.Rooms, GameServices.Effects, GameServices.Conditions);
            GameServices.CurrentRunEngine = Run.Engine;
            Run.RoomChanged += _ => _roomDirty = true;
            _roomDirty = true;
            _views.Clear();
            Paused = false;
            _accumulator = 0f;
        }

        private void Update()
        {
            if (Run == null) return;
            var frame = GameServices.PlayerInput != null ? GameServices.PlayerInput.ReadFrame() : default;
            if (GameServices.Settings != null)
            {
                frame.FireHeld = _shooting.Resolve(frame, GameServices.Settings, out var dir);
                frame.Aim = dir;
            }
            Latch(ref frame);

            if (frame.PausePressed && Run.State == RunState.Playing) Paused = !Paused;
            if (Run.State != RunState.Playing && (frame.InteractPressed || frame.PausePressed)) { StartNewRun(RunSeed.FromEntropy()); return; }

            if (!Paused && Run.State == RunState.Playing)
            {
                _accumulator = Mathf.Min(_accumulator + Time.deltaTime, RunSimulation.FixedStep * MaxStepsPerFrame);
                while (_accumulator >= RunSimulation.FixedStep)
                {
                    _accumulator -= RunSimulation.FixedStep;
                    Run.Step(_latched);
                    ClearPressed(ref _latched);
                    HandleEvents();
                    if (_roomDirty) RebuildRoom();
                }
            }
            if (_roomDirty) RebuildRoom();
            _views.Sync(Run.CurrentRoom, Run.Companions, Time.deltaTime);
            UpdateDarkness();
            if (Run.Player != null) _aimOrigin.localPosition = new Vector3(Run.Player.Position.X, Run.Player.Position.Y, 0f);
            UpdateCamera();
        }

        /// <summary>Held inputs track the latest frame; one-frame presses stick until a simulation step consumes them.</summary>
        private void Latch(ref PlayerInputFrame f)
        {
            bool fire = _latched.FirePressed, active = _latched.ActiveItemPressed, bomb = _latched.BombPressed, cons = _latched.ConsumablePressed,
                swap = _latched.SwapConsumablePressed, interact = _latched.InteractPressed;
            _latched = f;
            _latched.FirePressed |= fire; _latched.ActiveItemPressed |= active; _latched.BombPressed |= bomb;
            _latched.ConsumablePressed |= cons; _latched.SwapConsumablePressed |= swap; _latched.InteractPressed |= interact;
        }

        private static void ClearPressed(ref PlayerInputFrame f)
        {
            f.FirePressed = false; f.ActiveItemPressed = false; f.BombPressed = false; f.ConsumablePressed = false;
            f.SwapConsumablePressed = false; f.InteractPressed = false; f.PausePressed = false;
        }

        private void HandleEvents()
        {
            foreach (var e in Run.Events.Events)
            {
                switch (e.Kind)
                {
                    case SimEventKind.PlayerHurt: _shake = Mathf.Max(_shake, 0.25f); break;
                    case SimEventKind.Explosion: _shake = Mathf.Max(_shake, 0.15f + 0.05f * e.Value); break;
                    case SimEventKind.BossPhaseStarted: _shake = Mathf.Max(_shake, 0.35f); break;
                    case SimEventKind.TileChanged:
                    case SimEventKind.DoorChanged:
                    case SimEventKind.SecretRevealed: _roomDirty = true; break;
                }
                _hud.OnSimEvent(e);
                AudioCues.Play(e);
            }
            Run.Events.Clear();
        }

        private void RebuildRoom()
        {
            _roomDirty = false;
            if (Run.CurrentRoom != null) _roomRenderer.Show(Run.CurrentRoom.Grid);
        }

        /// <summary>Gloom-style hexes: a dark overlay with a clear circle around the player.</summary>
        private void UpdateDarkness()
        {
            float radius = Run.ActiveHex != null ? Run.ActiveHex.VisionRadius : 0f;
            if (radius <= 0f || Run.Player == null) { if (_darkness != null) _darkness.enabled = false; return; }
            if (_darkness == null)
            {
                var go = new GameObject("Darkness");
                go.transform.SetParent(_world, false);
                _darkness = go.AddComponent<SpriteRenderer>();
                _darkness.sortingOrder = 50;
                _darkness.sprite = MakeVignette();
            }
            _darkness.enabled = true;
            // the vignette's clear area is 1/6 of its size
            float size = radius * 6f;
            _darkness.transform.localScale = new Vector3(size, size, 1f);
            _darkness.transform.localPosition = new Vector3(Run.Player.Position.X, Run.Player.Position.Y, 0f);
        }

        private static Sprite MakeVignette()
        {
            const int n = 192;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f, clear = n / 6f, fade = n / 10f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((d - clear) / fade);
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 245f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private void UpdateCamera()
        {
            var room = Run.CurrentRoom;
            if (room == null || _camera == null) return;
            float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
            var p = room.Player != null ? room.Player.Position : room.Grid.Center;
            float x = ClampAxis(p.X, room.Grid.Width, halfW);
            float y = ClampAxis(p.Y, room.Grid.Height, halfH);
            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime);
                x += ((float)_shakeRng.NextDouble() - 0.5f) * _shake;
                y += ((float)_shakeRng.NextDouble() - 0.5f) * _shake;
            }
            _camera.transform.position = new Vector3(x, y, -10f);
        }

        private static float ClampAxis(float v, float size, float half)
        {
            float min = half - 1f, max = size + 1f - half;
            return min > max ? size * 0.5f : Mathf.Clamp(v, min, max);
        }
    }

    /// <summary>Put on an object in a menu scene to stop a run auto-starting there.</summary>
    public sealed class MainMenuMarker : MonoBehaviour { }

    /// <summary>Plays Resources/Audio/&lt;key&gt; clips for simulation events when they exist (silent placeholders otherwise).</summary>
    public static class AudioCues
    {
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> Clips = new System.Collections.Generic.Dictionary<string, AudioClip>();

        public static void Play(SimEvent e)
        {
            string key = e.AudioKey ?? DefaultKey(e.Kind);
            if (key == null) return;
            if (!Clips.TryGetValue(key, out var clip)) Clips[key] = clip = Resources.Load<AudioClip>("Audio/" + key);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, new Vector3(e.Position.X, e.Position.Y, -5f), GameServices.Settings != null ? GameServices.Settings.SfxVolume : 1f);
        }

        private static string DefaultKey(SimEventKind k)
        {
            switch (k)
            {
                case SimEventKind.ShotFired: return "sfx_shot";
                case SimEventKind.EnemyHit: return "sfx_hit";
                case SimEventKind.EnemyKilled: return "sfx_enemy_death";
                case SimEventKind.PlayerHurt: return "sfx_player_hurt";
                case SimEventKind.Explosion: return "sfx_explosion";
                case SimEventKind.PickupCollected: return "sfx_pickup";
                case SimEventKind.ItemCollected: return "sfx_item";
                case SimEventKind.DoorChanged: return "sfx_door";
                case SimEventKind.SecretRevealed: return "sfx_secret";
                case SimEventKind.RoomCleared: return "sfx_room_clear";
                default: return null;
            }
        }
    }
}
