using BovineLabs.Anchor;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Essence;
using BovineLabs.Timeline.UI.Data;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
        WorldSystemFilterFlags.Presentation)]
    public partial struct EssenceUITrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<EssenceUIViewModel, EssenceUIViewModel.Data> uiHelper;
        private NativeList<EssenceUIViewModel.Data.StatRow> statScratch;
        private NativeList<EssenceUIViewModel.Data.IntrinsicRow> intrinsicScratch;
        private NativeList<EssenceUIViewModel.Data.EventRow> eventScratch;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> sourcesLookup;
        private UnsafeBufferLookup<EntityLinkEntry> linksLookup;

        public void OnCreate(ref SystemState state)
        {
            this.uiHelper = new UIHelper<EssenceUIViewModel, EssenceUIViewModel.Data>(
                ref state, ComponentType.ReadOnly<ClipStat>());

            this.statScratch = new NativeList<EssenceUIViewModel.Data.StatRow>(Allocator.Persistent);
            this.intrinsicScratch = new NativeList<EssenceUIViewModel.Data.IntrinsicRow>(Allocator.Persistent);
            this.eventScratch = new NativeList<EssenceUIViewModel.Data.EventRow>(Allocator.Persistent);

            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this.sourcesLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            this.linksLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);

            state.RequireForUpdate<ControllableRegistry>();
        }

        public void OnDestroy(ref SystemState state)
        {
            this.statScratch.Dispose();
            this.intrinsicScratch.Dispose();
            this.eventScratch.Dispose();
        }

        public void OnStartRunning(ref SystemState state) => this.uiHelper.Bind();
        public void OnStopRunning(ref SystemState state) => this.uiHelper.Unbind();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            this.statScratch.Clear();
            this.intrinsicScratch.Clear();
            this.eventScratch.Clear();

            var dt = SystemAPI.Time.DeltaTime;
            var statsLookup = SystemAPI.GetBufferLookup<Stat>(true);
            var intrinsicsLookup = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var eventsLookup = SystemAPI.GetBufferLookup<ConditionEvent>(true);

            this.targetsLookup.Update(ref state);
            this.sourcesLookup.Update(ref state);
            this.linksLookup.Update(ref state);
            var players = SystemAPI.GetSingleton<ControllableRegistry>();

            state.Dependency.Complete();

            var visible = false;

            foreach (var (clipStats, clipIntrinsics, clipEvents, _activeEvents, trackBinding, source) in SystemAPI
                .Query<DynamicBuffer<ClipStat>, DynamicBuffer<ClipIntrinsic>, DynamicBuffer<ClipEvent>,
                    DynamicBuffer<ActiveUIEvent>, RefRO<TrackBinding>, RefRO<UISource>>()
                .WithAll<TimelineActive, ClipActive>())
            {
                if (!UISourceResolver.TryResolve(source.ValueRO, trackBinding.ValueRO.Value, players,
                        this.targetsLookup, this.sourcesLookup, this.linksLookup, out var player))
                {
                    continue;
                }

                visible = true;
                var playerIndex = player.Index;
                var activeEvents = _activeEvents;
                var hasStats = statsLookup.TryGetBuffer(player, out var stats);

                if (hasStats)
                {
                    var statMap = stats.AsMap();
                    foreach (var clipStat in clipStats)
                    {
                        if (!statMap.TryGetValue(clipStat.Key, out var stat))
                            continue;

                        this.statScratch.Add(new EssenceUIViewModel.Data.StatRow
                        {
                            Player = playerIndex,
                            Key = (ushort)clipStat.Key.Value,
                            RawName = clipStat.Name,
                            Added = stat.Added,
                            Multi = stat.Multi,
                            Scaled = stat.ValueFloat,
                        });
                    }
                }

                if (intrinsicsLookup.TryGetBuffer(player, out var intrinsics))
                {
                    var intrinsicMap = intrinsics.AsMap();
                    var statMap = hasStats ? stats.AsMap() : default;
                    foreach (var clipIntrinsic in clipIntrinsics)
                    {
                        var current = intrinsicMap.TryGetValue(clipIntrinsic.Key, out var value) ? value : 0;
                        var min = clipIntrinsic.Min;
                        var max = clipIntrinsic.Max;

                        if (hasStats)
                        {
                            if (clipIntrinsic.MinStat.Value != 0 && statMap.TryGetValue(clipIntrinsic.MinStat, out var minStat))
                                min = (int)math.floor(minStat.Value);
                            if (clipIntrinsic.MaxStat.Value != 0 && statMap.TryGetValue(clipIntrinsic.MaxStat, out var maxStat))
                                max = (int)math.floor(maxStat.Value);
                        }

                        this.intrinsicScratch.Add(new EssenceUIViewModel.Data.IntrinsicRow
                        {
                            Player = playerIndex,
                            Key = (ushort)clipIntrinsic.Key.Value,
                            RawName = clipIntrinsic.Name,
                            Current = current,
                            Min = min,
                            Max = max,
                        });
                    }
                }

                // Tick down active event timers
                for (var i = activeEvents.Length - 1; i >= 0; i--)
                {
                    var active = activeEvents[i];
                    active.TimeRemaining -= dt;
                    if (active.TimeRemaining <= 0f)
                        activeEvents.RemoveAtSwapBack(i);
                    else
                        activeEvents[i] = active;
                }

                // Ingest new condition events
                if (eventsLookup.TryGetBuffer(player, out var conditionEvents))
                {
                    var eventMap = conditionEvents.AsMap();
                    foreach (var clipEvent in clipEvents)
                    {
                        if (!eventMap.TryGetValue(clipEvent.Key, out var amount))
                            continue;

                        var refreshed = false;
                        for (var i = 0; i < activeEvents.Length; i++)
                        {
                            if (activeEvents[i].Key.Equals(clipEvent.Key))
                            {
                                var ev = activeEvents[i];
                                ev.Value = amount;
                                ev.TimeRemaining = clipEvent.Duration;
                                ev.Duration = clipEvent.Duration;
                                activeEvents[i] = ev;
                                refreshed = true;
                                break;
                            }
                        }

                        if (!refreshed)
                        {
                            activeEvents.Add(new ActiveUIEvent
                            {
                                Key = clipEvent.Key,
                                Name = clipEvent.Name,
                                Value = amount,
                                TimeRemaining = clipEvent.Duration,
                                Duration = clipEvent.Duration,
                            });
                        }
                    }
                }

                // Emit event rows
                foreach (var active in activeEvents)
                {
                    this.eventScratch.Add(new EssenceUIViewModel.Data.EventRow
                    {
                        Player = playerIndex,
                        Key = (ushort)active.Key.Value,
                        RawName = active.Name,
                        Amount = active.Value,
                        TimeRemaining = active.TimeRemaining,
                        Duration = active.Duration,
                    });
                }
            }

            ref var data = ref this.uiHelper.Binding;
            data.IsVisible = visible;
            data.Stats = this.statScratch;
            data.Intrinsics = this.intrinsicScratch;
            data.Events = this.eventScratch;
        }
    }
}
