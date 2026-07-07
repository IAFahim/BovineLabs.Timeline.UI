using BovineLabs.Testing;
using BovineLabs.Timeline.UI.Data;
using NUnit.Framework;
using Unity.Entities;
#if !BL_DISABLE_PAUSE
using BovineLabs.Core.Pause;
#endif

namespace BovineLabs.Timeline.UI.Tests
{
    /// <summary>
    /// Pins the <see cref="UIUnscaledClockSystem"/> publish contract (clock policy, TODO.md item 13): the
    /// singleton exists after one update, its delta is inside [0, MaxStep], and the built-in bl-core
    /// <c>PauseGame</c> state (a system-entity component) freezes it to exactly 0.
    /// </summary>
    public class UIUnscaledClockSystemTests : ECSTestsFixture
    {
        [Test]
        public void Publishes_ClampedNonNegativeDelta()
        {
            var sys = this.World.GetOrCreateSystem<UIUnscaledClockSystem>();
            sys.Update(this.WorldUnmanaged);

            var time = this.GetTime();
            Assert.GreaterOrEqual(time.DeltaTime, 0f);
            Assert.LessOrEqual(time.DeltaTime, UIClock.MaxStep);
        }

#if !BL_DISABLE_PAUSE
        [Test]
        public void Paused_PublishesZero()
        {
            var sys = this.World.GetOrCreateSystem<UIUnscaledClockSystem>();

            // PauseGame is attached to a SYSTEM entity by bl-core; mirror that placement here.
            this.Manager.AddComponentData(sys, new PauseGame());
            sys.Update(this.WorldUnmanaged);

            Assert.AreEqual(0f, this.GetTime().DeltaTime);
        }

        [Test]
        public void Unpaused_ResumesPublishing()
        {
            var sys = this.World.GetOrCreateSystem<UIUnscaledClockSystem>();

            this.Manager.AddComponentData(sys, new PauseGame());
            sys.Update(this.WorldUnmanaged);
            Assert.AreEqual(0f, this.GetTime().DeltaTime);

            this.Manager.RemoveComponent<PauseGame>(sys);
            sys.Update(this.WorldUnmanaged);
            Assert.GreaterOrEqual(this.GetTime().DeltaTime, 0f);
        }
#endif

        private UIUnscaledTime GetTime()
        {
            using var query = this.Manager.CreateEntityQuery(typeof(UIUnscaledTime));
            return query.GetSingleton<UIUnscaledTime>();
        }
    }
}
