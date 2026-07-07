using System.Collections.Generic;
using BovineLabs.Anchor;
using BovineLabs.Core;
using BovineLabs.Timeline.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    public abstract partial class ReversibleEffectSystem<TData, TInverse, TCleanup> : SystemBase
        where TData : unmanaged, IComponentData
        where TCleanup : unmanaged, ICleanupComponentData
    {
        private readonly Dictionary<Entity, TInverse> outstanding = new();
        private readonly HashSet<Entity> warned = new();
        private readonly HashSet<Entity> warnedRetain = new();
        private readonly List<Entity> warnedScratch = new();
        private EntityQuery animatedQuery;
        private EntityQuery enteredQuery;
        private EntityQuery outstandingQuery;

        protected virtual bool Animated => false;

        protected abstract bool TryApply(VisualElement root, Entity entity, in TData data, out TInverse inverse);

        protected abstract void Revert(TInverse inverse);

        protected virtual bool Ready(VisualElement root)
        {
            return true;
        }

        /// <summary>
        /// Optional per-subclass failure reason used in the once-per-entity warning when <see cref="TryApply"/>
        /// returns false. Return null to keep the generic "unresolved target" message.
        /// </summary>
        protected virtual string DescribeFailure(in TData data)
        {
            return null;
        }

        /// <summary>Called at the end of <see cref="OnDestroy"/> after all outstanding effects are reverted.</summary>
        protected virtual void OnCleanup()
        {
        }

        protected virtual void Advance(Entity entity, in TData data, TInverse inverse, in LocalTime time,
            in TimeTransform transform)
        {
        }

        protected sealed override void OnCreate()
        {
            enteredQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TData, TimelineActive, ClipActive>()
                .WithNone<TCleanup>()
                .Build(this);

            outstandingQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TCleanup>()
                .Build(this);

            if (Animated)
                animatedQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<TData, LocalTime, TimeTransform, TimelineActive, ClipActive, TCleanup>()
                    .Build(this);
        }

        protected sealed override void OnUpdate()
        {
            var root = AnchorApp.Current?.RootVisualElement;
            if (root == null || !Ready(root)) return;
            Exit();
            Enter(root);
            if (Animated) Tick();
        }

        protected sealed override void OnDestroy()
        {
            foreach (var inverse in outstanding.Values) Revert(inverse);
            outstanding.Clear();
            warned.Clear();
            OnCleanup();
        }

        private void Exit()
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = outstandingQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (Live(entity)) continue;
                if (outstanding.Remove(entity, out var inverse)) Revert(inverse);
                ecb.RemoveComponent<TCleanup>(entity);
            }

            ecb.Playback(EntityManager);
        }

        private void Enter(VisualElement root)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = enteredQuery.ToEntityArray(Allocator.Temp);
            var data = enteredQuery.ToComponentDataArray<TData>(Allocator.Temp);

            // Retain only entities still present this frame, without boxing the NativeArray enumerator
            // (IntersectWith takes IEnumerable<T>). Reused scratch collections keep this alloc-free.
            if (warned.Count > 0)
            {
                warnedRetain.Clear();
                foreach (var entity in entities)
                    warnedRetain.Add(entity);

                warnedScratch.Clear();
                foreach (var w in warned)
                    if (!warnedRetain.Contains(w))
                        warnedScratch.Add(w);

                for (var s = 0; s < warnedScratch.Count; s++)
                    warned.Remove(warnedScratch[s]);
            }

            for (var i = 0; i < entities.Length; i++)
            {
                var d = data[i];

                if (TryApply(root, entities[i], in d, out var inverse))
                {
                    outstanding[entities[i]] = inverse;
                    warned.Remove(entities[i]);
                    ecb.AddComponent<TCleanup>(entities[i]);
                }
                else if (warned.Add(entities[i]))
                {
                    var reason = DescribeFailure(in d);
                    BLGlobalLogger.LogWarningString(reason != null
                        ? $"{GetType().Name}: {reason}"
                        : $"{GetType().Name}: unresolved target for {entities[i].ToFixedString()}.");
                }
            }

            ecb.Playback(EntityManager);
        }

        private void Tick()
        {
            var entities = animatedQuery.ToEntityArray(Allocator.Temp);
            var data = animatedQuery.ToComponentDataArray<TData>(Allocator.Temp);
            var time = animatedQuery.ToComponentDataArray<LocalTime>(Allocator.Temp);
            var transform = animatedQuery.ToComponentDataArray<TimeTransform>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
                if (outstanding.TryGetValue(entities[i], out var inverse))
                {
                    var d = data[i];
                    var t = time[i];
                    var tr = transform[i];
                    Advance(entities[i], in d, inverse, in t, in tr);
                }
        }

        private bool Live(Entity entity)
        {
            return EntityManager.HasComponent<TData>(entity)
                   && EntityManager.HasComponent<TimelineActive>(entity)
                   && EntityManager.IsComponentEnabled<TimelineActive>(entity)
                   && EntityManager.HasComponent<ClipActive>(entity)
                   && EntityManager.IsComponentEnabled<ClipActive>(entity);
        }
    }
}