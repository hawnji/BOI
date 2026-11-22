using System;
using Hollowcrawl.Items;
using Hollowcrawl.Save;

namespace Hollowcrawl.Input
{
    public enum InputDeviceKind { KeyboardMouse = 0, Gamepad = 1 }

    /// <summary>Rebindable gameplay actions. Names match the Input System action names.</summary>
    public enum GameAction
    {
        Move, Aim, Fire, ActiveItem, Bomb, Consumable, SwapConsumable, DropTrinket, Interact, Map, Pause,
        // UI
        Navigate, Submit, Cancel, TabLeft, TabRight
    }

    /// <summary>One frame of player intent, already device-independent.</summary>
    public struct PlayerInputFrame
    {
        public Vec2 Move;               // magnitude 0..1
        /// <summary>Aim direction (normalized) or zero if not aiming this frame.</summary>
        public Vec2 Aim;
        public bool FireHeld;
        public bool FirePressed;
        public bool ActiveItemPressed;
        public bool BombPressed;
        public bool ConsumablePressed;
        public bool SwapConsumablePressed;
        public bool DropTrinketHeld;
        public bool InteractPressed;
        public bool MapHeld;
        public bool PausePressed;
        public InputDeviceKind Device;
    }

    /// <summary>Implemented by the Unity Input System adapter (and by scripted/replay sources in tests).</summary>
    public interface IPlayerInputSource
    {
        PlayerInputFrame ReadFrame();
        InputDeviceKind ActiveDevice { get; }
    }

    /// <summary>Binding persistence + interactive rebinding, implemented by the platform layer.</summary>
    public interface IInputRebinder
    {
        string ExportOverrides();
        void ImportOverrides(string json);
        void ResetAll();
        string GetBindingDisplay(GameAction action, InputDeviceKind device);
        void StartRebind(GameAction action, InputDeviceKind device, Action<bool> onComplete);
        void CancelRebind();
    }

    /// <summary>
    /// Device-independent shooting rules (hold, toggle auto-fire, aim-to-fire for sticks). Pure so
    /// it is unit-tested and identical for every platform.
    /// </summary>
    public sealed class ShootingIntent
    {
        private bool _autoFire;
        private Vec2 _lastAim = new Vec2(0f, -1f);

        public Vec2 LastAim => _lastAim;

        /// <summary>Returns true if the player should fire this frame, and the direction to fire in.</summary>
        public bool Resolve(in PlayerInputFrame frame, SettingsData settings, out Vec2 direction)
        {
            var mode = frame.Device == InputDeviceKind.Gamepad ? settings.GamepadShootMode : settings.MouseShootMode;
            bool aiming = frame.Aim.Length > Math.Max(0.05f, frame.Device == InputDeviceKind.Gamepad ? settings.StickDeadzone : 0.05f);
            if (aiming) _lastAim = frame.Aim.Normalized;
            direction = _lastAim;

            switch (mode)
            {
                case ShootMode.ToggleAutoFire:
                    if (frame.FirePressed) _autoFire = !_autoFire;
                    return _autoFire;
                case ShootMode.AimToFire:
                    return aiming || frame.FireHeld;
                default:
                    return frame.FireHeld;
            }
        }

        public void ResetToggle() => _autoFire = false;
    }
}
