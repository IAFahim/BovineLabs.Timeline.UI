using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct UISource : IComponentData
    {
        public const byte NoPlayer = byte.MaxValue;

        public byte Player;
        public EntityLinkRef Link;

        public static readonly UISource Binding = new()
        {
            Player = NoPlayer,
            Link = new EntityLinkRef { ReadRootFrom = Target.Self, LinkKey = 0 },
        };
    }
}
