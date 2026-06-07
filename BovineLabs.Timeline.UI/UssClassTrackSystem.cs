namespace BovineLabs.Timeline.UI
{
    using System.Collections.Generic;
    using BovineLabs.Anchor;
    using BovineLabs.Core;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine.UIElements;

    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(UxmlViewTrackSystem))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial class UssClassTrackSystem : SystemBase
    {
        private Dictionary<Entity, (VisualElement Element, string ClassName)> activeClasses;
        private EntityQuery newlyActiveQuery;
        private EntityQuery activeCleanupQuery;

        protected override void OnCreate()
        {
            this.activeClasses = new Dictionary<Entity, (VisualElement, string)>();

            this.newlyActiveQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UssClassData, TimelineActive, ClipActive>()
                .WithNone<UssClassCleanup>()
                .Build(this);

            this.activeCleanupQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UssClassCleanup>()
                .Build(this);
        }

        protected override void OnUpdate()
        {
            var app = AnchorApp.Current;
            if (app?.RootVisualElement == null)
            {
                return;
            }

            this.ProcessInactiveClasses();
            this.ProcessNewlyActiveClasses(app.RootVisualElement);
        }

        protected override void OnDestroy()
        {
            foreach (var activeClass in this.activeClasses.Values)
            {
                activeClass.Element?.RemoveFromClassList(activeClass.ClassName);
            }

            this.activeClasses.Clear();
        }

        private void ProcessInactiveClasses()
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = this.activeCleanupQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (this.ShouldKeepClassActive(entity))
                {
                    continue;
                }

                this.RemoveClassFromElement(entity);
                ecb.RemoveComponent<UssClassCleanup>(entity);
            }

            ecb.Playback(this.EntityManager);
        }

        private void ProcessNewlyActiveClasses(VisualElement root)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = this.newlyActiveQuery.ToEntityArray(Allocator.Temp);
            var dataArray = this.newlyActiveQuery.ToComponentDataArray<UssClassData>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var data = dataArray[i];

                this.ResolveAndApplyClass(entity, data, root);
                ecb.AddComponent<UssClassCleanup>(entity);
            }

            ecb.Playback(this.EntityManager);
        }

        private bool ShouldKeepClassActive(Entity entity)
        {
            return this.EntityManager.HasComponent<UssClassData>(entity) &&
                   this.EntityManager.HasComponent<TimelineActive>(entity) &&
                   this.EntityManager.IsComponentEnabled<TimelineActive>(entity) &&
                   this.EntityManager.HasComponent<ClipActive>(entity) &&
                   this.EntityManager.IsComponentEnabled<ClipActive>(entity);
        }

        private void ResolveAndApplyClass(Entity entity, UssClassData data, VisualElement root)
        {
            var targetId = data.TargetId.ToString();
            var className = data.ClassName.ToString();
            
            var target = string.IsNullOrEmpty(targetId) ? root : root.Q(targetId);

            if (target == null)
            {
                BLGlobalLogger.LogWarningString($"UssClassTrackSystem: Target element with id '{targetId}' not found.");
                return;
            }

            target.AddToClassList(className);
            this.activeClasses[entity] = (target, className);
        }

        private void RemoveClassFromElement(Entity entity)
        {
            if (this.activeClasses.TryGetValue(entity, out var activeClass))
            {
                activeClass.Element?.RemoveFromClassList(activeClass.ClassName);
                this.activeClasses.Remove(entity);
            }
        }
    }
}
