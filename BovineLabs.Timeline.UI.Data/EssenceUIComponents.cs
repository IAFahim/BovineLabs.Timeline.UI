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
        public float TimeRemaining;
        public float Duration;
    }
}
