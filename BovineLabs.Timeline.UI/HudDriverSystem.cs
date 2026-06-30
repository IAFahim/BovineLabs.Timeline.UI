using BovineLabs.Anchor;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Timeline.UI.Data;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// Always-on screen-HUD driver. Reads up to 4 local co-op players' health (Essence Intrinsic current + Stat max)
    /// via <see cref="ControllableRegistry"/> and pumps one <see cref="HudViewModel.Data.PlayerSlot"/> per player into
    /// <see cref="HudViewModel"/>. Unlike the timeline EssenceUI systems this is NOT clip-gated — the HUD is always
    /// live. It is deliberately NOT [BurstCompile] because it reads the managed <see cref="AnchorApp.Current"/> to
    /// guard against the no-app crash (the loop is 4 iterations, so the cost is irrelevant).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct HudDriverSystem : ISystem
    {
        private const int MaxPlayers = 4;

        private UIHelper<HudViewModel, HudViewModel.Data> uiHelper;
        private NativeList<HudViewModel.Data.PlayerSlot> scratch;
        private bool bound;
        private int lastLoggedSlots;

        public void OnCreate(ref SystemState state)
        {
            this.uiHelper = new UIHelper<HudViewModel, HudViewModel.Data>(
                ref state, ComponentType.ReadOnly<ControllableRegistry>());
            this.scratch = new NativeList<HudViewModel.Data.PlayerSlot>(Allocator.Persistent);

            state.RequireForUpdate<ControllableRegistry>();
            state.RequireForUpdate<HudConfig>();
        }

        public void OnDestroy(ref SystemState state)
        {
            // Only unbind while the app is still alive; on full teardown the service is already gone (process exiting).
            if (this.bound && AnchorApp.Current != null)
            {
                this.uiHelper.Unbind();
                this.bound = false;
            }

            this.scratch.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            // No live Anchor app yet (or it was torn down) → never dereference a null app (the PROGRESS #10 crash).
            if (AnchorApp.Current == null)
            {
                return;
            }

            if (!this.bound)
            {
                this.uiHelper.Bind();
                this.bound = true;
            }

            var config = SystemAPI.GetSingleton<HudConfig>();
            var players = SystemAPI.GetSingleton<ControllableRegistry>();
            var intrinsics = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var stats = SystemAPI.GetBufferLookup<Stat>(true);

            this.scratch.Clear();

            for (byte p = 0; p < MaxPlayers; p++)
            {
                var entity = players.Resolve(p);
                if (entity == Entity.Null)
                {
                    continue;
                }

                var current = 0;
                if (intrinsics.TryGetBuffer(entity, out var intrinsicBuffer))
                {
                    var map = intrinsicBuffer.AsMap();
                    if (map.TryGetValue(config.Health, out var value))
                    {
                        current = value;
                    }
                }

                var max = 0;
                if (stats.TryGetBuffer(entity, out var statBuffer))
                {
                    var map = statBuffer.AsMap();
                    if (map.TryGetValue(config.HealthMax, out var stat))
                    {
                        max = (int)math.round(stat.ValueFloat);
                    }
                }

                var name = default(FixedString32Bytes);
                name.Append('P');
                name.Append(p + 1);

                this.scratch.Add(new HudViewModel.Data.PlayerSlot
                {
                    Player = p,
                    RawName = name,
                    Health = current,
                    HealthMax = max,
                });
            }

            if (this.scratch.Length != this.lastLoggedSlots)
            {
                this.lastLoggedSlots = this.scratch.Length;
                var ctrlCount = SystemAPI.QueryBuilder()
                    .WithAll<BovineLabs.Timeline.PlayerInputs.Data.PlayerId, BovineLabs.Timeline.PlayerInputs.Data.Controllable>()
                    .Build().CalculateEntityCount();
                UnityEngine.Debug.Log($"[HUD] slots={this.scratch.Length} ctrlEntities={ctrlCount} version={players.Version} configHealth={config.Health.Value} configMax={config.HealthMax.Value}");
            }

            ref var data = ref this.uiHelper.Binding;
            data.IsVisible = this.scratch.Length > 0;
            data.Players = this.scratch;
        }
    }
}
