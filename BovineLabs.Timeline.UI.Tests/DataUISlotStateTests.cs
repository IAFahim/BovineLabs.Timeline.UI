using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;

namespace BovineLabs.Timeline.UI.Tests
{
    /// <summary>
    /// Pure coverage of <see cref="HudBarMath.AdvanceSlot"/> — the single per-slot presentation kernel the driver runs
    /// each frame (damage/idle detection, ghost catch-up, flash decay, visibility, heal-lead + flash event routing).
    /// No world, no UI.
    /// </summary>
    public class DataUISlotStateTests
    {
        // ref s, mode, fill, externalGhost, dt, ghostDelay, ghostSpeed, flashOnDamage, flashDecay, flashEvent, healFrac,
        // alwaysVisible, keepVisibleWhileNotFull, showOnHealthChange, autoHideDelay, out ghost, out flash, out alpha, out damaged
        private static void Step(ref HudSlotState s, float fill, float dt, out float ghost, out float flash, out float alpha, out bool damaged,
            HudGhostMode mode = HudGhostMode.ComputedLerp, float ghostDelay = 0.4f, float ghostSpeed = 6f,
            bool flashOnDamage = true, float flashDecay = 0.25f, bool flashEvent = false, float healFrac = 0f,
            bool alwaysVisible = true, bool keepVisibleWhileNotFull = true, bool showOnHealthChange = true, float autoHideDelay = 3f,
            float externalGhost = 0f)
        {
            HudBarMath.AdvanceSlot(ref s, mode, fill, externalGhost, dt, ghostDelay, ghostSpeed, flashOnDamage, flashDecay,
                flashEvent, healFrac, alwaysVisible, keepVisibleWhileNotFull, showOnHealthChange, autoHideDelay,
                out ghost, out flash, out alpha, out damaged);
        }

        [Test]
        public void FirstSight_SnapsGhostToFill_NoDamage_NoFlash()
        {
            var s = new HudSlotState();
            Step(ref s, 0.5f, 0.016f, out var ghost, out var flash, out _, out var damaged);

            Assert.IsFalse(damaged);
            Assert.AreEqual(0.5f, ghost, 1e-4f);
            Assert.AreEqual(0f, flash, 1e-4f);
        }

        [Test]
        public void FillDrop_MarksDamaged_ResetsIdle_AndFlashesOnDamage()
        {
            var s = new HudSlotState();
            Step(ref s, 1f, 0.016f, out _, out _, out _, out _); // prime
            Step(ref s, 1f, 0.5f, out _, out _, out _, out _);    // idle accumulates
            Step(ref s, 0.6f, 0.016f, out _, out var flash, out _, out var damaged);

            Assert.IsTrue(damaged);
            Assert.AreEqual(0f, s.Idle, 1e-5f);
            Assert.AreEqual(1f, flash, 1e-4f); // flashOnDamage → 1
        }

        [Test]
        public void FlashEvent_ForcesFlashOne_EvenWithoutDamage()
        {
            var s = new HudSlotState();
            Step(ref s, 1f, 0.016f, out _, out _, out _, out _); // prime
            Step(ref s, 1f, 0.016f, out _, out var flash, out _, out _, flashOnDamage: false, flashEvent: true);

            Assert.AreEqual(1f, flash, 1e-4f);
        }

        [Test]
        public void HealFrac_PushesGhostBelowFill()
        {
            var s = new HudSlotState();
            Step(ref s, 1f, 0.016f, out _, out _, out _, out _); // prime
            Step(ref s, 1f, 0.016f, out var ghost, out _, out _, out _, healFrac: 0.3f);

            Assert.Less(ghost, 1f);
            Assert.AreEqual(0.7f, ghost, 1e-4f);
        }

        [Test]
        public void AlwaysVisible_AlphaOne()
        {
            var s = new HudSlotState();
            Step(ref s, 1f, 0.016f, out _, out _, out var alpha, out _);
            Assert.AreEqual(1f, alpha, 1e-4f);
        }

        [Test]
        public void AutoHide_AfterIdleExceedsDelay_AlphaZero()
        {
            var s = new HudSlotState();
            // not always-visible, not keep-visible-while-not-full, full bar → only ShowOnHealthChange keeps it up.
            Step(ref s, 1f, 0.016f, out _, out _, out _, out _, mode: HudGhostMode.Off,
                alwaysVisible: false, keepVisibleWhileNotFull: false, showOnHealthChange: true, autoHideDelay: 1f);
            Step(ref s, 1f, 2f, out _, out _, out var alpha, out _, mode: HudGhostMode.Off,
                alwaysVisible: false, keepVisibleWhileNotFull: false, showOnHealthChange: true, autoHideDelay: 1f);

            Assert.AreEqual(0f, alpha, 1e-4f); // idle 2s > 1s delay → hidden
        }

        [Test]
        public void GhostComputedLerp_HoldsAboveFill_ThenCatchesUp()
        {
            var s = new HudSlotState();
            Step(ref s, 1f, 0.016f, out _, out _, out _, out _); // prime full
            // damage to 0.5 with delay 0 so the ghost begins draining but still leads on the first frame.
            Step(ref s, 0.5f, 0.016f, out var g1, out _, out _, out _, ghostDelay: 0f, ghostSpeed: 6f);
            Assert.Greater(g1, 0.5f);

            for (var k = 0; k < 600; k++)
            {
                Step(ref s, 0.5f, 0.016f, out _, out _, out _, out _, ghostDelay: 0f, ghostSpeed: 6f);
            }

            Assert.AreEqual(0.5f, s.Ghost, 1e-3f);
        }

        [Test]
        public void GhostFromStat_TracksExternalNeverBelowFill()
        {
            var s = new HudSlotState();
            Step(ref s, 0.4f, 0.016f, out var ghost, out _, out _, out _, mode: HudGhostMode.FromStat, externalGhost: 0.7f);
            Assert.AreEqual(0.7f, ghost, 1e-4f);

            // external below fill → clamps to fill (ghost never sits below fill on the FromStat path)
            Step(ref s, 0.9f, 0.016f, out var ghost2, out _, out _, out _, mode: HudGhostMode.FromStat, externalGhost: 0.2f);
            Assert.AreEqual(0.9f, ghost2, 1e-4f);
        }
    }
}
