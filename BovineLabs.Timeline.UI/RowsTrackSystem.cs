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
        WorldSystemFilterFlags.Presentation
    )]
    public partial struct RowsTrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<RowsViewModel, RowsViewModel.Data> uiHelper;
        private NativeList<RowsViewModel.Data.Row> scratch;

        public void OnCreate(ref SystemState state)
        {
            uiHelper = new UIHelper<RowsViewModel, RowsViewModel.Data>(ref state,
                ComponentType.ReadOnly<NumberComponent>());

            scratch = new NativeList<RowsViewModel.Data.Row>(Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            scratch.Dispose();
        }

        public void OnStartRunning(ref SystemState state)
        {
            uiHelper.Bind();
        }

        public void OnStopRunning(ref SystemState state)
        {
            uiHelper.Unbind();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            scratch.Clear();

            foreach (var (number, entity) in SystemAPI
                         .Query<RefRO<NumberComponent>>()
                         .WithAll<TimelineActive, ClipActive>()
                         .WithEntityAccess())
                scratch.Add(new RowsViewModel.Data.Row
                {
                    Source = entity,
                    RawLabel = entity.ToFixedString(),
                    RawValue = number.ValueRO.Value
                });

            ref var data = ref uiHelper.Binding;
            data.IsVisible = scratch.Length > 0;
            data.Rows = scratch;
        }
    }
}