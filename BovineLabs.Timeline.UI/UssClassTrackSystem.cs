namespace BovineLabs.Timeline.UI
{
    using BovineLabs.Timeline.Data;
    using Data;
    using Unity.Entities;
    using UnityEngine.UIElements;

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
        public readonly struct AppliedClass
        {
            public readonly VisualElement Element;
            public readonly string ClassName;

            public AppliedClass(VisualElement element, string className)
            {
                Element = element;
                ClassName = className;
            }
        }

        protected override bool TryApply(VisualElement root, Entity entity, in UssClassData data, out AppliedClass inverse)
        {
            var target = data.TargetId.IsEmpty ? root : root.Q(data.TargetId.ToString());
            if (target == null)
            {
                inverse = default;
                return false;
            }

            var className = data.ClassName.ToString();
            target.AddToClassList(className);
            inverse = new AppliedClass(target, className);
            return true;
        }

        protected override void Revert(AppliedClass inverse)
        {
            inverse.Element?.RemoveFromClassList(inverse.ClassName);
        }
    }
}
