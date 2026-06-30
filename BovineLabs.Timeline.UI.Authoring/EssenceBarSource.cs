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

        [Header("Ghost / chip")]
        public HudGhostMode ghostMode = HudGhostMode.ComputedLerp;
        [Tooltip("For GhostMode.FromIntrinsic.")]
        public IntrinsicSchemaObject ghostIntrinsic;
        [Tooltip("For GhostMode.FromStat.")]
        public StatSchemaObject ghostStat;
        public float ghostDelay = 0.4f;
        public float ghostSpeed = 6f;

        [Header("Flash")]
        public float flashDecay = 0.25f;
    }
}
