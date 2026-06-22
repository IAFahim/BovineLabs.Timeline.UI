using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class EssenceUIDecayTests
    {
        [Test]
        public void Surviving_ReturnsFalseAndRemaining()
        {
            var expired = EssenceUIDecay.TryDecay(1.0f, 0.25f, out var next);

            Assert.IsFalse(expired);
            Assert.AreEqual(0.75f, next);
        }

        [Test]
        public void ExactlyZero_HitsBoundaryAsExpired()
        {
            var expired = EssenceUIDecay.TryDecay(0.25f, 0.25f, out var next);

            Assert.IsTrue(expired);
            Assert.AreEqual(0f, next);
        }

        [Test]
        public void Overshoot_ReturnsTrueAndNegative()
        {
            var expired = EssenceUIDecay.TryDecay(0.25f, 0.5f, out var next);

            Assert.IsTrue(expired);
            Assert.AreEqual(-0.25f, next);
        }

        [Test]
        public void ZeroDelta_DoesNotExpire()
        {
            var expired = EssenceUIDecay.TryDecay(0.5f, 0f, out var next);

            Assert.IsFalse(expired);
            Assert.AreEqual(0.5f, next);
        }
    }
}
