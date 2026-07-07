using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    // Pure-logic tests for HudBar's factored-out collapse-duration math. No panel / VisualElement is constructed —
    // ComputeCollapseDurationMs is static, so the drainRate-vs-drainMs decision is testable in isolation.
    public class HudBarLogicTests
    {
        [Test]
        public void DrainMsPositive_UsesMaxOfMinAndDrain()
        {
            // drainMs wins when set; drainRate is ignored.
            Assert.AreEqual(450, HudBar.ComputeCollapseDurationMs(0.9f, 450f, 120f, 1.5f));
        }

        [Test]
        public void DrainMsBelowMin_FloorsToMinDrain()
        {
            Assert.AreEqual(120, HudBar.ComputeCollapseDurationMs(0.9f, 50f, 120f, 1.5f));
        }

        [Test]
        public void DrainMsZero_UsesDrainRate_BandOverRate()
        {
            // band / rate seconds -> ms: 0.75 / 1.5 = 0.5s = 500ms, above the 120ms floor.
            Assert.AreEqual(500, HudBar.ComputeCollapseDurationMs(0.75f, 0f, 120f, 1.5f));
        }

        [Test]
        public void DrainMsZero_DrainRateResult_FloorsToMinDrain()
        {
            // 0.06 / 1.5 = 0.04s = 40ms -> clamped up to the 120ms floor.
            Assert.AreEqual(120, HudBar.ComputeCollapseDurationMs(0.06f, 0f, 120f, 1.5f));
        }

        [Test]
        public void DrainMsZero_DrainRateResult_CapsAt5000()
        {
            // A huge band at a slow rate would blow past the cap; clamp to 5000ms.
            Assert.AreEqual(5000, HudBar.ComputeCollapseDurationMs(1f, 0f, 120f, 0.05f));
        }

        [Test]
        public void DrainMsZeroAndRateZero_FallsBackToMinDrain()
        {
            // No usable rate -> max(minDrainMs, drainMs) = max(120, 0) = 120.
            Assert.AreEqual(120, HudBar.ComputeCollapseDurationMs(0.9f, 0f, 120f, 0f));
        }

        [Test]
        public void NegativeBand_ClampedToMinDrain()
        {
            // Defensive: a negative band under the drainRate path floors to minDrainMs rather than going negative.
            Assert.AreEqual(120, HudBar.ComputeCollapseDurationMs(-0.2f, 0f, 120f, 1.5f));
        }
    }
}
