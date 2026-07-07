using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct ClipStat : IBufferElementData
    {
        public StatKey Key;
        public FixedString32Bytes Name;
    }

    public struct ClipIntrinsic : IBufferElementData
    {
        public IntrinsicKey Key;
        public FixedString32Bytes Name;
        public int Min;
        public int Max;
        public StatKey MinStat;
        public StatKey MaxStat;
    }

    public struct ClipEvent : IBufferElementData
    {
        public ConditionKey Key;
        public FixedString32Bytes Name;
        public float Duration;
    }

    public struct ActiveUIEvent : IBufferElementData
    {
        public ConditionKey Key;
        public FixedString32Bytes Name;
        public int Value;

        /// <summary>
        /// Remaining lifetime of the toast, in <b>game-time seconds</b> (decays with
        /// <c>SystemAPI.Time.DeltaTime</c>, clamped against dt spikes). See TODO.md item 13:
        /// a full scaled-vs-unscaled clock policy is still owed — under bullet-time this decays
        /// at the scaled rate. Do not treat this as wall-clock time.
        /// </summary>
        public float TimeRemaining;

        /// <summary> Original display duration in game-time seconds (see <see cref="TimeRemaining" />). </summary>
        public float Duration;

        /// <summary>
        /// The resolved Essence source entity this toast was captured against. Stamped when the
        /// event is added; used to drop stale toasts when a clip's resolved source changes mid-clip
        /// (link retarget) so surviving toasts are never re-labelled onto a different entity.
        /// </summary>
        public Entity Source;
    }
}