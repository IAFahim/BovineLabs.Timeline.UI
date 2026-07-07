using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    /// <summary>
    /// Pins the HUD-feedback clock policy (TODO.md item 13): unscaled input passes through, pause freezes
    /// (0), a frame hitch is clamped so it can never expire a multi-second toast in one step, and a negative
    /// engine delta can never rewind feedback timers.
    /// </summary>
    public class UIClockTests
    {
        [Test]
        public void Normal_PassesThrough()
        {
            Assert.AreEqual(1f / 60f, UIClock.Step(1f / 60f, false));
        }

        [Test]
        public void Paused_IsZero()
        {
            Assert.AreEqual(0f, UIClock.Step(1f / 60f, true));
        }

        [Test]
        public void Hitch_ClampedToMaxStep()
        {
            Assert.AreEqual(UIClock.MaxStep, UIClock.Step(2.5f, false));
        }

        [Test]
        public void Negative_ClampedToZero()
        {
            Assert.AreEqual(0f, UIClock.Step(-0.5f, false));
        }

        [Test]
        public void Zero_StaysZero()
        {
            Assert.AreEqual(0f, UIClock.Step(0f, false));
        }
    }
}
