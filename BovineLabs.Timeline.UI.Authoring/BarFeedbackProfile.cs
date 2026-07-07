namespace BovineLabs.Timeline.UI.Authoring
{
    using BovineLabs.Timeline.UI.Data;
    using UnityEngine;

    // CHANGELOG (production audit — "wire or delete dead bar config"):
    //   REMOVED collapseTrigger, collapseEvent, maxHoldMs, healEase, healDrainMs — these were never baked into
    //   UIBindingEntry nor read by any runtime system (dead knobs that silently did nothing). Rather than ship a
    //   half-wired collapse-signal/heal-drain feature, the fields (and the CollapseTrigger enum) were dropped.
    //   The remaining fields ARE wired: trailMode/accumulate/holdMs/drainMs/drainRate/drainEase/minDrainMs/fade/
    //   fadeMs/minChipFrac all bake into UIBindingEntry and reach HudBar via SetTrailConfig. Defaults now come from
    //   BarFeedbackDefaults so an empty profile and "no profile" behave identically.

    /// <summary>
    /// BEHAVIOR SOUL (shared world + screen, zero colors). Drives the ACCUMULATE → HOLD → COLLAPSE(eased) + SWISH trail.
    /// One asset referenced from <see cref="EssenceBarSource"/>.feedback configures both stacks identically. Look stays
    /// per-medium (USS / material). The game still TELLS the bar each hit (an explicit signal with its amount); these
    /// fields only control the VIEW animation the presentation plays.
    /// </summary>
    [CreateAssetMenu(menuName = "Vex/Bar Feedback Profile", fileName = "BarFeedbackProfile")]
    public sealed class BarFeedbackProfile : ScriptableObject
    {
        [Header("Trail")]
        public TrailMode trailMode = TrailMode.GhostSlider;
        [Tooltip("Repeated hits raise the held band (high-water mark) instead of replacing it.")]
        public bool accumulate = BarFeedbackDefaults.Accumulate;
        [Tooltip("Each hit re-arms the hold timer, so a burst keeps the window open.")]
        public bool reArmOnHit = true;
        [Tooltip("Ignore sub-fraction chips so a 1-damage tick doesn't flicker.")]
        [Range(0f, 0.1f)] public float minChipFrac = BarFeedbackDefaults.MinChipFrac;

        [Header("Hold")]
        [Tooltip("Hold this long (no new hit) before draining.")]
        public float holdMs = BarFeedbackDefaults.HoldMs;

        [Header("Drain (the swish)")]
        [Tooltip("Eased drain duration. 0 = use drainRate instead.")]
        public float drainMs = BarFeedbackDefaults.DrainMs;
        [Tooltip("Units/sec drain when drainMs == 0 (duration = band / rate).")]
        public float drainRate = BarFeedbackDefaults.DrainRate;
        public EaseId drainEase = BarFeedbackDefaults.DrainEase;
        [Tooltip("Floor so even a tiny drain is readable.")]
        public float minDrainMs = BarFeedbackDefaults.MinDrainMs;

        [Header("Fade")]
        public bool fade = BarFeedbackDefaults.Fade;
        public float fadeMs = BarFeedbackDefaults.FadeMs;
    }
}
