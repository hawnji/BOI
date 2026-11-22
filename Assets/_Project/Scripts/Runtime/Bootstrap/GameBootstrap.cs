using Hollowcrawl.Content;
using Hollowcrawl.Events;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.Platform;
using Hollowcrawl.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowcrawl.Bootstrap
{
    /// <summary>
    /// Creates the persistent [Game] object before the first scene loads and wires every core service:
    /// platform, saves, settings, profile, content, input. Scenes never need to contain a bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private InputActionAsset _input;
        private UnityPlayerInputSource _playerInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => GameServices.Reset();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (GameServices.IsReady || FindAnyObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("[Game]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (GameServices.IsReady) { Destroy(gameObject); return; }

            var registry = new ServiceRegistry();
            var events = new EventBus();
            events.HandlerFailed += Debug.LogException;
            events.RecursionLimitHit += t => Debug.LogWarning("Event recursion limit hit for " + t.Name);

            IPlatformServices platform = new DesktopPlatformServices();
            var saves = new SaveService(platform.SaveStorage);

            var settingsResult = saves.LoadSettings();
            LogLoad("settings", settingsResult.Status, settingsResult.Messages);
            var settings = settingsResult.Data;

            int slot = Mathf.Clamp(settings.LastProfileSlot, 0, SaveService.ProfileSlots - 1);
            var profileResult = saves.LoadProfile(slot);
            LogLoad("profile " + slot, profileResult.Status, profileResult.Messages);

            var effects = EffectRegistry.CreateDefault();
            var conditions = new ConditionRegistry();
            var content = ContentDirectoryLoader.LoadAll(platform.ContentRoot);
            foreach (var e in content.LoadErrors) Debug.LogError("[Content] " + e);
            var rooms = content.BuildRoomLibrary(out var roomReport);
            foreach (var e in roomReport.Errors) Debug.LogError("[Rooms] " + e);
            if (Debug.isDebugBuild)
            {
                var report = ContentValidator.Validate(content, effects, conditions);
                foreach (var e in report.Errors) Debug.LogError("[Content] " + e);
                foreach (var w in report.Warnings) Debug.LogWarning("[Content] " + w);
            }

            _input = GameInputActions.Create();
            var rebinder = new UnityInputRebinder(_input);
            rebinder.ImportOverrides(settings.BindingOverridesJson);
            _playerInput = new UnityPlayerInputSource(_input);
            _playerInput.Enable();

            registry.Register(events);
            registry.Register(platform);
            registry.Register(saves);
            registry.Register(content);
            registry.Register(rooms);
            registry.Register(effects);
            registry.Register(conditions);
            registry.Register<IPlayerInputSource>(_playerInput);
            registry.Register<IInputRebinder>(rebinder);

            GameServices.Registry = registry;
            GameServices.Events = events;
            GameServices.Platform = platform;
            GameServices.Saves = saves;
            GameServices.Settings = settings;
            GameServices.Profile = profileResult.Data;
            GameServices.ProfileSlot = slot;
            GameServices.Content = content;
            GameServices.Rooms = rooms;
            GameServices.Effects = effects;
            GameServices.Conditions = conditions;
            GameServices.PlayerInput = _playerInput;
            GameServices.Rebinder = rebinder;
            GameServices.IsReady = true;

            Menus.SettingsMenu.Clamp(settings);
            SettingsApplier.Apply(settings);
            Debug.Log($"[Hollowcrawl] Ready on {platform.PlatformName}: {content.Items.Count} items, {rooms.Count} room templates, {content.Floors.Count} floors.");
        }

        private void OnApplicationQuit()
        {
            GameServices.SaveSettings();
            GameServices.SaveProfile();
        }

        private void OnDestroy()
        {
            if (_playerInput != null) _playerInput.Disable();
            if (_input != null) Destroy(_input);
        }

        private static void LogLoad(string what, LoadStatus status, System.Collections.Generic.List<string> messages)
        {
            foreach (var m in messages) Debug.LogWarning($"[Save] {what}: {m}");
            if (status == LoadStatus.RecoveredFromBackup) Debug.LogWarning($"[Save] {what} restored from backup.");
            if (status == LoadStatus.ResetAfterCorruption) Debug.LogError($"[Save] {what} was unreadable and has been reset (corrupt copy kept).");
        }
    }

    /// <summary>Applies device settings to Unity (audio, display, frame pacing).</summary>
    public static class SettingsApplier
    {
        public static void Apply(SettingsData s)
        {
            AudioListener.volume = Mathf.Clamp01(s.MasterVolume);
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.VSync || s.TargetFrameRate <= 0 ? -1 : s.TargetFrameRate;
            Application.runInBackground = !s.MuteWhenUnfocused;

            FullScreenMode mode;
            switch (s.WindowMode)
            {
                case WindowMode.Fullscreen: mode = FullScreenMode.ExclusiveFullScreen; break;
                case WindowMode.Windowed: mode = FullScreenMode.Windowed; break;
                default: mode = FullScreenMode.FullScreenWindow; break;
            }
            if (!Application.isEditor)
            {
                int w = s.ResolutionWidth > 0 ? s.ResolutionWidth : Screen.currentResolution.width;
                int h = s.ResolutionHeight > 0 ? s.ResolutionHeight : Screen.currentResolution.height;
                Screen.SetResolution(w, h, mode);
            }
        }
    }
}
