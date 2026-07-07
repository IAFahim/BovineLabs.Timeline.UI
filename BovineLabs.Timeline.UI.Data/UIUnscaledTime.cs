using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>
    /// The HUD-feedback clock (TODO.md item 13 — clock policy). ONE policy, encoded here: HUD feedback timing
    /// (toast decay, ghost/flash catch-up, idle/auto-hide) runs on UNSCALED presentation time, so
    /// <c>WorldTimeScale</c> bullet-time neither stretches a 2 s toast to 20 s nor freezes it at timescale 0;
    /// game pause (bl-core <c>PauseGame</c>) freezes HUD feedback (the published delta is 0 while paused).
    /// Published once per frame by <c>UIUnscaledClockSystem</c>; consumed by <c>EssenceUITrackSystem</c> (toast
    /// decay) and <c>DataUIDriverSystem</c> (kernel dt). Gameplay-driven fills stay on scaled sim time — only
    /// feedback presentation reads this clock.
    /// </summary>
    public struct UIUnscaledTime : IComponentData
    {
        /// <summary>Unscaled, pause-aware, hitch-clamped delta seconds for this frame. 0 while paused.</summary>
        public float DeltaTime;
    }

    /// <summary>Pure step policy for <see cref="UIUnscaledTime"/> so it is unit-testable without a world.</summary>
    public static class UIClock
    {
        /// <summary>Hitch clamp: a stall (or resume-from-pause spike) may never expire a multi-second toast or
        /// snap the ghost in a single frame. Mirrors the pre-policy clamp the systems used on scaled time.</summary>
        public const float MaxStep = 0.1f;

        /// <summary>Paused → 0 (HUD feedback freezes); otherwise the unscaled delta clamped to [0, MaxStep].</summary>
        public static float Step(float unscaledDeltaTime, bool paused)
        {
            return paused ? 0f : math.clamp(unscaledDeltaTime, 0f, MaxStep);
        }
    }
}
