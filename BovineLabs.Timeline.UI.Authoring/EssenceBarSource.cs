namespace BovineLabs.Timeline.UI.Authoring
{
    using BovineLabs.Essence.Authoring;
    using BovineLabs.Timeline.UI.Data;
    using UnityEngine;

    /// <summary>
    /// A SHARED, presentation-agnostic definition of "a value/max bar" — which Essence keys feed it and how the
    /// ghost/flash behave. ONE asset can be referenced by BOTH the world-space bar (com.vex.healthbar) and the
    /// screen-space HUD (DataUISettings), so the DATA is configured once. Colors/size/visibility are NOT here — each
    /// medium owns its look (world = material, UI = USS). Generic: works for health, shield, stamina, anything that is
    /// "an intrinsic current over a stat max".
    /// </summary>
    [CreateAssetMenu(menuName = "Vex/Essence Bar Source", fileName = "EssenceBarSource")]
    public sealed class EssenceBarSource : ScriptableObject
    {
        [Header("Value")]
        [Tooltip("Intrinsic holding the CURRENT value (numerator).")]
        public IntrinsicSchemaObject current;

        [Tooltip("Stat holding the MAX value (denominator). None = no max (plain readout).")]
        public StatSchemaObject max;

        [Tooltip("Optional: intrinsic holding LOCKED health (e.g. a curse). Rendered as a hatched band at the high end; " +
                 "a current drop under the lock is NEVER shown as damage — it's structure, read not inferred.")]
        public IntrinsicSchemaObject locked;

        [Header("Ghost / chip")]
        public HudGhostMode ghostMode = HudGhostMode.ComputedLerp;
        [Tooltip("For GhostMode.FromIntrinsic.")]
        public IntrinsicSchemaObject ghostIntrinsic;
        [Tooltip("For GhostMode.FromStat.")]
        public StatSchemaObject ghostStat;
        public float ghostDelay = BarFeedbackDefaults.GhostDelay;
        public float ghostSpeed = BarFeedbackDefaults.GhostSpeed;

        [Header("Flash")]
        public float flashDecay = BarFeedbackDefaults.FlashDecay;

        [Header("Trail behaviour (optional)")]
        [Tooltip("Shared feedback profile — drop-chip/slider toggle, accumulate, hold, drain ease/rate, fade. " +
                 "None = BarFeedbackDefaults. The SAME asset drives both the world bar and the HUD.")]
        public BarFeedbackProfile feedback;

        /// <summary>True when a Max stat is assigned. Lets editor-time validators in another assembly check the
        /// denominator without needing a direct reference to the Essence schema type.</summary>
        public bool HasMax => this.max != null;
    }
}
