using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowcrawl.Input
{
    /// <summary>
    /// Builds the game's InputActionAsset in code so bindings live in one reviewable place and need
    /// no editor-generated asset. Control schemes: "KeyboardMouse" and "Gamepad". Players' rebinds
    /// are stored as override JSON in SettingsData and re-applied on start.
    /// </summary>
    public static class GameInputActions
    {
        public const string KeyboardMouse = "KeyboardMouse";
        public const string Gamepad = "Gamepad";
        public const string GameplayMap = "Gameplay";
        public const string UiMap = "UI";

        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "HollowcrawlInput";
            asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            asset.AddControlScheme(Gamepad).WithRequiredDevice("<Gamepad>");

            var g = asset.AddActionMap(GameplayMap);

            var move = g.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouse)
                .With("Down", "<Keyboard>/s", KeyboardMouse)
                .With("Left", "<Keyboard>/a", KeyboardMouse)
                .With("Right", "<Keyboard>/d", KeyboardMouse);
            move.AddBinding("<Gamepad>/leftStick").WithGroup(Gamepad);
            move.AddBinding("<Gamepad>/dpad").WithGroup(Gamepad);

            // Mouse aim is a screen position (converted to a direction relative to the player).
            g.AddAction("AimPoint", InputActionType.PassThrough, "<Mouse>/position", groups: KeyboardMouse, expectedControlLayout: "Vector2");

            // Directional aim: right stick, or arrow keys for keyboard-only players (arrows also fire).
            var aim = g.AddAction("AimStick", InputActionType.Value, expectedControlLayout: "Vector2");
            aim.AddBinding("<Gamepad>/rightStick").WithGroup(Gamepad);
            aim.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouse)
                .With("Down", "<Keyboard>/downArrow", KeyboardMouse)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouse)
                .With("Right", "<Keyboard>/rightArrow", KeyboardMouse);

            Button(g, "Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Button(g, "ActiveItem", "<Keyboard>/space", "<Gamepad>/rightShoulder");
            Button(g, "Bomb", "<Keyboard>/e", "<Gamepad>/leftShoulder");
            Button(g, "Consumable", "<Keyboard>/q", "<Gamepad>/buttonWest");
            Button(g, "SwapConsumable", "<Keyboard>/tab", "<Gamepad>/buttonNorth");
            Button(g, "DropTrinket", "<Keyboard>/leftCtrl", "<Gamepad>/leftTrigger");
            Button(g, "Interact", "<Keyboard>/f", "<Gamepad>/buttonSouth");
            Button(g, "Map", "<Keyboard>/m", "<Gamepad>/select");
            Button(g, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            // Secondary mouse binding so the right mouse button can trigger the active item.
            g.FindAction("ActiveItem").AddBinding("<Mouse>/rightButton").WithGroup(KeyboardMouse);

            var ui = asset.AddActionMap(UiMap);
            var nav = ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            nav.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouse).With("Down", "<Keyboard>/s", KeyboardMouse)
                .With("Left", "<Keyboard>/a", KeyboardMouse).With("Right", "<Keyboard>/d", KeyboardMouse);
            nav.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouse).With("Down", "<Keyboard>/downArrow", KeyboardMouse)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouse).With("Right", "<Keyboard>/rightArrow", KeyboardMouse);
            nav.AddBinding("<Gamepad>/leftStick").WithGroup(Gamepad);
            nav.AddBinding("<Gamepad>/dpad").WithGroup(Gamepad);
            Button(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            Button(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            Button(ui, "TabLeft", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Button(ui, "TabRight", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", groups: KeyboardMouse, expectedControlLayout: "Vector2");
            ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", groups: KeyboardMouse);
            return asset;
        }

        private static void Button(InputActionMap map, string name, string keyboardMouse, string gamepad)
        {
            var a = map.AddAction(name, InputActionType.Button);
            a.AddBinding(keyboardMouse).WithGroup(KeyboardMouse);
            a.AddBinding(gamepad).WithGroup(Gamepad);
        }
    }
}
