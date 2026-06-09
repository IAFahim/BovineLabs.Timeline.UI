using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct UISource : IComponentData
    {
        public const byte NoPlayer = byte.MaxValue;

        public byte Player;
        public Target Route;
        public ushort LinkKey;

        public static readonly UISource Binding = new()
        {
            Player = NoPlayer,
            Route = Target.Self,
            LinkKey = 0
        };
    }
}