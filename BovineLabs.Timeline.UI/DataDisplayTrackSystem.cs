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
        WorldSystemFilterFlags.ServerSimulation |
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
                    var value = 0f;
                    for (var i = 0; i < buffer.Length; i++)
                        if (buffer[i].Id == id.Id)
                        {
                            value = buffer[i].Value;
                            break;
                        }

                    scratch.Add(new DataDisplayViewModel.Data.Row { Id = id.Id, Name = id.Label, Value = value });
                }
            }

            ref var data = ref ui.Binding;
            data.IsVisible = visible;
            data.Rows = scratch;
        }
    }
}