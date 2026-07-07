using BovineLabs.Core;
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
        // Invariant: a valid PlayerId slot index is in [0, PlayerCapacity). This holds today because
        // PlayerId.Value is a byte (0..255) and PlayerCapacity is 256, so every index is in range. The
        // guard in OnUpdate makes the invariant explicit so a future widening of PlayerId (e.g. to
        // ushort) can never cause an out-of-bounds write into byPlayer — it would drop instead.
        private const int PlayerCapacity = 256;

        private NativeArray<Entity> byPlayer;
        private NativeArray<Entity> previous;
        private NativeArray<bool> duplicateWarned;

        public void OnCreate(ref SystemState state)
        {
            byPlayer = new NativeArray<Entity>(PlayerCapacity, Allocator.Persistent);
            previous = new NativeArray<Entity>(PlayerCapacity, Allocator.Persistent);
            duplicateWarned = new NativeArray<bool>(PlayerCapacity, Allocator.Persistent);

            var singleton = state.EntityManager.CreateEntity(typeof(ControllableRegistry));
            state.EntityManager.SetComponentData(singleton, new ControllableRegistry { ByPlayer = byPlayer });
        }

        public void OnDestroy(ref SystemState state)
        {
            byPlayer.Dispose();
            previous.Dispose();
            duplicateWarned.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            for (var i = 0; i < byPlayer.Length; i++) byPlayer[i] = Entity.Null;

            foreach (var (player, entity) in SystemAPI.Query<RefRO<PlayerId>>().WithAll<Controllable>()
                         .WithEntityAccess())
            {
                int idx = player.ValueRO.Value;

                // Bounds guard — see the PlayerCapacity invariant note above. Always in range for a
                // byte PlayerId; defends against a future widening.
                if (idx < 0 || idx >= byPlayer.Length)
                    continue;

                var existing = byPlayer[idx];

                // Two different Controllable entities claiming the same PlayerId: the winner is an
                // arbitrary, spawn-order-dependent choice (lowest entity index). Warn once per slot so
                // the "HUD shows the wrong character" symptom has a paper trail instead of being silent.
                // Uses the Burst-safe BLLogger ECS singleton (this OnUpdate is Burst-compiled, so the
                // managed BLGlobalLogger path is unavailable). The registry rebuild never gates on the
                // logger — we only warn when a logger singleton exists, and latch once we do.
                if (ControllableSelection.IsDuplicateClaim(existing, entity) && !duplicateWarned[idx]
                    && SystemAPI.TryGetSingleton<BLLogger>(out var logger))
                {
                    duplicateWarned[idx] = true;

                    var msg = new FixedString512Bytes();
                    msg.Append((FixedString128Bytes)"ControllableRegistry: multiple Controllable entities claim PlayerId ");
                    msg.Append(idx);
                    msg.Append((FixedString128Bytes)"; keeping the lowest entity-index winner (spawn-order dependent).");
                    logger.LogWarning512(msg);
                }

                byPlayer[idx] = ControllableSelection.Select(existing, entity);
            }

            // Bump Version only when the resolved set actually changed this frame, so downstream
            // change-detection isn't defeated by a per-frame increment (TODO.md item 14).
            if (ControllableSelection.Changed(byPlayer, previous))
            {
                SystemAPI.GetSingletonRW<ControllableRegistry>().ValueRW.Version++;
                previous.CopyFrom(byPlayer);
            }
        }
    }
}
