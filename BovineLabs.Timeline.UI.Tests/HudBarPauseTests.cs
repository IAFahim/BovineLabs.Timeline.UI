using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    /// <summary>
    /// Pins the pause-resume math for the HudBar chip collapse (clock policy, TODO.md item 13): a fall frozen
    /// mid-flight resumes over exactly the remaining duration, and degenerate progress/duration inputs are safe.
    /// </summary>
    public class HudBarPauseTests
    {
        [Test]
        public void NotStarted_FullDurationRemains()
        {
            Assert.AreEqual(400, HudBar.RemainingCollapseMs(0f, 400));
        }

        [Test]
        public void ThreeQuartersDone_QuarterRemains()
        {
            Assert.AreEqual(100, HudBar.RemainingCollapseMs(0.75f, 400));
        }

        [Test]
        public void Complete_NothingRemains()
        {
            Assert.AreEqual(0, HudBar.RemainingCollapseMs(1f, 400));
        }

        [Test]
        public void OvershotProgress_ClampsToZero()
        {
            Assert.AreEqual(0, HudBar.RemainingCollapseMs(1.5f, 400));
        }

        [Test]
        public void NegativeProgress_ClampsToFullDuration()
        {
            Assert.AreEqual(400, HudBar.RemainingCollapseMs(-0.5f, 400));
        }

        [Test]
        public void NegativeDuration_ClampsToZero()
        {
            Assert.AreEqual(0, HudBar.RemainingCollapseMs(0.5f, -100));
        }

        [Test]
        public void FractionalRemainder_RoundsUp_NeverSkipsTheTail()
        {
            // ceil(0.999 * (1 - 0.7) * 1000) style remainders must not truncate to a shorter fall.
            Assert.AreEqual(34, HudBar.RemainingCollapseMs(2f / 3f, 100));
        }
    }
}
