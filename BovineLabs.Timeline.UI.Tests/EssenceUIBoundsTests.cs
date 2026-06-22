using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class EssenceUIBoundsTests
    {
        [Test]
        public void NoOverride_KeepsClipBounds()
        {
            EssenceUIBounds.ResolveIntrinsicBounds(2, 9, false, 0f, false, 0f, out var min, out var max);

            Assert.AreEqual(2, min);
            Assert.AreEqual(9, max);
        }

        [Test]
        public void MinOverride_FloorsTowardZero()
        {
            EssenceUIBounds.ResolveIntrinsicBounds(2, 9, true, 7.9f, false, 0f, out var min, out var max);

            Assert.AreEqual(7, min);
            Assert.AreEqual(9, max);
        }

        [Test]
        public void MaxOverride_FloorsTowardNegativeInfinity()
        {
            EssenceUIBounds.ResolveIntrinsicBounds(2, 9, false, 0f, true, -2.1f, out var min, out var max);

            Assert.AreEqual(2, min);
            Assert.AreEqual(-3, max);
        }
    }
}
