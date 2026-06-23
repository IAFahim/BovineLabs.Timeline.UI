using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    public class NumberFoldTests
    {
        [Test]
        public void SingleValue_BecomesVisibleAndFolded()
        {
            var folded = int.MinValue;
            var visible = false;

            NumberFold.Accumulate(ref folded, ref visible, 42);

            Assert.IsTrue(visible);
            Assert.AreEqual(42, folded);
        }

        [Test]
        public void Max_IsOrderIndependent()
        {
            var ascendingFolded = int.MinValue;
            var ascendingVisible = false;
            NumberFold.Accumulate(ref ascendingFolded, ref ascendingVisible, 1);
            NumberFold.Accumulate(ref ascendingFolded, ref ascendingVisible, 5);
            NumberFold.Accumulate(ref ascendingFolded, ref ascendingVisible, 3);

            var descendingFolded = int.MinValue;
            var descendingVisible = false;
            NumberFold.Accumulate(ref descendingFolded, ref descendingVisible, 3);
            NumberFold.Accumulate(ref descendingFolded, ref descendingVisible, 5);
            NumberFold.Accumulate(ref descendingFolded, ref descendingVisible, 1);

            Assert.AreEqual(5, ascendingFolded);
            Assert.AreEqual(ascendingFolded, descendingFolded);
        }

        [Test]
        public void NegativeValue_OverridesSeed()
        {
            var folded = int.MinValue;
            var visible = false;

            NumberFold.Accumulate(ref folded, ref visible, -7);

            Assert.IsTrue(visible);
            Assert.AreEqual(-7, folded);
        }

        [Test]
        public void NoAccumulation_LeavesSeedAndInvisible()
        {
            var folded = int.MinValue;
            var visible = false;

            Assert.IsFalse(visible);
            Assert.AreEqual(int.MinValue, folded);
        }
    }
}
