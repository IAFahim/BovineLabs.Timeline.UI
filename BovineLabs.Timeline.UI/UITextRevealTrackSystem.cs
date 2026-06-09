using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(UssClassTrackSystem))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.ServerSimulation |
        WorldSystemFilterFlags.Presentation)]
    public sealed partial class UITextRevealTrackSystem
        : ReversibleEffectSystem<UITextRevealData, UITextRevealTrackSystem.CapturedText, UITextRevealCleanup>
    {
        protected override bool Animated => true;

        protected override bool TryApply(VisualElement root, Entity entity, in UITextRevealData data,
            out CapturedText inverse)
        {
            var target = data.TargetId.IsEmpty ? null : root.Q<TextElement>(data.TargetId.ToString());
            if (target == null)
            {
                inverse = default;
                return false;
            }

            inverse = new CapturedText(target, target.text);
            return true;
        }

        protected override void Revert(CapturedText inverse)
        {
            if (inverse.Element != null) inverse.Element.text = inverse.Original;
        }

        protected override void Advance(Entity entity, in UITextRevealData data, CapturedText inverse,
            in LocalTime time, in TimeTransform transform)
        {
            if (inverse.Element == null) return;

            var full = data.Text.ToString();

            if (data.Mode == UITextRevealMode.Instant)
            {
                inverse.Element.text = full;
                return;
            }

            var duration = (transform.End.Value - transform.Start.Value) * transform.Scale;
            var elapsed = time.Value.Value - transform.ClipIn.Value;
            var percent = duration > 0 ? math.clamp(elapsed / duration, 0.0, 1.0) : 1.0;
            var visible = (int)math.round(full.Length * percent);

            inverse.Element.text = full.Substring(0, visible);
        }

        public readonly struct CapturedText
        {
            public readonly TextElement Element;
            public readonly string Original;

            public CapturedText(TextElement element, string original)
            {
                Element = element;
                Original = original;
            }
        }
    }
}