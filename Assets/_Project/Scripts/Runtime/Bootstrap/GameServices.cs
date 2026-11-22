using Hollowcrawl.Content;
using Hollowcrawl.Events;
using Hollowcrawl.Input;
using Hollowcrawl.Items;
using Hollowcrawl.Platform;
using Hollowcrawl.ProcGen;
using Hollowcrawl.Save;

namespace Hollowcrawl.Bootstrap
{
    /// <summary>
    /// The long-lived services created once by <see cref="GameBootstrap"/>. Scenes and runtime-spawned
    /// objects read from here instead of using scattered singletons. Reset on every play-mode entry
    /// (works with domain reload disabled).
    /// </summary>
    public static class GameServices
    {
        public static bool IsReady { get; internal set; }
        public static ServiceRegistry Registry { get; internal set; }
        public static EventBus Events { get; internal set; }
        public static IPlatformServices Platform { get; internal set; }
        public static SaveService Saves { get; internal set; }
        public static SettingsData Settings { get; internal set; }
        public static ProfileData Profile { get; internal set; }
        public static int ProfileSlot { get; internal set; }
        public static ContentDatabase Content { get; internal set; }
        public static RoomTemplateLibrary Rooms { get; internal set; }
        public static EffectRegistry Effects { get; internal set; }
        public static ConditionRegistry Conditions { get; internal set; }
        public static IPlayerInputSource PlayerInput { get; internal set; }
        public static IInputRebinder Rebinder { get; internal set; }

        /// <summary>The interaction engine of the run in progress (null outside a run). Read by the synergy debugger.</summary>
        public static InteractionEngine CurrentRunEngine { get; set; }

        internal static void Reset()
        {
            IsReady = false;
            Registry = null; Events = null; Platform = null; Saves = null; Settings = null; Profile = null;
            Content = null; Rooms = null; Effects = null; Conditions = null; PlayerInput = null; Rebinder = null;
            CurrentRunEngine = null;
        }

        public static void SaveSettings()
        {
            if (Saves == null || Settings == null) return;
            if (Rebinder != null) Settings.BindingOverridesJson = Rebinder.ExportOverrides();
            Saves.SaveSettings(Settings);
        }

        public static void SaveProfile()
        {
            if (Saves != null && Profile != null) Saves.SaveProfile(ProfileSlot, Profile);
        }
    }
}
