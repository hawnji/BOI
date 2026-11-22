using System;
using UnityEngine.InputSystem;

namespace Hollowcrawl.Input
{
    /// <summary>
    /// Interactive rebinding on top of the Input System. Rebinds the first non-composite binding of
    /// an action for the chosen device group (buttons). Composite (movement) parts are rebound
    /// through <see cref="StartRebindPart"/>.
    /// </summary>
    public sealed class UnityInputRebinder : IInputRebinder
    {
        private readonly InputActionAsset _asset;
        private InputActionRebindingExtensions.RebindingOperation _operation;

        public event Action OverridesChanged;

        public UnityInputRebinder(InputActionAsset asset) { _asset = asset; }

        public string ExportOverrides() => _asset.SaveBindingOverridesAsJson();

        public void ImportOverrides(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try { _asset.LoadBindingOverridesFromJson(json); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("Ignoring invalid binding overrides: " + ex.Message); _asset.RemoveAllBindingOverrides(); }
        }

        public void ResetAll()
        {
            _asset.RemoveAllBindingOverrides();
            OverridesChanged?.Invoke();
        }

        public string GetBindingDisplay(GameAction action, InputDeviceKind device)
        {
            var a = Find(action);
            int i = FindBindingIndex(a, device);
            return i < 0 ? "-" : a.GetBindingDisplayString(i);
        }

        public void StartRebind(GameAction action, InputDeviceKind device, Action<bool> onComplete)
        {
            var a = Find(action);
            int index = FindBindingIndex(a, device);
            if (index < 0) { onComplete?.Invoke(false); return; }
            Rebind(a, index, device, onComplete);
        }

        /// <summary>Rebinds one part ("Up", "Down", "Left", "Right") of a composite such as Move.</summary>
        public void StartRebindPart(GameAction action, string partName, InputDeviceKind device, Action<bool> onComplete)
        {
            var a = Find(action);
            string group = Group(device);
            for (int i = 0; i < a.bindings.Count; i++)
            {
                var b = a.bindings[i];
                if (b.isPartOfComposite && string.Equals(b.name, partName, StringComparison.OrdinalIgnoreCase) && b.groups != null && b.groups.Contains(group))
                {
                    Rebind(a, i, device, onComplete);
                    return;
                }
            }
            onComplete?.Invoke(false);
        }

        public void CancelRebind() => _operation?.Cancel();

        private void Rebind(InputAction action, int index, InputDeviceKind device, Action<bool> onComplete)
        {
            CancelRebind();
            bool wasEnabled = action.enabled;
            action.Disable();
            var op = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta");
            if (action.bindings[index].isPartOfComposite) op = op.WithExpectedControlType("Button");
            op = device == InputDeviceKind.Gamepad ? op.WithControlsHavingToMatchPath("<Gamepad>") : op.WithControlsExcluding("<Gamepad>");
            _operation = op
                .OnComplete(o => Finish(o, action, wasEnabled, true, onComplete))
                .OnCancel(o => Finish(o, action, wasEnabled, false, onComplete))
                .Start();
        }

        private void Finish(InputActionRebindingExtensions.RebindingOperation op, InputAction action, bool reenable, bool success, Action<bool> cb)
        {
            op.Dispose();
            if (_operation == op) _operation = null;
            if (reenable) action.Enable();
            if (success) OverridesChanged?.Invoke();
            cb?.Invoke(success);
        }

        private InputAction Find(GameAction action)
        {
            string map = action >= GameAction.Navigate ? GameInputActions.UiMap : GameInputActions.GameplayMap;
            string name = action == GameAction.Aim ? "AimStick" : action.ToString();
            return _asset.FindActionMap(map, true).FindAction(name, true);
        }

        private static string Group(InputDeviceKind d) => d == InputDeviceKind.Gamepad ? GameInputActions.Gamepad : GameInputActions.KeyboardMouse;

        private static int FindBindingIndex(InputAction action, InputDeviceKind device)
        {
            string group = Group(device);
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (b.groups != null && b.groups.Contains(group)) return i;
            }
            return -1;
        }
    }
}
