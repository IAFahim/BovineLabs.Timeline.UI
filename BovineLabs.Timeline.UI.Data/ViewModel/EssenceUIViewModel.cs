using System;
using BovineLabs.Anchor;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    [IsService]
    public partial class EssenceUIViewModel : SystemObservableObject<EssenceUIViewModel.Data>, ILoadable
    {
        [CreateProperty(ReadOnly = true)] public bool IsVisible => Value.IsVisible;
        [CreateProperty(ReadOnly = true)] public UIArray<Data.StatRow> Stats => Value.Stats;
        [CreateProperty(ReadOnly = true)] public UIArray<Data.IntrinsicRow> Intrinsics => Value.Intrinsics;
        [CreateProperty(ReadOnly = true)] public UIArray<Data.EventRow> Events => Value.Events;

        public void Load()
        {
            Value.Initialize();
        }

        public void Unload()
        {
            Value.Dispose();
        }

        public partial struct Data
        {
            [SystemProperty] private bool isVisible;
            [SystemProperty] private NativeList<StatRow> stats;
            [SystemProperty] private NativeList<IntrinsicRow> intrinsics;
            [SystemProperty] private NativeList<EventRow> events;

            internal void Initialize()
            {
                stats = new NativeList<StatRow>(Allocator.Persistent);
                intrinsics = new NativeList<IntrinsicRow>(Allocator.Persistent);
                events = new NativeList<EventRow>(Allocator.Persistent);
            }

            internal void Dispose()
            {
                stats.Dispose();
                intrinsics.Dispose();
                events.Dispose();
            }

            [GeneratePropertyBag]
            public struct StatRow : IEquatable<StatRow>
            {
                public int Player;
                public ushort Key;
                public FixedString32Bytes RawName;
                public int Added;
                public float Multi;
                public float Scaled;

                [CreateProperty(ReadOnly = true)] public string Label => RawName.ToString();
                [CreateProperty(ReadOnly = true)] public string Value => Scaled.ToString("0.##");
                [CreateProperty(ReadOnly = true)] public string Breakdown => $"{Added} x {Multi.ToString("0.##")}";

                public bool Equals(StatRow other)
                {
                    return Player == other.Player && Key == other.Key && Added == other.Added &&
                           Multi.Equals(other.Multi);
                }

                public override int GetHashCode()
                {
                    return unchecked((Player * 397) ^ (Key << 8) ^ Added);
                }
            }

            [GeneratePropertyBag]
            public struct IntrinsicRow : IEquatable<IntrinsicRow>
            {
                public int Player;
                public ushort Key;
                public FixedString32Bytes RawName;
                public int Current;
                public int Min;
                public int Max;

                [CreateProperty(ReadOnly = true)] public string Label => RawName.ToString();
                [CreateProperty(ReadOnly = true)] public string Display => $"{Current} / {Max}";

                [CreateProperty(ReadOnly = true)]
                public float Fraction => Max > Min ? math.saturate((Current - (float)Min) / (Max - Min)) : 0f;

                public bool Equals(IntrinsicRow other)
                {
                    return Player == other.Player && Key == other.Key && Current == other.Current && Min == other.Min &&
                           Max == other.Max;
                }

                public override int GetHashCode()
                {
                    return unchecked((Player * 397) ^ (Key << 8) ^ Current);
                }
            }

            [GeneratePropertyBag]
            public struct EventRow : IEquatable<EventRow>
            {
                public int Player;
                public int Key;
                public FixedString32Bytes RawName;
                public int Amount;
                public float TimeRemaining;
                public float Duration;

                [CreateProperty(ReadOnly = true)] public string Label => RawName.ToString();
                [CreateProperty(ReadOnly = true)] public string Display => Amount.ToString();

                [CreateProperty(ReadOnly = true)]
                public float Fade => Duration > 0f ? math.saturate(TimeRemaining / Duration) : 0f;

                public bool Equals(EventRow other)
                {
                    return Player == other.Player && Key == other.Key && Amount == other.Amount &&
                           TimeRemaining.Equals(other.TimeRemaining);
                }

                public override int GetHashCode()
                {
                    return unchecked((Player * 397) ^ (Key << 8) ^ Amount);
                }
            }
        }
    }
}