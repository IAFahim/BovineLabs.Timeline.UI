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

            // frame is default(Targets) (all-null slots) when seed has no Targets component — matches the old
            // "return false" path for a real slot route (Targets.Get on the empty frame yields Entity.Null).
            targets.TryGetComponent(seed, out var frame);

            // Preserve the historical semantics: this resolver has always treated Target.None like Target.Self
            // (resolve from the seed itself). Targets.Get maps None => Null, so coerce it on a local copy.
            var link = source.Link;
            if (link.ReadRootFrom == Target.None)
            {
                link.ReadRootFrom = Target.Self;
            }

            return link.TryResolve(seed, frame, sources, links, out resolved);
        }
    }
}