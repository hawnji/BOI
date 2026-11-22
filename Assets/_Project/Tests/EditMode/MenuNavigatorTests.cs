using Hollowcrawl.Input;
using Hollowcrawl.Items;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class MenuNavigatorTests
    {
        [Test]
        public void HeldDirectionStepsOnceThenRepeatsAfterDelay()
        {
            var nav = new MenuNavigator { InitialDelay = 0.3f, RepeatInterval = 0.1f };
            var down = new MenuInputFrame { Navigate = new Vec2(0f, -1f) };
            Assert.That(nav.Update(down, 0.016f), Is.EqualTo(MenuCommand.Down));
            int steps = 0;
            for (int i = 0; i < 60; i++) if (nav.Update(down, 0.01f) == MenuCommand.Down) steps++; // 0.6s held
            Assert.That(steps, Is.InRange(3, 4), "first repeat after 0.3s, then every 0.1s");
            Assert.That(nav.Update(default, 0.01f), Is.EqualTo(MenuCommand.None));
            Assert.That(nav.Update(down, 0.01f), Is.EqualTo(MenuCommand.Down), "release and press again steps immediately");
        }

        [Test]
        public void SmallStickNoiseIsIgnoredAndDominantAxisWins()
        {
            var nav = new MenuNavigator();
            Assert.That(nav.Update(new MenuInputFrame { Navigate = new Vec2(0.2f, 0.3f) }, 0.016f), Is.EqualTo(MenuCommand.None));
            Assert.That(nav.Update(new MenuInputFrame { Navigate = new Vec2(0.9f, 0.6f) }, 0.016f), Is.EqualTo(MenuCommand.Right));
            Assert.That(nav.Update(new MenuInputFrame { Submit = true, Navigate = new Vec2(1f, 0f) }, 0.016f), Is.EqualTo(MenuCommand.Submit));
        }

        [Test]
        public void StepWrapsAround()
        {
            Assert.That(MenuNavigator.Step(0, 5, MenuCommand.Up), Is.EqualTo(4));
            Assert.That(MenuNavigator.Step(4, 5, MenuCommand.Down), Is.EqualTo(0));
            Assert.That(MenuNavigator.Step(2, 5, MenuCommand.Right, horizontal: true), Is.EqualTo(3));
            Assert.That(MenuNavigator.Step(2, 5, MenuCommand.Right), Is.EqualTo(2), "vertical lists ignore left/right");
        }
    }
}
