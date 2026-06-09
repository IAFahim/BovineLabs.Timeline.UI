namespace BovineLabs.Timeline.UI.Data
{
    using Unity.Collections;
    using Unity.Entities;

    public struct ControllableRegistry : IComponentData
    {
        public NativeArray<Entity> ByPlayer;
        public uint Version;

        public readonly Entity Resolve(byte player)
        {
            return this.ByPlayer.IsCreated && player < this.ByPlayer.Length ? this.ByPlayer[player] : Entity.Null;
        }
    }
}
