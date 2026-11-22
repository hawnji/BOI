using System;
using Hollowcrawl.Items;

namespace Hollowcrawl.Input
{
    /// <summary>One frame of menu input (UI action map), device-independent.</summary>
    public struct MenuInputFrame
    {
        public Vec2 Navigate;
        public bool Submit, Cancel, TabLeft, TabRight;
    }

    public interface IMenuInputSource
    {
        MenuInputFrame ReadMenuFrame();
    }

    public enum MenuCommand { None, Up, Down, Left, Right, Submit, Cancel, TabLeft, TabRight }

    /// <summary>
    /// Turns held navigation into discrete steps with an initial delay and key-repeat, the same for sticks,
    /// d-pads and keys. Pure so it's unit-tested.
    /// </summary>
    public sealed class MenuNavigator
    {
        public float InitialDelay = 0.35f;
        public float RepeatInterval = 0.11f;
        public float Deadzone = 0.5f;
        private MenuCommand _held;
        private float _timer;

        public MenuCommand Update(in MenuInputFrame f, float dt)
        {
            if (f.Submit) { _held = MenuCommand.None; return MenuCommand.Submit; }
            if (f.Cancel) { _held = MenuCommand.None; return MenuCommand.Cancel; }
            if (f.TabLeft) return MenuCommand.TabLeft;
            if (f.TabRight) return MenuCommand.TabRight;

            var dir = Direction(f.Navigate);
            if (dir == MenuCommand.None) { _held = MenuCommand.None; return MenuCommand.None; }
            if (dir != _held) { _held = dir; _timer = InitialDelay; return dir; }
            _timer -= dt;
            if (_timer > 0f) return MenuCommand.None;
            _timer += RepeatInterval;
            return dir;
        }

        private MenuCommand Direction(Vec2 v)
        {
            if (Math.Abs(v.X) < Deadzone && Math.Abs(v.Y) < Deadzone) return MenuCommand.None;
            if (Math.Abs(v.Y) >= Math.Abs(v.X)) return v.Y > 0f ? MenuCommand.Up : MenuCommand.Down;
            return v.X > 0f ? MenuCommand.Right : MenuCommand.Left;
        }

        /// <summary>Moves a selection index with wrap-around.</summary>
        public static int Step(int index, int count, MenuCommand cmd, bool horizontal = false)
        {
            if (count <= 0) return 0;
            int d = horizontal
                ? (cmd == MenuCommand.Left ? -1 : cmd == MenuCommand.Right ? 1 : 0)
                : (cmd == MenuCommand.Up ? -1 : cmd == MenuCommand.Down ? 1 : 0);
            return ((index + d) % count + count) % count;
        }
    }
}
