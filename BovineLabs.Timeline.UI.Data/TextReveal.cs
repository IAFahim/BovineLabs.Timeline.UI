using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    public static class TextReveal
    {
        public static int RevealedCount(int length, double start, double end, double scale, double clipIn,
            double localTime, bool instant)
        {
            if (instant)
            {
                return length;
            }

            var duration = (end - start) * scale;
            var elapsed = localTime - clipIn;
            var percent = duration > 0 ? math.clamp(elapsed / duration, 0.0, 1.0) : 1.0;
            return math.clamp((int)math.round(length * percent), 0, length);
        }

        // Kept for back-compat; superseded by BuildRevealOffsets (grapheme + rich-text aware).
        public static int BumpHighSurrogate(ReadOnlySpan<char> text, int visible)
        {
            if (visible <= 0 || visible >= text.Length)
            {
                return visible;
            }

            return char.IsHighSurrogate(text[visible - 1]) ? visible + 1 : visible;
        }

        /// <summary>
        /// Precomputes the source-substring length to display at each revealable step.
        /// Step boundaries are grapheme-cluster-safe (never split a surrogate pair, combining mark, or ZWJ
        /// emoji sequence) and rich-text-tag-safe (a <c>&lt;...&gt;</c> run is atomic and attaches to the
        /// FOLLOWING visible step, so a step's substring never ends inside a tag).
        /// </summary>
        /// <remarks>
        /// The returned array has one entry per revealable step; entry <c>k</c> is the length to pass to
        /// <c>string.Substring(0, len)</c> to show steps <c>0..k</c>. The final entry always equals
        /// <paramref name="text"/>.Length so a full reveal reproduces the source exactly (trailing tags included).
        /// </remarks>
        public static int[] BuildRevealOffsets(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<int>();
            }

            var n = text.Length;

            // 1. Base grapheme boundaries via StringInfo (surrogate pairs + combining marks).
            var starts = new List<int>();
            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                starts.Add(enumerator.ElementIndex);
            }

            starts.Add(n); // sentinel end so starts[e + 1] is always valid

            var offsets = new List<int>();
            var e = 0;

            while (e < starts.Count - 1)
            {
                var elemStart = starts[e];

                // A '<...>' run is atomic and belongs to the next visible step: skip it without emitting an offset.
                if (text[elemStart] == '<')
                {
                    var close = text.IndexOf('>', elemStart);
                    if (close < 0)
                    {
                        // Unterminated tag: consume the remainder atomically; there is no following visible step.
                        break;
                    }

                    e = AdvancePastIndex(starts, e, close);
                    continue;
                }

                // Visible grapheme cluster; coalesce ZWJ joins (👨‍👩‍👧) that StringInfo may leave separate on Mono.
                var clusterEnd = starts[e + 1];
                var ne = e + 1;
                while (ne < starts.Count - 1)
                {
                    const char zwj = '\u200D'; // zero-width joiner
                    var joinedByZwj = text[clusterEnd - 1] == zwj || text[starts[ne]] == zwj;
                    if (!joinedByZwj)
                    {
                        break;
                    }

                    clusterEnd = starts[ne + 1];
                    ne++;
                }

                offsets.Add(clusterEnd);
                e = ne;
            }

            if (offsets.Count > 0)
            {
                // Fold trailing tags into the final visible step so a full reveal never ends on an open/orphan '<'.
                if (offsets[offsets.Count - 1] < n)
                {
                    offsets[offsets.Count - 1] = n;
                }
            }
            else
            {
                // Tag-only (or all-atomic) non-empty text: one step that reveals everything at once.
                offsets.Add(n);
            }

            return offsets.ToArray();
        }

        private static int AdvancePastIndex(List<int> starts, int e, int index)
        {
            while (e < starts.Count - 1 && starts[e] <= index)
            {
                e++;
            }

            return e;
        }
    }
}
