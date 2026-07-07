using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>
    /// Pure helpers for <c>ControllableRegistrySystem</c>: tie-breaking, duplicate detection and
    /// content-change detection. Kept free of world/system state so they are unit-testable in
    /// isolation (see ControllableRegistryGuardTests).
    /// </summary>
    public static class ControllableSelection
    {
        /// <summary> Ties between two entities claiming the same PlayerId break to the lowest entity index. </summary>
        public static Entity Select(Entity current, Entity candidate)
        {
            return current == Entity.Null || candidate.Index < current.Index ? candidate : current;
        }

        /// <summary>
        /// True when <paramref name="candidate" /> collides with an already-claimed, <b>different</b>
        /// entity for a PlayerId slot — i.e. two distinct Controllables share one PlayerId this frame.
        /// A re-observation of the same entity (or an empty slot) is not a duplicate.
        /// </summary>
        public static bool IsDuplicateClaim(Entity existing, Entity candidate)
        {
            return existing != Entity.Null && existing != candidate;
        }

        /// <summary>
        /// True when the resolved by-player set differs from the previous frame's snapshot. Used to
        /// bump <c>ControllableRegistry.Version</c> only on real change so downstream change-detection
        /// isn't defeated by a per-frame increment. Arrays must be the same length.
        /// </summary>
        public static bool Changed(NativeArray<Entity> current, NativeArray<Entity> previous)
        {
            if (current.Length != previous.Length)
                return true;

            for (var i = 0; i < current.Length; i++)
                if (current[i] != previous[i])
                    return true;

            return false;
        }
    }
}
