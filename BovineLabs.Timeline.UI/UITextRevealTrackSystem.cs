namespace BovineLabs.Timeline.UI
{
    using System.Collections.Generic;
    using BovineLabs.Anchor;
    using BovineLabs.Core;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine.UIElements;

    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(UssClassTrackSystem))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial class UITextRevealTrackSystem : SystemBase
    {
        private Dictionary<Entity, TextElement> activeElements;
        private EntityQuery newlyActiveQuery;
        private EntityQuery activeQuery;
        private EntityQuery activeCleanupQuery;

        protected override void OnCreate()
        {
            this.activeElements = new Dictionary<Entity, TextElement>();

            this.newlyActiveQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UITextRevealData, LocalTime, TimeTransform, TimelineActive, ClipActive>()
                .WithNone<UITextRevealCleanup>()
                .Build(this);

            this.activeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UITextRevealData, LocalTime, TimeTransform, TimelineActive, ClipActive, UITextRevealCleanup>()
                .Build(this);

            this.activeCleanupQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UITextRevealCleanup>()
                .Build(this);
        }

        protected override void OnUpdate()
        {
            var app = AnchorApp.Current;
            if (app?.RootVisualElement == null)
            {
                return;
            }

            this.ProcessInactiveElements();
            this.ProcessNewlyActiveElements(app.RootVisualElement);
            this.ProcessActiveElements();
        }

        protected override void OnDestroy()
        {
            this.activeElements.Clear();
        }

        private void ProcessInactiveElements()
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = this.activeCleanupQuery.ToEntityArray(Allocator.Temp);
            var cleanups = this.activeCleanupQuery.ToComponentDataArray<UITextRevealCleanup>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var cleanup = cleanups[i];

                if (this.ShouldKeepElementActive(entity))
                {
                    continue;
                }

                this.RestoreOriginalText(entity, cleanup.OriginalText);
                ecb.RemoveComponent<UITextRevealCleanup>(entity);
            }

            ecb.Playback(this.EntityManager);
        }

        private void ProcessNewlyActiveElements(VisualElement root)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entities = this.newlyActiveQuery.ToEntityArray(Allocator.Temp);
            var dataArray = this.newlyActiveQuery.ToComponentDataArray<UITextRevealData>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var data = dataArray[i];

                var originalText = this.ResolveTargetAndSaveOriginal(entity, data, root);
                ecb.AddComponent(entity, new UITextRevealCleanup { OriginalText = originalText });
            }

            ecb.Playback(this.EntityManager);
        }

        private void ProcessActiveElements()
        {
            var entities = this.activeQuery.ToEntityArray(Allocator.Temp);
            var dataArray = this.activeQuery.ToComponentDataArray<UITextRevealData>(Allocator.Temp);
            var localTimeArray = this.activeQuery.ToComponentDataArray<LocalTime>(Allocator.Temp);
            var transformArray = this.activeQuery.ToComponentDataArray<TimeTransform>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var data = dataArray[i];
                var localTime = localTimeArray[i];
                var transform = transformArray[i];

                if (!this.activeElements.TryGetValue(entity, out var textElement) || textElement == null)
                {
                    continue;
                }

                if (data.Mode == UITextRevealMode.Instant)
                {
                    textElement.text = data.Text.ToString();
                    continue;
                }

                var durationTicks = (transform.End.Value - transform.Start.Value) * transform.Scale;
                var currentTicks = localTime.Value.Value - transform.ClipIn.Value;

                var percent = durationTicks > 0 
                    ? math.clamp(currentTicks / durationTicks, 0.0, 1.0) 
                    : 1.0;

                var textString = data.Text.ToString();
                var visibleChars = (int)math.round(textString.Length * percent);

                textElement.text = textString.Substring(0, visibleChars);
            }
        }

        private bool ShouldKeepElementActive(Entity entity)
        {
            return this.EntityManager.HasComponent<UITextRevealData>(entity) &&
                   this.EntityManager.HasComponent<TimelineActive>(entity) &&
                   this.EntityManager.IsComponentEnabled<TimelineActive>(entity) &&
                   this.EntityManager.HasComponent<ClipActive>(entity) &&
                   this.EntityManager.IsComponentEnabled<ClipActive>(entity);
        }

        private FixedString512Bytes ResolveTargetAndSaveOriginal(Entity entity, UITextRevealData data, VisualElement root)
        {
            var targetId = data.TargetId.ToString();
            var target = string.IsNullOrEmpty(targetId) ? null : root.Q<TextElement>(targetId);

            if (target == null)
            {
                BLGlobalLogger.LogWarningString($"UITextRevealTrackSystem: Target TextElement with id '{targetId}' not found.");
                return default;
            }

            this.activeElements[entity] = target;
            return new FixedString512Bytes(target.text);
        }

        private void RestoreOriginalText(Entity entity, FixedString512Bytes originalText)
        {
            if (this.activeElements.TryGetValue(entity, out var textElement))
            {
                if (textElement != null)
                {
                    textElement.text = originalText.ToString();
                }
                this.activeElements.Remove(entity);
            }
        }
    }
}
