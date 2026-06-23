using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class UIFractionTests
    {
        [Test]
        public void DenominatorZero_ReturnsZero()
        {
            Assert.AreEqual(0f, UIFraction.Saturated(5f, 0f));
        }

        [Test]
        public void DenominatorNegative_ReturnsZero()
        {
            Assert.AreEqual(0f, UIFraction.Saturated(5f, -4f));
        }

        [Test]
        public void MidRange_ReturnsRatio()
        {
            Assert.AreEqual(0.25f, UIFraction.Saturated(1f, 4f));
        }

        [Test]
        public void NumeratorAboveDenominator_ReturnsOne()
        {
            Assert.AreEqual(1f, UIFraction.Saturated(6f, 4f));
        }

        [Test]
        public void NumeratorNegative_ReturnsZero()
        {
            Assert.AreEqual(0f, UIFraction.Saturated(-2f, 4f));
        }
    }
}
