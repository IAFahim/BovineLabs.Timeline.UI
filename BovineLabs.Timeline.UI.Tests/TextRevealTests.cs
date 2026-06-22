using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class TextRevealTests
    {
        [Test]
        public void RevealedCount_Instant_ReturnsLengthRegardlessOfTime()
        {
            Assert.AreEqual(10, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, -5.0, true));
        }

        [Test]
        public void RevealedCount_ZeroDuration_StartEqualsEnd_ReturnsLength()
        {
            Assert.AreEqual(10, TextReveal.RevealedCount(10, 2.0, 2.0, 1.0, 0.0, 0.0, false));
        }

        [Test]
        public void RevealedCount_ZeroDuration_ScaleZero_ReturnsLength()
        {
            Assert.AreEqual(10, TextReveal.RevealedCount(10, 0.0, 1.0, 0.0, 0.0, 0.0, false));
        }

        [Test]
        public void RevealedCount_AtClipIn_ReturnsZero()
        {
            Assert.AreEqual(0, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, 0.0, false));
        }

        [Test]
        public void RevealedCount_AtClipEnd_ReturnsLength()
        {
            Assert.AreEqual(10, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, 1.0, false));
        }

        [Test]
        public void RevealedCount_Midpoint_ReturnsHalf()
        {
            Assert.AreEqual(5, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, 0.5, false));
        }

        [Test]
        public void RevealedCount_RoundsToNearestNotFloor()
        {
            Assert.AreEqual(5, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, 0.46, false));
        }

        [Test]
        public void RevealedCount_ElapsedBeyondDuration_ClampsToLength()
        {
            Assert.AreEqual(10, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, 5.0, false));
        }

        [Test]
        public void RevealedCount_NegativeElapsed_ClampsToZero()
        {
            Assert.AreEqual(0, TextReveal.RevealedCount(10, 0.0, 1.0, 1.0, 0.0, -1.0, false));
        }

        [Test]
        public void BumpHighSurrogate_BetweenSurrogatePair_BumpsByOne()
        {
            var text = "a😀b";

            Assert.AreEqual(3, TextReveal.BumpHighSurrogate(text, 2));
        }

        [Test]
        public void BumpHighSurrogate_VisibleZero_Unchanged()
        {
            var text = "😀";

            Assert.AreEqual(0, TextReveal.BumpHighSurrogate(text, 0));
        }

        [Test]
        public void BumpHighSurrogate_VisibleAtLength_Unchanged()
        {
            var text = "a😀";

            Assert.AreEqual(3, TextReveal.BumpHighSurrogate(text, 3));
        }

        [Test]
        public void BumpHighSurrogate_NonSurrogate_Unchanged()
        {
            var text = "abc";

            Assert.AreEqual(2, TextReveal.BumpHighSurrogate(text, 2));
        }
    }
}
