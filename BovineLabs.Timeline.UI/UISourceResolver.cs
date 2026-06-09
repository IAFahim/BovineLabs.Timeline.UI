using System.Runtime.CompilerServices;
using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.UI
{
    public static class UISourceResolver
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolve(
            in UISource source,
            Entity self,
            in ControllableRegistry players,
            in UnsafeComponentLookup<Targets> targets,
            in UnsafeComponentLookup<EntityLinkSource> sources,
            in UnsafeBufferLookup<EntityLinkEntry> links,
            out Entity resolved)
        {
            var seed = source.Player == UISource.NoPlayer ? self : players.Resolve(source.Player);
            if (seed == Entity.Null)
            {
                resolved = Entity.Null;
                return false;
            }

            Entity roled;
            if (source.Route is Target.Self or Target.None)
            {
                roled = seed;
            }
            else if (targets.TryGetComponent(seed, out var frame))
            {
                roled = frame.Get(source.Route, seed);
            }
            else
            {
                resolved = Entity.Null;
                return false;
            }

            if (roled == Entity.Null)
            {
                resolved = Entity.Null;
                return false;
            }

            if (source.LinkKey == 0)
            {
                resolved = roled;
                return true;
            }

            resolved = EntityLinkResolver.TryResolve(roled, source.LinkKey, sources, links, out var linked)
                ? linked
                : roled;
            return true;
        }
    }
}