using System.Collections.Generic;
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
        WorldSystemFilterFlags.Presentation)]
    public sealed partial class UITextRevealTrackSystem
        : ReversibleEffectSystem<UITextRevealData, UITextRevealTrackSystem.CapturedText, UITextRevealCleanup>
    {
        // Per-element base-text registry so overlapping reveals share one true original: the first active clip
        // records target.text; later clips reuse it as their Original; the last clip to exit restores and unregisters.
        private readonly Dictionary<TextElement, BaseText> baseTexts = new();

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

            if (!this.baseTexts.TryGetValue(target, out var baseText))
            {
                baseText = new BaseText { Original = target.text, Holders = 0 };
                this.baseTexts.Add(target, baseText);
            }

            baseText.Holders++;

            var full = data.Text.ToString();
            var offsets = TextReveal.BuildRevealOffsets(full);
            inverse = new CapturedText(target, baseText.Original, full, offsets);
            return true;
        }

        protected override void Revert(CapturedText inverse)
        {
            if (inverse?.Element == null)
                return;

            if (this.baseTexts.TryGetValue(inverse.Element, out var baseText))
            {
                baseText.Holders--;
                if (baseText.Holders <= 0)
                {
                    inverse.Element.text = baseText.Original;
                    this.baseTexts.Remove(inverse.Element);
                }

                return;
            }

            // Registry miss (should not happen): fall back to the captured original.
            inverse.Element.text = inverse.Original;
        }

        protected override void Advance(Entity entity, in UITextRevealData data, CapturedText inverse,
            in LocalTime time, in TimeTransform transform)
        {
            if (inverse?.Element == null)
                return;

            var offsets = inverse.RevealOffsets;
            var steps = offsets.Length;
            if (steps == 0)
            {
                // Empty source text — nothing to reveal.
                if (inverse.LastVisible != 0)
                {
                    inverse.Element.text = string.Empty;
                    inverse.LastVisible = 0;
                }

                return;
            }

            var count = TextReveal.RevealedCount(steps, transform.Start.Value, transform.End.Value,
                transform.Scale, transform.ClipIn.Value, time.Value.Value, data.Mode == UITextRevealMode.Instant);

            // Early-out when the revealed step is unchanged: kills the per-frame Substring alloc and text relayout.
            if (count == inverse.LastVisible)
                return;

            inverse.LastVisible = count;

            var length = count <= 0 ? 0 : offsets[count - 1];
            inverse.Element.text = inverse.Full.Substring(0, length);
        }

        protected override string DescribeFailure(in UITextRevealData data)
        {
            return data.TargetId.IsEmpty
                ? "TargetId is empty."
                : $"TargetId '{data.TargetId.ToString()}' not found under root.";
        }

        protected override void OnCleanup()
        {
            this.baseTexts.Clear();
        }

        public sealed class CapturedText
        {
            public readonly TextElement Element;
            public readonly string Original;
            public readonly string Full;
            public readonly int[] RevealOffsets;

            public int LastVisible;

            public CapturedText(TextElement element, string original, string full, int[] revealOffsets)
            {
                this.Element = element;
                this.Original = original;
                this.Full = full;
                this.RevealOffsets = revealOffsets;
                this.LastVisible = -1;
            }
        }

        private sealed class BaseText
        {
            public string Original;
            public int Holders;
        }
    }
}
