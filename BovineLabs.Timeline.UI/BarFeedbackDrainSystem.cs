using BovineLabs.Core;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The SINGLE owner of <see cref="BarFeedbackEvent"/> lifetime. Runs once per frame in EVERY simulation world with
    /// no UI dependency, so feedback never leaks in headless/menu worlds and any number of presentation readers (screen
    /// HUD, world-space bar, kill-feed) can consume the same events non-destructively.
    ///
    /// CONTRACT — an event lives exactly ONE frame:
    ///  * A producer appends an event; it MAY leave <see cref="BarFeedbackEvent.Frame"/> at 0 ("unstamped").
    ///  * This system increments a frame counter (never 0 — 0 is reserved for unstamped), publishes it as the
    ///    <see cref="BarFeedbackFrame"/> singleton, stamps every 0-framed event with the current frame, and REMOVES any
    ///    event stamped in a prior frame (readers had one full frame to see it).
    ///  * Readers consume only events whose Frame == the published current frame → each reader sees each event once,
    ///    none destroys it.
    ///  * A safety valve caps every buffer at <see cref="BarFeedbackDefaults.EventCap"/> so a reader-less world cannot
    ///    grow unbounded.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation)]
    public partial struct BarFeedbackDrainSystem : ISystem
    {
        private uint frame;
        private bool warnedCap;

        public void OnCreate(ref SystemState state)
        {
            this.frame = 0;
            state.EntityManager.CreateSingleton(new BarFeedbackFrame { Frame = 0 }, "BarFeedbackFrame");
        }

        public void OnUpdate(ref SystemState state)
        {
            this.frame++;
            if (this.frame == 0)
            {
                this.frame = 1; // 0 is reserved for "unstamped"; skip it on wrap
            }

            var current = this.frame;
            SystemAPI.SetSingleton(new BarFeedbackFrame { Frame = current });

            foreach (var fbIter in SystemAPI.Query<DynamicBuffer<BarFeedbackEvent>>())
            {
                // Copy the (readonly) foreach variable; DynamicBuffer is a view struct, so the copy
                // still writes through to the same underlying buffer.
                var fb = fbIter;

                // Stamp new (Frame==0) events to the current frame; drop anything stamped in a PRIOR frame.
                for (var i = fb.Length - 1; i >= 0; i--)
                {
                    var evt = fb[i];
                    if (evt.Frame == 0)
                    {
                        evt.Frame = current;
                        fb[i] = evt;
                    }
                    else if (evt.Frame != current)
                    {
                        fb.RemoveAtSwapBack(i); // readers had one full frame with it
                    }
                }

                // Safety valve: cap the buffer so a reader-less/drain-only world can't grow unbounded.
                if (fb.Length > BarFeedbackDefaults.EventCap)
                {
                    if (!this.warnedCap)
                    {
                        this.warnedCap = true;
                        BLGlobalLogger.LogWarningString(
                            $"[DataUI] BarFeedbackEvent buffer exceeded {BarFeedbackDefaults.EventCap}; capping. A producer is emitting many events per frame or no reader is consuming them.");
                    }

                    fb.RemoveRange(0, fb.Length - BarFeedbackDefaults.EventCap);
                }
            }
        }
    }
}
