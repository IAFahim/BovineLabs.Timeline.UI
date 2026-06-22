using System;
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

        public static int BumpHighSurrogate(ReadOnlySpan<char> text, int visible)
        {
            if (visible <= 0 || visible >= text.Length)
            {
                return visible;
            }

            return char.IsHighSurrogate(text[visible - 1]) ? visible + 1 : visible;
        }
    }
}
