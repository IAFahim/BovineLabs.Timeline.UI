using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>
    /// Singleton mapping <c>PlayerId.Value</c> to the winning Controllable entity, rebuilt every frame
    /// by <c>ControllableRegistrySystem</c>.
    /// </summary>
    /// <remarks>
    /// The backing <see cref="ByPlayer" /> array is owned and disposed by the system in OnDestroy —
    /// consumers must resolve through the singleton each frame and never cache the component across
    /// system teardown or world reload (doing so reads freed memory). <see cref="Version" /> is bumped
    /// only when the resolved set actually changes, so it is usable for change detection. Storage type
    /// is intentionally left as a raw <see cref="NativeArray{Entity}" /> here — other systems depend on
    /// this shape and on the <see cref="Resolve" /> signature.
    /// </remarks>
    public struct ControllableRegistry : IComponentData
    {
        public NativeArray<Entity> ByPlayer;

        /// <summary> Incremented only when the by-player mapping changed this frame (not every frame). </summary>
        public uint Version;

        public readonly Entity Resolve(byte player)
        {
            return ByPlayer.IsCreated && player < ByPlayer.Length ? ByPlayer[player] : Entity.Null;
        }
    }
}