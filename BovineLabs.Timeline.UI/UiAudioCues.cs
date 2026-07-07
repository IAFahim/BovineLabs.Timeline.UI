namespace BovineLabs.Timeline.UI
{
    using BovineLabs.Anchor.Audio;

    /// <summary>
    /// Centralized <see cref="AnchorAudioProfile"/> keys for the Timeline.UI presentation layer, so every call site
    /// (NodeChip hover/click, map navigation, damage vignette) resolves the same named profiles instead of scattering
    /// string literals. The keys are authored as profiles in the project's <see cref="AnchorAudioSettings"/>; an absent
    /// profile simply plays nothing (safe no-op), so these are wire-once seams the audio designer fills in.
    /// </summary>
    public static class UiAudioCues
    {
        /// <summary>Profile for node-chip hover/select and general UI clicks (falls back to the Anchor default profile).</summary>
        public const string Click = AnchorAudioSettings.DefaultProfileKey;

        /// <summary>Profile for navigation transitions (entering/leaving a screen, choosing a map room).</summary>
        public const string Nav = "nav";

        /// <summary>Profile for the damage sting played alongside the damage vignette.</summary>
        public const string Damage = "damage";
    }
}
