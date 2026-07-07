using BovineLabs.Anchor;
using BovineLabs.Anchor.Services;
using BovineLabs.Core;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public sealed partial class UxmlViewTrackSystem
        : ReversibleEffectSystem<UxmlViewData, VisualElement, UxmlViewCleanup>
    {
        private IUXMLService uxml;
        private AnchorApp resolvedApp;

        protected override bool Ready(VisualElement root)
        {
            // Cache the service; only re-resolve if we never resolved it or the Anchor app instance changed.
            var app = AnchorApp.Current;
            if (uxml == null || !ReferenceEquals(app, resolvedApp))
            {
                resolvedApp = app;
                uxml = app?.Services.GetService(typeof(IUXMLService)) as IUXMLService;
            }

            return uxml != null;
        }

        protected override bool TryApply(VisualElement root, Entity entity, in UxmlViewData data,
            out VisualElement inverse)
        {
            inverse = uxml.Instantiate(data.UxmlKey.ToString());
            if (inverse == null) return false;

            Attach(inverse, data, root);
            return true;
        }

        protected override void Revert(VisualElement inverse)
        {
            inverse?.RemoveFromHierarchy();
        }

        protected override string DescribeFailure(in UxmlViewData data)
        {
            return $"key '{data.UxmlKey.ToString()}' not registered (AnchorSettings ▸ Views).";
        }

        private static void Attach(VisualElement view, in UxmlViewData data, VisualElement root)
        {
            var target = data.TargetId.IsEmpty ? null : root.Q(data.TargetId.ToString());
            if (target == null && !data.TargetId.IsEmpty)
                BLGlobalLogger.LogWarningString(
                    $"UxmlView: TargetId '{data.TargetId.ToString()}' not found under root; attaching to root.");

            var hasTarget = target != null;
            var hasParent = hasTarget && target.parent != null;
            var targetIndex = hasParent ? target.parent.IndexOf(target) : 0;
            var plan = UxmlAttach.PlanAttach(data.Mode, hasTarget, hasParent, targetIndex);

            switch (plan.Op)
            {
                case AttachOp.AppendChild:
                    target.Add(view);
                    break;
                case AttachOp.InsertAt:
                    target.parent.Insert(plan.Index, view);
                    break;
                default:
                    root.Add(view);
                    break;
            }
        }
    }
}