using System.Collections.Generic;
using BovineLabs.Anchor;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// Projects every resolved player onto the minimap. For each player in the <see cref="ControllableRegistry"/> (1..N,
    /// no hardcoded count) it reads the entity's <see cref="LocalToWorld"/>, projects it through the camera resolved by
    /// <see cref="HudCameraProvider"/> (the shared camera today, a per-player camera later — the reference is never
    /// <c>Camera.main</c> inline), and creates/updates a marker element inside <c>minimap-markers</c>. Markers are added
    /// and removed as players appear and leave. Managed <see cref="SystemBase"/> (touches <see cref="AnchorApp.Current"/>);
    /// never Burst.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial class MinimapDriverSystem : SystemBase
    {
        public const string MarkersElementName = "minimap-markers";
        public const string MarkerNamePrefix = "minimap-marker-";

        private ComponentLookup<LocalToWorld> ltwLookup;
        private readonly Dictionary<int, VisualElement> markers = new();
        private VisualElement lastContainer;

        protected override void OnCreate()
        {
            this.ltwLookup = this.GetComponentLookup<LocalToWorld>(true);
            this.RequireForUpdate<ControllableRegistry>();
        }

        protected override void OnUpdate()
        {
            var app = AnchorApp.Current;
            if (app?.RootVisualElement == null)
            {
                return;
            }

            var container = app.RootVisualElement.Q<VisualElement>(MarkersElementName);
            if (container == null)
            {
                // No minimap mounted — drop any markers we still hold so a remount starts clean.
                if (this.markers.Count > 0)
                {
                    this.markers.Clear();
                    this.lastContainer = null;
                }

                return;
            }

            // Panel remount: the old markers belong to a detached tree — forget them and rebuild against the new one.
            if (!ReferenceEquals(container, this.lastContainer))
            {
                this.markers.Clear();
                this.lastContainer = container;
            }

            var registry = SystemAPI.GetSingleton<ControllableRegistry>();
            if (!registry.ByPlayer.IsCreated)
            {
                return;
            }

            this.ltwLookup.Update(this);
            this.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

            // Reused per-frame live set so we can prune markers for players that left.
            var live = new NativeParallelHashSet<int>(registry.ByPlayer.Length, Unity.Collections.Allocator.Temp);

            for (var p = 0; p < registry.ByPlayer.Length; p++)
            {
                var entity = registry.ByPlayer[p];
                if (entity == Entity.Null || !this.ltwLookup.TryGetComponent(entity, out var ltw))
                {
                    continue;
                }

                var cam = HudCameraProvider.Resolve(p);
                if (cam == null)
                {
                    continue;
                }

                var vp = cam.WorldToViewportPoint(ltw.Position);

                var marker = this.GetOrCreateMarker(p, container);

                if (vp.z <= 0f)
                {
                    // Behind the camera — keep the element but hide it.
                    marker.style.display = DisplayStyle.None;
                    live.Add(p);
                    continue;
                }

                marker.style.display = DisplayStyle.Flex;
                marker.style.left = Length.Percent(math.saturate(vp.x) * 100f);
                marker.style.top = Length.Percent((1f - math.saturate(vp.y)) * 100f);
                live.Add(p);
            }

            this.PruneStaleMarkers(live);
            live.Dispose();
        }

        private VisualElement GetOrCreateMarker(int player, VisualElement container)
        {
            if (this.markers.TryGetValue(player, out var marker))
            {
                return marker;
            }

            marker = new VisualElement { name = MarkerNamePrefix + player, pickingMode = PickingMode.Ignore };
            marker.AddToClassList("vex-minimap__marker");
            marker.AddToClassList("vex-minimap__marker--p" + player);
            container.Add(marker);
            this.markers.Add(player, marker);
            return marker;
        }

        private void PruneStaleMarkers(NativeParallelHashSet<int> live)
        {
            using var stale = new NativeList<int>(this.markers.Count, Unity.Collections.Allocator.Temp);
            foreach (var kvp in this.markers)
            {
                if (!live.Contains(kvp.Key))
                {
                    stale.Add(kvp.Key);
                }
            }

            for (var i = 0; i < stale.Length; i++)
            {
                var key = stale[i];
                this.markers[key].RemoveFromHierarchy();
                this.markers.Remove(key);
            }
        }
    }
}
