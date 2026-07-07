using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
#if !BL_DISABLE_PAUSE
using BovineLabs.Core.Pause;
#endif

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// Publishes the <see cref="UIUnscaledTime"/> singleton — the ONE place the HUD-feedback clock touches engine
    /// time (TODO.md item 13). Runs in initialization so both the track systems (toast decay) and the presentation
    /// driver (kernel dt) read the same value in the same frame. Pause detection uses the built-in bl-core
    /// <see cref="PauseGame"/> component (on a system entity, hence <see cref="EntityQueryOptions.IncludeSystems"/>);
    /// no ad-hoc pause state is introduced.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct UIUnscaledClockSystem : ISystem
    {
#if !BL_DISABLE_PAUSE
        private EntityQuery pauseQuery;
#endif

        public void OnCreate(ref SystemState state)
        {
#if !BL_DISABLE_PAUSE
            using var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PauseGame>()
                .WithOptions(EntityQueryOptions.IncludeSystems);
            this.pauseQuery = builder.Build(ref state);
#endif
            state.EntityManager.CreateEntity(typeof(UIUnscaledTime));
        }

        // Deliberately NOT [BurstCompile]: UnityEngine.Time.unscaledDeltaTime is a managed engine call. This is the
        // single non-Burst crossing; every consumer stays Burst-compatible by reading the singleton.
        public void OnUpdate(ref SystemState state)
        {
#if !BL_DISABLE_PAUSE
            var paused = !this.pauseQuery.IsEmptyIgnoreFilter;
#else
            var paused = false;
#endif
            SystemAPI.SetSingleton(new UIUnscaledTime
            {
                DeltaTime = UIClock.Step(UnityEngine.Time.unscaledDeltaTime, paused),
            });
        }
    }
}
