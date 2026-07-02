using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
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

            inverse = new CapturedText(target, target.text, data.Text.ToString());
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

            // Use the string captured once at TryApply — data.Text.ToString() every frame was steady GC pressure.
            // ponytail: the Substring below still allocs per frame; skipping it on unchanged count needs a mutable
            // last-count written back through the base outstanding map — the per-frame ToString was the dominant cost.
            var full = inverse.Full;

            var visible = TextReveal.RevealedCount(full.Length, transform.Start.Value, transform.End.Value,
                transform.Scale, transform.ClipIn.Value, time.Value.Value, data.Mode == UITextRevealMode.Instant);
            visible = TextReveal.BumpHighSurrogate(full, visible);

            inverse.Element.text = full.Substring(0, visible);
        }

        public readonly struct CapturedText
        {
            public readonly TextElement Element;
            public readonly string Original;
            public readonly string Full;

            public CapturedText(TextElement element, string original, string full)
            {
                Element = element;
                Original = original;
                Full = full;
            }
        }
    }
}