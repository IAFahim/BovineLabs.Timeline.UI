using System;
using BovineLabs.Anchor;
using Unity.Collections;
using Unity.Properties;

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    /// <summary>
    /// Screen-space HUD view model: one <see cref="Data.PlayerSlot"/> per local co-op player (up to 4), pumped every
    /// frame by <c>HudDriverSystem</c>. Mirrors <c>EssenceUIViewModel</c>'s shape; the screen-space counterpart of the
    /// world-space com.vex.healthbar (same Stat(max) + Intrinsic(current) → Fraction contract).
    /// </summary>
    [IsService]
    public partial class HudViewModel : SystemObservableObject<HudViewModel.Data>, ILoadable
    {
        [CreateProperty(ReadOnly = true)] public bool IsVisible => Value.IsVisible;
        [CreateProperty(ReadOnly = true)] public UIArray<Data.PlayerSlot> Players => Value.Players;

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
            [SystemProperty] private NativeList<PlayerSlot> players;

            internal void Initialize()
            {
                players = new NativeList<PlayerSlot>(Allocator.Persistent);
            }

            internal void Dispose()
            {
                players.Dispose();
            }

            [GeneratePropertyBag]
            public struct PlayerSlot : IEquatable<PlayerSlot>
            {
                public int Player;
                public FixedString32Bytes RawName;
                public int Health;
                public int HealthMax;

                [CreateProperty(ReadOnly = true)] public string Label => RawName.ToString();
                [CreateProperty(ReadOnly = true)] public string Display => $"{Health} / {HealthMax}";

                [CreateProperty(ReadOnly = true)]
                public float Fraction => UIFraction.Saturated(Health, HealthMax);

                public bool Equals(PlayerSlot other)
                {
                    return Player == other.Player && Health == other.Health && HealthMax == other.HealthMax &&
                           RawName.Equals(other.RawName);
                }

                public override int GetHashCode()
                {
                    return unchecked((Player * 397) ^ (Health << 4) ^ HealthMax);
                }
            }
        }
    }
}
