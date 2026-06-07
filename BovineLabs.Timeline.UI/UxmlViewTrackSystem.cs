namespace BovineLabs.Timeline.UI
{
    using System.Collections.Generic;
    using BovineLabs.Anchor;
    using BovineLabs.Anchor.Services;
    using BovineLabs.Core;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine.UIElements;

    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial class UxmlViewTrackSystem : SystemBase
    {
        private Dictionary<Entity, VisualElement> activeViews;
        private EntityQuery newlyActiveQuery;
        private EntityQuery activeCleanupQuery;

        protected override void OnCreate()
        {
            this.activeViews = new Dictionary<Entity, VisualElement>();

            this.newlyActiveQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UxmlViewData, TimelineActive, ClipActive>()
                .WithNone<UxmlViewCleanup>()
                .Build(this);

            this.activeCleanupQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UxmlViewCleanup>()
                .Build(this);
        }

        protected override void OnUpdate()
        {
            var app = AnchorApp.Current;
            if (app?.RootVisualElement == null)
            {
                return;
            }

            if (app.Services.GetService(typeof(IUXMLService)) is not IUXMLService uxmlService)
            {
                return;
            }

            this.ProcessInactiveViews();
            this.ProcessNewlyActiveViews(app.RootVisualElement, uxmlService);
        }

        protected override void OnDestroy()
        {
            foreach (var view in this.activeViews.Values)
            {
                view?.RemoveFromHierarchy();
            }

            this.activeViews.Clear();
        }

        private void ProcessInactiveViews()
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = this.activeCleanupQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (this.ShouldKeepViewActive(entity))
                {
                    continue;
                }

                this.DetachAndRemoveView(entity);
                ecb.RemoveComponent<UxmlViewCleanup>(entity);
            }

            ecb.Playback(this.EntityManager);
        }

        private void ProcessNewlyActiveViews(VisualElement root, IUXMLService uxmlService)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = this.newlyActiveQuery.ToEntityArray(Allocator.Temp);
            var dataArray = this.newlyActiveQuery.ToComponentDataArray<UxmlViewData>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var data = dataArray[i];

                this.InstantiateAndAttachView(entity, data, root, uxmlService);
                ecb.AddComponent<UxmlViewCleanup>(entity);
            }

            ecb.Playback(this.EntityManager);
        }

        private bool ShouldKeepViewActive(Entity entity)
        {
            return this.EntityManager.HasComponent<UxmlViewData>(entity) &&
                   this.EntityManager.HasComponent<TimelineActive>(entity) &&
                   this.EntityManager.IsComponentEnabled<TimelineActive>(entity) &&
                   this.EntityManager.HasComponent<ClipActive>(entity) &&
                   this.EntityManager.IsComponentEnabled<ClipActive>(entity);
        }

        private void InstantiateAndAttachView(Entity entity, UxmlViewData data, VisualElement root, IUXMLService uxmlService)
        {
            var uxmlKey = data.UxmlKey.ToString();
            var view = uxmlService.Instantiate(uxmlKey);

            if (view == null)
            {
                BLGlobalLogger.LogWarningString($"UxmlViewTrackSystem: Failed to instantiate UXML with key '{uxmlKey}'.");
                return;
            }

            this.AttachView(view, data, root);
            this.activeViews[entity] = view;
        }

        private void AttachView(VisualElement view, UxmlViewData data, VisualElement root)
        {
            if (data.TargetId.IsEmpty)
            {
                root.Add(view);
                return;
            }

            var targetId = data.TargetId.ToString();
            var target = root.Q(targetId);

            if (target == null)
            {
                BLGlobalLogger.LogWarningString($"UxmlViewTrackSystem: Target element with id '{targetId}' not found. Falling back to root.");
                root.Add(view);
                return;
            }

            this.ExecuteAttachmentStrategy(view, data.Mode, root, target);
        }

        private void ExecuteAttachmentStrategy(VisualElement view, UxmlAttachmentMode mode, VisualElement root, VisualElement target)
        {
            switch (mode)
            {
                case UxmlAttachmentMode.AppendToElement:
                    target.Add(view);
                    break;

                case UxmlAttachmentMode.InsertBeforeElement:
                    if (target.parent != null)
                    {
                        target.parent.Insert(target.parent.IndexOf(target), view);
                    }
                    else
                    {
                        root.Add(view);
                    }
                    break;

                case UxmlAttachmentMode.InsertAfterElement:
                    if (target.parent != null)
                    {
                        target.parent.Insert(target.parent.IndexOf(target) + 1, view);
                    }
                    else
                    {
                        root.Add(view);
                    }
                    break;

                case UxmlAttachmentMode.AppendToRoot:
                default:
                    root.Add(view);
                    break;
            }
        }

        private void DetachAndRemoveView(Entity entity)
        {
            if (this.activeViews.TryGetValue(entity, out var view))
            {
                view?.RemoveFromHierarchy();
                this.activeViews.Remove(entity);
            }
        }
    }
}
