using BovineLabs.Testing;
using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Tests
{
    /// <summary>
    /// Pins the non-destructive, bounded feedback contract owned by <see cref="BarFeedbackDrainSystem"/>: unstamped
    /// events get stamped to the current frame, survive exactly one frame, and the buffer is capped so a reader-less
    /// world cannot grow unbounded.
    /// </summary>
    public class BarFeedbackDrainTests : ECSTestsFixture
    {
        private uint Drain()
        {
            this.World.GetOrCreateSystem<BarFeedbackDrainSystem>().Update(this.WorldUnmanaged);
            this.Manager.CompleteAllTrackedJobs();

            var q = this.Manager.CreateEntityQuery(typeof(BarFeedbackFrame));
            var frame = q.GetSingleton<BarFeedbackFrame>().Frame;
            q.Dispose();
            return frame;
        }

        [Test]
        public void UnstampedEvent_GetsStampedToCurrentFrame_AndSurvivesOneFrame()
        {
            var e = this.Manager.CreateEntity();
            var b = this.Manager.AddBuffer<BarFeedbackEvent>(e);
            b.Add(new BarFeedbackEvent { Kind = FeedbackKind.DamageChip, Amount = 10 });

            var f1 = this.Drain();

            var buf = this.Manager.GetBuffer<BarFeedbackEvent>(e, true);
            Assert.AreEqual(1, buf.Length);
            Assert.AreEqual(f1, buf[0].Frame);
            Assert.AreNotEqual(0u, f1);
        }

        [Test]
        public void PriorFrameEvent_IsRemovedNextFrame()
        {
            var e = this.Manager.CreateEntity();
            var b = this.Manager.AddBuffer<BarFeedbackEvent>(e);
            b.Add(new BarFeedbackEvent { Kind = FeedbackKind.DamageChip, Amount = 10 });

            this.Drain(); // stamp to frame N
            this.Drain(); // frame N+1: last-frame event gone

            var buf = this.Manager.GetBuffer<BarFeedbackEvent>(e, true);
            Assert.AreEqual(0, buf.Length);
        }

        [Test]
        public void FrameCounter_NeverZero_AndIncrements()
        {
            var a = this.Drain();
            var b = this.Drain();

            Assert.AreNotEqual(0u, a);
            Assert.AreNotEqual(0u, b);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Buffer_IsCappedAtEventCap()
        {
            var e = this.Manager.CreateEntity();
            var b = this.Manager.AddBuffer<BarFeedbackEvent>(e);
            for (var i = 0; i < BarFeedbackDefaults.EventCap + 50; i++)
            {
                b.Add(new BarFeedbackEvent { Kind = FeedbackKind.DamageChip, Amount = 1 });
            }

            this.Drain();

            var buf = this.Manager.GetBuffer<BarFeedbackEvent>(e, true);
            Assert.LessOrEqual(buf.Length, BarFeedbackDefaults.EventCap);
        }
    }
}
