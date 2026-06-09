namespace BovineLabs.Timeline.UI
{
    using BovineLabs.Timeline.PlayerInputs.Data;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct ControllableRegistrySystem : ISystem
    {
        private const int PlayerCapacity = 256;

        private NativeArray<Entity> byPlayer;

        public void OnCreate(ref SystemState state)
        {
            this.byPlayer = new NativeArray<Entity>(PlayerCapacity, Allocator.Persistent);

            var singleton = state.EntityManager.CreateEntity(typeof(ControllableRegistry));
            state.EntityManager.SetComponentData(singleton, new ControllableRegistry { ByPlayer = this.byPlayer });
        }

        public void OnDestroy(ref SystemState state)
        {
            this.byPlayer.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            for (var i = 0; i < this.byPlayer.Length; i++)
            {
                this.byPlayer[i] = Entity.Null;
            }

            foreach (var (player, entity) in SystemAPI.Query<RefRO<PlayerId>>().WithAll<Controllable>().WithEntityAccess())
            {
                var idx = player.ValueRO.Value;
                if (this.byPlayer[idx] == Entity.Null || entity.Index < this.byPlayer[idx].Index)
                {
                    this.byPlayer[idx] = entity;
                }
            }

            SystemAPI.GetSingletonRW<ControllableRegistry>().ValueRW.Version++;
        }
    }
}
