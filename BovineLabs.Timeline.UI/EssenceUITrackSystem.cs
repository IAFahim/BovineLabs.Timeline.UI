using BovineLabs.Anchor;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Timeline;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Essence;
using BovineLabs.Timeline.UI.Data;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(TimelineEssenceStatSystem))]
    [UpdateAfter(typeof(TimelineEssenceIntrinsicSystem))]
    [UpdateAfter(typeof(TimelineEssenceEventSystem))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation
    )]
    public partial struct EssenceUITrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<EssenceUIViewModel, EssenceUIViewModel.Data> _uiHelper;

        public void OnCreate(ref SystemState state)
        {
            _uiHelper = new UIHelper<EssenceUIViewModel, EssenceUIViewModel.Data>(ref state, ComponentType.ReadOnly<ClipStat>());
        }

        public void OnStartRunning(ref SystemState state) => _uiHelper.Bind();
        public void OnStopRunning(ref SystemState state) => _uiHelper.Unbind();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var statsLookup = SystemAPI.GetBufferLookup<Stat>(true);
            var intrinsicsLookup = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var conditionEventsLookup = SystemAPI.GetBufferLookup<ConditionEvent>(true);
            var activeUIEventsLookup = SystemAPI.GetBufferLookup<ActiveUIEvent>();

            state.Dependency.Complete();

            bool isVisible = false;
            FixedString4096Bytes dumpText = new FixedString4096Bytes();

            foreach (var (clipStats, clipIntrinsics, clipEvents, _activeEvents, trackBinding, entity) in 
                     SystemAPI.Query<DynamicBuffer<ClipStat>, DynamicBuffer<ClipIntrinsic>, DynamicBuffer<ClipEvent>, DynamicBuffer<ActiveUIEvent>, RefRO<TrackBinding>>().WithEntityAccess()
                     .WithAll<TimelineActive, ClipActive>())
            {
                isVisible = true;
                Entity player = trackBinding.ValueRO.Value;

                var activeEvents = _activeEvents;

                dumpText.Append($"--- PLAYER {player.Index} ---\n");

                if (statsLookup.TryGetBuffer(player, out var stats))
                {
                    foreach (var s in clipStats)
                    {
                        dumpText.Append($"[STAT] {s.Name}: {stats.GetValueFloat(s.Key)}\n");
                    }
                }

                if (intrinsicsLookup.TryGetBuffer(player, out var intrinsics))
                {
                    foreach (var i in clipIntrinsics)
                    {
                        dumpText.Append($"[INT] {i.Name}: {intrinsics.GetValue(i.Key)}\n");
                    }
                }

                for (int e = activeEvents.Length - 1; e >= 0; e--)
                {
                    var ev = activeEvents[e];
                    ev.TimeRemaining -= dt;
                    if (ev.TimeRemaining <= 0)
                        activeEvents.RemoveAtSwapBack(e);
                    else
                        activeEvents[e] = ev; 
                }

                if (conditionEventsLookup.TryGetBuffer(player, out var conditionEvents))
                {
                    var eventMap = conditionEvents.AsMap();
                    foreach (var ce in clipEvents)
                    {
                        if (eventMap.TryGetValue(ce.Key, out int val))
                        {
                            bool found = false;
                            for (int e = 0; e < activeEvents.Length; e++)
                            {
                                if (activeEvents[e].Key.Equals(ce.Key))
                                {
                                    var ev = activeEvents[e];
                                    ev.TimeRemaining = ce.Duration;
                                    ev.Value = val;
                                    activeEvents[e] = ev;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) activeEvents.Add(new ActiveUIEvent { Key = ce.Key, Name = ce.Name, Value = val, TimeRemaining = ce.Duration });
                        }
                    }
                }

                foreach (var ev in activeEvents)
                {
                    dumpText.Append($"[EVENT] {ev.Name}! ({ev.Value})\n");
                }
                
                dumpText.Append("\n");
            }

            ref var data = ref _uiHelper.Binding;
            data.IsVisible = isVisible;
            data.DumpText = dumpText;
        }
    }
}