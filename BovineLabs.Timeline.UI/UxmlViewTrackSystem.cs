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
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public sealed partial class UxmlViewTrackSystem
        : ReversibleEffectSystem<UxmlViewData, VisualElement, UxmlViewCleanup>
    {
        private IUXMLService uxml;

        protected override bool Ready(VisualElement root)
        {
            uxml = AnchorApp.Current.Services.GetService(typeof(IUXMLService)) as IUXMLService;
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

        private static void Attach(VisualElement view, in UxmlViewData data, VisualElement root)
        {
            var target = data.TargetId.IsEmpty ? null : root.Q(data.TargetId.ToString());
            if (target == null)
            {
                if (!data.TargetId.IsEmpty)
                    BLGlobalLogger.LogWarningString(
                        $"UxmlView: TargetId '{data.TargetId.ToString()}' not found under root; attaching to root.");

                root.Add(view);
                return;
            }

            switch (data.Mode)
            {
                case UxmlAttachmentMode.AppendToElement:
                    target.Add(view);
                    break;
                case UxmlAttachmentMode.InsertBeforeElement when target.parent != null:
                    target.parent.Insert(target.parent.IndexOf(target), view);
                    break;
                case UxmlAttachmentMode.InsertAfterElement when target.parent != null:
                    target.parent.Insert(target.parent.IndexOf(target) + 1, view);
                    break;
                default:
                    root.Add(view);
                    break;
            }
        }
    }
}