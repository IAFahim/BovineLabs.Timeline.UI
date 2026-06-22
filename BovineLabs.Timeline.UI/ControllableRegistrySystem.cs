using BovineLabs.Timeline.PlayerInputs.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI
{
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
            byPlayer = new NativeArray<Entity>(PlayerCapacity, Allocator.Persistent);

            var singleton = state.EntityManager.CreateEntity(typeof(ControllableRegistry));
            state.EntityManager.SetComponentData(singleton, new ControllableRegistry { ByPlayer = byPlayer });
        }

        public void OnDestroy(ref SystemState state)
        {
            byPlayer.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            for (var i = 0; i < byPlayer.Length; i++) byPlayer[i] = Entity.Null;

            foreach (var (player, entity) in SystemAPI.Query<RefRO<PlayerId>>().WithAll<Controllable>()
                         .WithEntityAccess())
            {
                var idx = player.ValueRO.Value;
                byPlayer[idx] = ControllableSelection.Select(byPlayer[idx], entity);
            }

            SystemAPI.GetSingletonRW<ControllableRegistry>().ValueRW.Version++;
        }
    }
}