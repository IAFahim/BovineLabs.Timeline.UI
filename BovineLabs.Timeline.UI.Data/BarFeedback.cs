using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>
    /// The ONE shared feedback vocabulary (used by the screen HUD and, later, the world bar). Each kind is a transient
    /// view pulse the bar PLAYS — it is never inferred. The game (or the showcase conductor) decides WHAT happened and
    /// fires the matching signal; the bar only renders.
    /// </summary>
    public enum FeedbackKind : byte
    {
        DamageChip = 0, // lost-health chunk: drops/recedes off the bar
        HealSurge = 1,  // gained-health band: green rise + fade
        Flash = 2,      // brief whole-bar whiten
        ShieldHit = 3,  // a shield/armor layer absorbed
        ShieldBreak = 4,
        ShieldGain = 5,
        Overheal = 6,
        Block = 7,
        Crit = 8,
        LockForm = 9,   // a curse locked part of max (neutral cue, never damage)
        LockLift = 10,
    }

    /// <summary>
    /// The DUMB-UI inbox. The game / conductor appends EXACTLY ONE event per explicit gameplay signal, each carrying its
    /// own amount — so the bar never diffs the fill to guess damage-vs-heal-vs-lock-vs-clamp. The driver drains this
    /// buffer each frame, spawns a view pulse per event, then clears it. Lives on the source (Essence) entity, modelled
    /// on Essence's ActiveUIEvent. (Reserved for transient flair — crit/shield/block; the basic damage/heal AMOUNT is
    /// shown by the ghost slider below.)
    /// </summary>
    public struct BarFeedbackEvent : IBufferElementData
    {
        public FeedbackKind Kind;
        public int Amount;     // raw amount (same units as the bound intrinsic); sizes the pulse
        public ushort PoolKey; // which pool/segment fired it (0 = main HP)
        public byte Element;   // damage element/type → tint modifier class (0 = none)
        public byte Flags;     // crit/pierce/self/etc. → modifier classes
    }

    /// <summary>
    /// The GHOST SLIDER value — a PASSED secondary level (the "delayed damage / heal" bar, Image #9). The game (or the
    /// conductor) writes this value; it lags behind the live current (lingers after a hit, then drains to catch up), and
    /// leads after a heal. The bar renders the band between current and Value, colored by which side it's on. The UI
    /// never computes the lag — it just renders the value it is given (same units as the current intrinsic).
    /// </summary>
    public struct BarGhost : IComponentData
    {
        public float Value;
    }

    /// <summary>How the recently-lost trail presents: a slider that recedes in place, a chip that detaches and drops, or both.</summary>
    public enum TrailMode : byte
    {
        GhostSlider = 0,
        DropChip = 1,
        Both = 2,
    }

    /// <summary>What ends the HOLD and starts the eased collapse.</summary>
    public enum CollapseTrigger : byte
    {
        Timeout = 0,
        Signaled = 1,
        Both = 2,
    }
}
