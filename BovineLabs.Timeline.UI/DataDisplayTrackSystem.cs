using BovineLabs.Anchor;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.UI.Data;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct DataDisplayTrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<DataDisplayViewModel, DataDisplayViewModel.Data> ui;
        private NativeList<DataDisplayViewModel.Data.Row> scratch;

        public void OnCreate(ref SystemState state)
        {
            ui = new UIHelper<DataDisplayViewModel, DataDisplayViewModel.Data>(ref state,
                ComponentType.ReadOnly<ClipDataId>());
            scratch = new NativeList<DataDisplayViewModel.Data.Row>(Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            scratch.Dispose();
        }

        public void OnStartRunning(ref SystemState state)
        {
            ui.Bind();
        }

        public void OnStopRunning(ref SystemState state)
        {
            ui.Unbind();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            scratch.Clear();

            // We read the IdValue buffer lookup on the main thread below. SystemAPI.Query only
            // auto-completes the queried types (ClipDataId, TrackBinding) — not IdValue — so a
            // producer that writes IdValue from a scheduled job would race (or trip the safety
            // system) without this targeted completion. Narrow-complete RO to avoid a full sync.
            state.EntityManager.CompleteDependencyBeforeRO<IdValue>();
            var values = SystemAPI.GetBufferLookup<IdValue>(true);
            var visible = false;

            foreach (var (ids, binding) in SystemAPI
                         .Query<DynamicBuffer<ClipDataId>, RefRO<TrackBinding>>()
                         .WithAll<TimelineActive, ClipActive>())
            {
                visible = true;
                if (!values.TryGetBuffer(binding.ValueRO.Value, out var buffer))
                    continue;

                foreach (var id in ids)
                {
                    var value = IdValueLookup.Resolve(buffer, id.Id);
                    scratch.Add(new DataDisplayViewModel.Data.Row { Id = id.Id, Name = id.Label, Value = value });
                }
            }

            ref var data = ref ui.Binding;
            data.IsVisible = visible;
            data.Rows = scratch;
        }
    }
}