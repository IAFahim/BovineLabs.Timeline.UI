using BovineLabs.Anchor;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.ObjectManagement;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Essence;
using BovineLabs.Timeline.PlayerInputs.Data;
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
        WorldSystemFilterFlags.Presentation)]
    public partial struct EssenceUITrackSystem : ISystem, ISystemStartStop
    {
        private UIHelper<EssenceUIViewModel, EssenceUIViewModel.Data> uiHelper;
        private NativeList<EssenceUIViewModel.Data.StatRow> statScratch;
        private NativeList<EssenceUIViewModel.Data.IntrinsicRow> intrinsicScratch;
        private NativeList<EssenceUIViewModel.Data.EventRow> eventScratch;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> sourcesLookup;
        private UnsafeComponentLookup<PlayerId> playerIdLookup;
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
            playerIdLookup = state.GetUnsafeComponentLookup<PlayerId>(true);
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

            // TODO.md item 13 (clock policy — RESOLVED): toasts decay on the UNSCALED presentation clock published
            // by UIUnscaledClockSystem, so WorldTimeScale bullet-time no longer stretches a 2 s toast to 20 s and
            // game pause freezes it (the clock publishes 0 while PauseGame is active). Fallback: the old clamped
            // scaled step for worlds without the clock system (tests); either path is hitch-clamped.
            var decayDt = SystemAPI.TryGetSingleton<UIUnscaledTime>(out var uiTime)
                ? uiTime.DeltaTime
                : math.min(SystemAPI.Time.DeltaTime, UIClock.MaxStep);

            var statsLookup = SystemAPI.GetBufferLookup<Stat>(true);
            var intrinsicsLookup = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var eventsLookup = SystemAPI.GetBufferLookup<ConditionEvent>(true);

            targetsLookup.Update(ref state);
            sourcesLookup.Update(ref state);
            playerIdLookup.Update(ref state);
            linksLookup.Update(ref state);
            var players = SystemAPI.GetSingleton<ControllableRegistry>();

            // Main-thread UI reads below need these buffers/components settled. Narrow-complete each
            // read type instead of a blanket state.Dependency.Complete() so unrelated worker jobs keep
            // running (TODO.md item 11). Every lookup read further down MUST appear in this list:
            //   Stat / Intrinsic / ConditionEvent   -> per-player Essence values (statsLookup/intrinsicsLookup/eventsLookup)
            //   Targets / EntityLinkSource / EntityLinkEntry -> UISourceResolver.TryResolve (targets/sources/links)
            //   PlayerId                              -> row "Player" display value (playerIdLookup)
            state.EntityManager.CompleteDependencyBeforeRO<Stat>();
            state.EntityManager.CompleteDependencyBeforeRO<Intrinsic>();
            state.EntityManager.CompleteDependencyBeforeRO<ConditionEvent>();
            state.EntityManager.CompleteDependencyBeforeRO<Targets>();
            state.EntityManager.CompleteDependencyBeforeRO<EntityLinkSource>();
            state.EntityManager.CompleteDependencyBeforeRO<EntityLinkEntry>();
            state.EntityManager.CompleteDependencyBeforeRO<PlayerId>();

            // Stale-clear: a clip that is no longer showing must drop its accumulated toasts so a
            // re-activation starts clean. A clip stops showing when EITHER ClipActive OR TimelineActive
            // is disabled (e.g. director destroyed the same frame without disabling ClipActive), so we
            // sweep both. The Length>0 guard makes the second pass a no-op on a buffer the first already
            // cleared, so a doubly-disabled clip is never cleared twice (TODO.md item 18).
            foreach (var staleEvents in SystemAPI.Query<DynamicBuffer<ActiveUIEvent>>().WithDisabled<ClipActive>())
                if (staleEvents.Length > 0)
                    staleEvents.Clear();

            foreach (var staleEvents in SystemAPI.Query<DynamicBuffer<ActiveUIEvent>>().WithDisabled<TimelineActive>())
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

                // "Player" display value: prefer the resolved entity's stable PlayerId over the raw
                // entity index (index churns on respawn/pooling and reads as a misleading "P7").
                // Non-player sources (no PlayerId) fall back to the entity index (TODO.md item 18).
                var playerIndex = playerIdLookup.TryGetComponent(player, out var playerId)
                    ? playerId.Value
                    : player.Index;

                var activeEvents = _activeEvents;

                // Resolution can change mid-clip (link retarget). Any toast captured against a
                // different source is stale — drop it so it is never re-labelled onto the new entity.
                DropStaleSourceEvents(activeEvents, player);

                var hasStats = statsLookup.TryGetBuffer(player, out var stats);

                if (hasStats)
                    CollectStats(clipStats, stats, playerIndex, ref statScratch);

                if (intrinsicsLookup.TryGetBuffer(player, out var intrinsics))
                    CollectIntrinsics(clipIntrinsics, intrinsics, hasStats, stats, playerIndex, ref intrinsicScratch);

                DecayActiveEvents(activeEvents, decayDt);

                if (eventsLookup.TryGetBuffer(player, out var conditionEvents))
                    RefreshActiveEvents(clipEvents, conditionEvents, activeEvents, player);

                foreach (var active in activeEvents)
                {
                    if (ContainsEvent(eventScratch, playerIndex, new BLId(active.Key.Value)))
                        continue;

                    eventScratch.Add(new EssenceUIViewModel.Data.EventRow
                    {
                        Player = playerIndex,
                        Key = new BLId(active.Key.Value),
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

                if (ContainsStat(scratch, playerIndex, new BLId(clipStat.Key.Value)))
                    continue;

                scratch.Add(new EssenceUIViewModel.Data.StatRow
                {
                    Player = playerIndex,
                    Key = new BLId(clipStat.Key.Value),
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
                if (ContainsIntrinsic(scratch, playerIndex, new BLId(clipIntrinsic.Key.Value)))
                    continue;

                var current = intrinsicMap.TryGetValue(clipIntrinsic.Key, out var value) ? value : 0;

                var hasMinStat = TryResolveStat(in statMap, hasStats, clipIntrinsic.MinStat, out var minStatValue);
                var hasMaxStat = TryResolveStat(in statMap, hasStats, clipIntrinsic.MaxStat, out var maxStatValue);

                EssenceUIBounds.ResolveIntrinsicBounds(clipIntrinsic.Min, clipIntrinsic.Max,
                    hasMinStat, minStatValue, hasMaxStat, maxStatValue, out var min, out var max);

                scratch.Add(new EssenceUIViewModel.Data.IntrinsicRow
                {
                    Player = playerIndex,
                    Key = new BLId(clipIntrinsic.Key.Value),
                    RawName = clipIntrinsic.Name,
                    Current = current,
                    Min = min,
                    Max = max
                });
            }
        }

        private static bool TryResolveStat(
            in DynamicHashMap<StatKey, StatValue> statMap, bool hasStats, StatKey key, out float value)
        {
            if (hasStats && !key.Value.IsNull() && statMap.TryGetValue(key, out var stat))
            {
                value = stat.Value;
                return true;
            }

            value = 0f;
            return false;
        }

        private static void DecayActiveEvents(DynamicBuffer<ActiveUIEvent> activeEvents, float dt)
        {
            for (var i = activeEvents.Length - 1; i >= 0; i--)
            {
                var active = activeEvents[i];
                if (EssenceUIDecay.TryDecay(active.TimeRemaining, dt, out var next))
                {
                    activeEvents.RemoveAtSwapBack(i);
                }
                else
                {
                    active.TimeRemaining = next;
                    activeEvents[i] = active;
                }
            }
        }

        // Drops toasts whose captured source no longer matches the currently resolved entity.
        private static void DropStaleSourceEvents(DynamicBuffer<ActiveUIEvent> activeEvents, Entity source)
        {
            for (var i = activeEvents.Length - 1; i >= 0; i--)
                if (activeEvents[i].Source != source)
                    activeEvents.RemoveAtSwapBack(i);
        }

        private static void RefreshActiveEvents(
            DynamicBuffer<ClipEvent> clipEvents, DynamicBuffer<ConditionEvent> conditionEvents,
            DynamicBuffer<ActiveUIEvent> activeEvents, Entity source)
        {
            var eventMap = conditionEvents.AsMap();
            foreach (var clipEvent in clipEvents)
            {
                if (!eventMap.TryGetValue(clipEvent.Key, out var amountPayload))
                    continue;

                var amount = amountPayload.Read<int>();

                if (TryRefreshExisting(activeEvents, clipEvent, amount, source))
                    continue;

                activeEvents.Add(new ActiveUIEvent
                {
                    Key = clipEvent.Key,
                    Name = clipEvent.Name,
                    Value = amount,
                    TimeRemaining = clipEvent.Duration,
                    Duration = clipEvent.Duration,
                    Source = source
                });
            }
        }

        private static bool TryRefreshExisting(
            DynamicBuffer<ActiveUIEvent> activeEvents, ClipEvent clipEvent, int amount, Entity source)
        {
            for (var i = 0; i < activeEvents.Length; i++)
                if (activeEvents[i].Key.Equals(clipEvent.Key))
                {
                    var ev = activeEvents[i];
                    ev.Value = amount;
                    ev.TimeRemaining = clipEvent.Duration;
                    ev.Duration = clipEvent.Duration;
                    ev.Source = source;
                    activeEvents[i] = ev;
                    return true;
                }

            return false;
        }

        private static bool ContainsStat(
            NativeList<EssenceUIViewModel.Data.StatRow> scratch, int player, BLId key)
        {
            for (var i = 0; i < scratch.Length; i++)
                if (scratch[i].Player == player && scratch[i].Key == key)
                    return true;

            return false;
        }

        private static bool ContainsIntrinsic(
            NativeList<EssenceUIViewModel.Data.IntrinsicRow> scratch, int player, BLId key)
        {
            for (var i = 0; i < scratch.Length; i++)
                if (scratch[i].Player == player && scratch[i].Key == key)
                    return true;

            return false;
        }

        private static bool ContainsEvent(
            NativeList<EssenceUIViewModel.Data.EventRow> scratch, int player, BLId key)
        {
            for (var i = 0; i < scratch.Length; i++)
                if (scratch[i].Player == player && scratch[i].Key == key)
                    return true;

            return false;
        }
    }
}