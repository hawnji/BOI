using Hollowcrawl.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowcrawl.Input
{
    /// <summary>
    /// Reads the Input System and produces device-independent <see cref="PlayerInputFrame"/>s.
    /// The active device switches to whichever device was used last, so mouse aim and stick aim
    /// never fight each other.
    /// </summary>
    public sealed class UnityPlayerInputSource : IPlayerInputSource
    {
        private readonly InputActionMap _map;
        private readonly InputAction _move, _aimPoint, _aimStick, _fire, _active, _bomb, _consumable, _swap, _drop, _interact, _mapView, _pause;
        private Vector2 _lastMouse;

        /// <summary>Camera and player transform used to turn the mouse position into an aim direction.</summary>
        public Camera AimCamera { get; set; }
        public Transform AimOrigin { get; set; }
        public InputDeviceKind ActiveDevice { get; private set; } = InputDeviceKind.KeyboardMouse;

        public UnityPlayerInputSource(InputActionAsset asset)
        {
            _map = asset.FindActionMap(GameInputActions.GameplayMap, true);
            _move = _map.FindAction("Move", true);
            _aimPoint = _map.FindAction("AimPoint", true);
            _aimStick = _map.FindAction("AimStick", true);
            _fire = _map.FindAction("Fire", true);
            _active = _map.FindAction("ActiveItem", true);
            _bomb = _map.FindAction("Bomb", true);
            _consumable = _map.FindAction("Consumable", true);
            _swap = _map.FindAction("SwapConsumable", true);
            _drop = _map.FindAction("DropTrinket", true);
            _interact = _map.FindAction("Interact", true);
            _mapView = _map.FindAction("Map", true);
            _pause = _map.FindAction("Pause", true);
        }

        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();

        public PlayerInputFrame ReadFrame()
        {
            UpdateActiveDevice();
            var frame = new PlayerInputFrame
            {
                Move = ClampMagnitude(_move.ReadValue<Vector2>()).ToCoreVec(),
                FireHeld = _fire.IsPressed(),
                FirePressed = _fire.WasPressedThisFrame(),
                ActiveItemPressed = _active.WasPressedThisFrame(),
                BombPressed = _bomb.WasPressedThisFrame(),
                ConsumablePressed = _consumable.WasPressedThisFrame(),
                SwapConsumablePressed = _swap.WasPressedThisFrame(),
                DropTrinketHeld = _drop.IsPressed(),
                InteractPressed = _interact.WasPressedThisFrame(),
                MapHeld = _mapView.IsPressed(),
                PausePressed = _pause.WasPressedThisFrame(),
                Device = ActiveDevice,
            };

            Vector2 stick = _aimStick.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.0025f)
            {
                frame.Aim = ClampMagnitude(stick).ToCoreVec();
                // Arrow keys behave like an aiming stick that also fires.
                if (ActiveDevice == InputDeviceKind.KeyboardMouse) frame.FireHeld = true;
            }
            else if (ActiveDevice == InputDeviceKind.KeyboardMouse && AimCamera != null && AimOrigin != null)
            {
                Vector2 screen = _aimPoint.ReadValue<Vector2>();
                Vector3 world = AimCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -AimCamera.transform.position.z));
                Vector2 dir = (Vector2)(world - AimOrigin.position);
                if (dir.sqrMagnitude > 1e-4f) frame.Aim = dir.normalized.ToCoreVec();
            }
            return frame;
        }

        private void UpdateActiveDevice()
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null && gp.wasUpdatedThisFrame &&
                (gp.leftStick.ReadValue().sqrMagnitude > 0.04f || gp.rightStick.ReadValue().sqrMagnitude > 0.04f ||
                 gp.buttonSouth.isPressed || gp.rightTrigger.isPressed || gp.startButton.isPressed || gp.dpad.ReadValue().sqrMagnitude > 0.1f))
            {
                ActiveDevice = InputDeviceKind.Gamepad;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if ((pos - _lastMouse).sqrMagnitude > 4f || mouse.leftButton.isPressed) ActiveDevice = InputDeviceKind.KeyboardMouse;
                _lastMouse = pos;
            }
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) ActiveDevice = InputDeviceKind.KeyboardMouse;
        }

        private static Vector2 ClampMagnitude(Vector2 v) => v.sqrMagnitude > 1f ? v.normalized : v;
    }

    internal static class InputVectorExtensions
    {
        public static Vec2 ToCoreVec(this Vector2 v) => new Vec2(v.x, v.y);
    }
}
