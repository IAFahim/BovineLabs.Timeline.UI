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
            uiHelper = new UIHelper<EssenceUIViewModel, EssenceUIViewModel.Data>(
                ref state, ComponentType.ReadOnly<ClipStat>());

            statScratch = new NativeList<EssenceUIViewModel.Data.StatRow>(Allocator.Persistent);
            intrinsicScratch = new NativeList<EssenceUIViewModel.Data.IntrinsicRow>(Allocator.Persistent);
            eventScratch = new NativeList<EssenceUIViewModel.Data.EventRow>(Allocator.Persistent);

            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            sourcesLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            linksLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);

            state.RequireForUpdate<ControllableRegistry>();
        }

        public void OnDestroy(ref SystemState state)
        {
            statScratch.Dispose();
            intrinsicScratch.Dispose();
            eventScratch.Dispose();
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
            statScratch.Clear();
            intrinsicScratch.Clear();
            eventScratch.Clear();

            var dt = SystemAPI.Time.DeltaTime;
            var statsLookup = SystemAPI.GetBufferLookup<Stat>(true);
            var intrinsicsLookup = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var eventsLookup = SystemAPI.GetBufferLookup<ConditionEvent>(true);

            targetsLookup.Update(ref state);
            sourcesLookup.Update(ref state);
            linksLookup.Update(ref state);
            var players = SystemAPI.GetSingleton<ControllableRegistry>();

            state.Dependency.Complete();

            foreach (var staleEvents in SystemAPI.Query<DynamicBuffer<ActiveUIEvent>>().WithDisabled<ClipActive>())
                if (staleEvents.Length > 0)
                    staleEvents.Clear();

            var visible = false;

            foreach (var (clipStats, clipIntrinsics, clipEvents, _activeEvents, trackBinding, source) in SystemAPI
                         .Query<DynamicBuffer<ClipStat>, DynamicBuffer<ClipIntrinsic>, DynamicBuffer<ClipEvent>,
                             DynamicBuffer<ActiveUIEvent>, RefRO<TrackBinding>, RefRO<UISource>>()
                         .WithAll<TimelineActive, ClipActive>())
            {
                if (!UISourceResolver.TryResolve(source.ValueRO, trackBinding.ValueRO.Value, players,
                        targetsLookup, sourcesLookup, linksLookup, out var player))
                    continue;

                visible = true;
                var playerIndex = player.Index;
                var activeEvents = _activeEvents;
                var hasStats = statsLookup.TryGetBuffer(player, out var stats);

                if (hasStats)
                    CollectStats(clipStats, stats, playerIndex, ref statScratch);

                if (intrinsicsLookup.TryGetBuffer(player, out var intrinsics))
                    CollectIntrinsics(clipIntrinsics, intrinsics, hasStats, stats, playerIndex, ref intrinsicScratch);

                DecayActiveEvents(activeEvents, dt);

                if (eventsLookup.TryGetBuffer(player, out var conditionEvents))
                    RefreshActiveEvents(clipEvents, conditionEvents, activeEvents);

                foreach (var active in activeEvents)
                {
                    if (ContainsEvent(eventScratch, playerIndex, active.Key.Value))
                        continue;

                    eventScratch.Add(new EssenceUIViewModel.Data.EventRow
                    {
                        Player = playerIndex,
                        Key = active.Key.Value,
                        RawName = active.Name,
                        Amount = active.Value,
                        TimeRemaining = active.TimeRemaining,
                        Duration = active.Duration
                    });
                }
            }

            ref var data = ref uiHelper.Binding;
            data.IsVisible = visible;
            data.Stats = statScratch;
            data.Intrinsics = intrinsicScratch;
            data.Events = eventScratch;
        }

        private static void CollectStats(
            DynamicBuffer<ClipStat> clipStats, DynamicBuffer<Stat> stats, int playerIndex,
            ref NativeList<EssenceUIViewModel.Data.StatRow> scratch)
        {
            var statMap = stats.AsMap();
            foreach (var clipStat in clipStats)
            {
                if (!statMap.TryGetValue(clipStat.Key, out var stat))
                    continue;

                if (ContainsStat(scratch, playerIndex, clipStat.Key.Value))
                    continue;

                scratch.Add(new EssenceUIViewModel.Data.StatRow
                {
                    Player = playerIndex,
                    Key = clipStat.Key.Value,
                    RawName = clipStat.Name,
                    Added = stat.Added,
                    Multi = stat.Multi,
                    Scaled = stat.ValueFloat
                });
            }
        }

        private static void CollectIntrinsics(
            DynamicBuffer<ClipIntrinsic> clipIntrinsics, DynamicBuffer<Intrinsic> intrinsics,
            bool hasStats, DynamicBuffer<Stat> stats, int playerIndex,
            ref NativeList<EssenceUIViewModel.Data.IntrinsicRow> scratch)
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
                    if (clipIntrinsic.MinStat.Value != 0 &&
                        statMap.TryGetValue(clipIntrinsic.MinStat, out var minStat))
                        min = (int)math.floor(minStat.Value);
                    if (clipIntrinsic.MaxStat.Value != 0 &&
                        statMap.TryGetValue(clipIntrinsic.MaxStat, out var maxStat))
                        max = (int)math.floor(maxStat.Value);
                }

                if (ContainsIntrinsic(scratch, playerIndex, clipIntrinsic.Key.Value))
                    continue;

                scratch.Add(new EssenceUIViewModel.Data.IntrinsicRow
                {
                    Player = playerIndex,
                    Key = clipIntrinsic.Key.Value,
                    RawName = clipIntrinsic.Name,
                    Current = current,
                    Min = min,
                    Max = max
                });
            }
        }

        private static void DecayActiveEvents(DynamicBuffer<ActiveUIEvent> activeEvents, float dt)
        {
            for (var i = activeEvents.Length - 1; i >= 0; i--)
            {
                var active = activeEvents[i];
                active.TimeRemaining -= dt;
                if (active.TimeRemaining <= 0f)
                    activeEvents.RemoveAtSwapBack(i);
                else
                    activeEvents[i] = active;
            }
        }

        private static void RefreshActiveEvents(
            DynamicBuffer<ClipEvent> clipEvents, DynamicBuffer<ConditionEvent> conditionEvents,
            DynamicBuffer<ActiveUIEvent> activeEvents)
        {
            var eventMap = conditionEvents.AsMap();
            foreach (var clipEvent in clipEvents)
            {
                if (!eventMap.TryGetValue(clipEvent.Key, out var amount))
                    continue;

                if (TryRefreshExisting(activeEvents, clipEvent, amount))
                    continue;

                activeEvents.Add(new ActiveUIEvent
                {
                    Key = clipEvent.Key,
                    Name = clipEvent.Name,
                    Value = amount,
                    TimeRemaining = clipEvent.Duration,
                    Duration = clipEvent.Duration
                });
            }
        }

        private static bool TryRefreshExisting(
            DynamicBuffer<ActiveUIEvent> activeEvents, ClipEvent clipEvent, int amount)
        {
            for (var i = 0; i < activeEvents.Length; i++)
                if (activeEvents[i].Key.Equals(clipEvent.Key))
                {
                    var ev = activeEvents[i];
                    ev.Value = amount;
                    ev.TimeRemaining = clipEvent.Duration;
                    ev.Duration = clipEvent.Duration;
                    activeEvents[i] = ev;
                    return true;
                }

            return false;
        }

        private static bool ContainsStat(
            NativeList<EssenceUIViewModel.Data.StatRow> scratch, int player, ushort key)
        {
            for (var i = 0; i < scratch.Length; i++)
                if (scratch[i].Player == player && scratch[i].Key == key)
                    return true;

            return false;
        }

        private static bool ContainsIntrinsic(
            NativeList<EssenceUIViewModel.Data.IntrinsicRow> scratch, int player, ushort key)
        {
            for (var i = 0; i < scratch.Length; i++)
                if (scratch[i].Player == player && scratch[i].Key == key)
                    return true;

            return false;
        }

        private static bool ContainsEvent(
            NativeList<EssenceUIViewModel.Data.EventRow> scratch, int player, int key)
        {
            for (var i = 0; i < scratch.Length; i++)
                if (scratch[i].Player == player && scratch[i].Key == key)
                    return true;

            return false;
        }
    }
}