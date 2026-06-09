using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct ControllableRegistry : IComponentData
    {
        public NativeArray<Entity> ByPlayer;
        public uint Version;

        public readonly Entity Resolve(byte player)
        {
            return ByPlayer.IsCreated && player < ByPlayer.Length ? ByPlayer[player] : Entity.Null;
        }
    }
}