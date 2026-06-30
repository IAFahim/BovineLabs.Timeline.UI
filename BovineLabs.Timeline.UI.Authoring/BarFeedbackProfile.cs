namespace BovineLabs.Timeline.UI.Authoring
{
    using BovineLabs.Reaction.Authoring.Conditions;
    using BovineLabs.Timeline.UI.Data;
    using UnityEngine;

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
        public bool accumulate = true;
        [Tooltip("Each hit re-arms the hold timer, so a burst keeps the window open.")]
        public bool reArmOnHit = true;
        [Tooltip("Ignore sub-fraction chips so a 1-damage tick doesn't flicker.")]
        [Range(0f, 0.1f)] public float minChipFrac = 0.005f;

        [Header("Collapse")]
        public CollapseTrigger collapseTrigger = CollapseTrigger.Timeout;
        [Tooltip("Hold this long (no new hit) before draining, when trigger includes Timeout.")]
        public float holdMs = 350f;
        [Tooltip("Explicit 'collapse now' event (for Signaled/Both).")]
        public ConditionEventObject collapseEvent;
        [Tooltip("Safety cap: drain after this long even if a Signaled event never fires.")]
        public float maxHoldMs = 4000f;

        [Header("Drain (the swish)")]
        [Tooltip("Eased drain duration. 0 = use drainRate instead.")]
        public float drainMs = 450f;
        [Tooltip("Units/sec drain when drainMs == 0 (duration = band / rate).")]
        public float drainRate = 1.5f;
        public EaseId drainEase = EaseId.OutCubic;
        [Tooltip("Floor so even a tiny drain is readable.")]
        public float minDrainMs = 120f;

        [Header("Fade")]
        public bool fade = true;
        public float fadeMs = 200f;

        [Header("Heal lead (ghost below fill)")]
        public EaseId healEase = EaseId.OutQuad;
        public float healDrainMs = 350f;
    }
}
