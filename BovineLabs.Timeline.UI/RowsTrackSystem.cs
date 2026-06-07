// <copyright file="RowsTrackSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.UI
{
    using BovineLabs.Anchor;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.UI.Data;
    using BovineLabs.Timeline.UI.Data.ViewModel;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;

    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation
    )]
    public partial struct RowsTrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<RowsViewModel, RowsViewModel.Data> uiHelper;
        private NativeList<RowsViewModel.Data.Row> scratch;

        public void OnCreate(ref SystemState state)
        {
            this.uiHelper = new UIHelper<RowsViewModel, RowsViewModel.Data>(ref state,
                ComponentType.ReadOnly<NumberComponent>());

            this.scratch = new NativeList<RowsViewModel.Data.Row>(Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            this.scratch.Dispose();
        }

        public void OnStartRunning(ref SystemState state)
        {
            this.uiHelper.Bind();
        }

        public void OnStopRunning(ref SystemState state)
        {
            this.uiHelper.Unbind();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            this.scratch.Clear();

            foreach (var (number, entity) in SystemAPI
                .Query<RefRO<NumberComponent>>()
                .WithAll<TimelineActive, ClipActive>()
                .WithEntityAccess())
            {
                this.scratch.Add(new RowsViewModel.Data.Row
                {
                    Source = entity,
                    RawLabel = entity.ToFixedString(),
                    RawValue = number.ValueRO.Value,
                });
            }

            ref var data = ref this.uiHelper.Binding;
            data.IsVisible = this.scratch.Length > 0;
            data.Rows = this.scratch;
        }
    }
}
