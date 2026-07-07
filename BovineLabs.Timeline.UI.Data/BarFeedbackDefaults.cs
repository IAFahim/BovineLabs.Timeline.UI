namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>
    /// The ONE place bar-feedback defaults and the near-full / near-equal epsilons live, so the baker's "no profile"
    /// fallbacks, the <c>BarFeedbackProfile</c> field initializers, and the runtime driver cannot drift apart. Adding
    /// an empty profile must NOT change behaviour — that only holds if all three read the same numbers from here.
    /// Referenced by both the authoring (bake) and runtime (driver / drain) assemblies.
    /// </summary>
    public static class BarFeedbackDefaults
    {
        // Trail / collapse timing — mirror the BarFeedbackProfile field defaults EXACTLY.
        public const float HoldMs = 350f;
        public const float DrainMs = 450f;
        public const float MinDrainMs = 120f;
        public const float DrainRate = 1.5f;
        public const float FadeMs = 200f;
        public const float MinChipFrac = 0.005f;
        public const bool Accumulate = true;
        public const bool Fade = true;
        public const EaseId DrainEase = EaseId.OutCubic;

        // Ghost / flash behaviour — mirror the EssenceBarSource defaults.
        public const float GhostDelay = 0.4f;
        public const float GhostSpeed = 6f;
        public const float FlashDecay = 0.25f;

        // Near-full / near-equal epsilons (were scattered literals: driver 0.999f, TargetAlpha 1e-3f, HudBar 0.003f).
        public const float FullEpsilon = 1e-3f;
        public const float GhostEpsilon = 0.003f;

        // Feedback buffer safety valve — the drain system caps every BarFeedbackEvent buffer at this many events.
        public const int EventCap = 64;
    }
}
