using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(UxmlViewTrackSystem))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public sealed partial class UssClassTrackSystem
        : ReversibleEffectSystem<UssClassData, UssClassTrackSystem.AppliedClass, UssClassCleanup>
    {
        protected override bool TryApply(VisualElement root, Entity entity, in UssClassData data,
            out AppliedClass inverse)
        {
            var target = data.TargetId.IsEmpty ? root : root.Q(data.TargetId.ToString());
            if (target == null)
            {
                inverse = default;
                return false;
            }

            var className = data.ClassName.ToString();
            if (string.IsNullOrEmpty(className)) // AddToClassList("") throws
            {
                inverse = default;
                return false;
            }

            // Only revert to removing the class if the clip actually added it — a class already present on the
            // element (UXML-authored, or held by an overlapping clip) must survive this clip's exit.
            var wasPresent = target.ClassListContains(className);
            target.AddToClassList(className);
            inverse = new AppliedClass(target, className, wasPresent);
            return true;
        }

        protected override void Revert(AppliedClass inverse)
        {
            if (!inverse.WasPresent)
                inverse.Element?.RemoveFromClassList(inverse.ClassName);
        }

        public readonly struct AppliedClass
        {
            public readonly VisualElement Element;
            public readonly string ClassName;
            public readonly bool WasPresent;

            public AppliedClass(VisualElement element, string className, bool wasPresent)
            {
                Element = element;
                ClassName = className;
                WasPresent = wasPresent;
            }
        }
    }
}