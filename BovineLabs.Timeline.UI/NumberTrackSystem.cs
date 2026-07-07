using BovineLabs.Anchor;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.UI.Data;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.Burst;
using Unity.Entities;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct NumberTrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<NumberViewModel, NumberViewModel.Data> uiHelper;

        public void OnCreate(ref SystemState state)
        {
            uiHelper = new UIHelper<NumberViewModel, NumberViewModel.Data>(
                ref state, ComponentType.ReadOnly<NumberComponent>());
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
            var visible = false;
            var folded = int.MinValue;

            foreach (var number in SystemAPI.Query<RefRO<NumberComponent>>().WithAll<TimelineActive, ClipActive>())
            {
                NumberFold.Accumulate(ref folded, ref visible, number.ValueRO.Value);
            }

            ref var data = ref uiHelper.Binding;
            data.IsVisible = visible;
            if (visible) data.Number = folded;
        }
    }
}