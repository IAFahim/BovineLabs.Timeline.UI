using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>The shared easing vocabulary. ONE enum + ONE pure evaluator so the HUD (fed as a Func into UITK
    /// ValueAnimation) and the world bar (called in Burst) use the mathematically identical curve and cannot drift.
    /// Mirrors the UITK Easing.* set available in this Unity version (no Quart/Quint/Expo).</summary>
    public enum EaseId : byte
    {
        Linear = 0,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce,
    }

    /// <summary>Pure, Burst-safe easing. <see cref="Eval"/> is used directly by the world (Burst) and wrapped in a
    /// Func for the HUD's ValueAnimation.Ease, guaranteeing one curve across both stacks.</summary>
    [BurstCompile]
    public static class VexEase
    {
        private const float Pi = math.PI;

        /// <summary>A managed Func wrapper for UITK ValueAnimation.Ease — same curve as the Burst <see cref="Eval"/>.</summary>
        public static System.Func<float, float> Get(EaseId id) => t => Eval(id, t);

        public static float Eval(EaseId id, float t)
        {
            t = math.saturate(t);
            switch (id)
            {
                case EaseId.InSine: return 1f - math.cos((t * Pi) / 2f);
                case EaseId.OutSine: return math.sin((t * Pi) / 2f);
                case EaseId.InOutSine: return -(math.cos(Pi * t) - 1f) / 2f;

                case EaseId.InQuad: return t * t;
                case EaseId.OutQuad: return 1f - ((1f - t) * (1f - t));
                case EaseId.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - (Pow2(-2f * t + 2f) / 2f);

                case EaseId.InCubic: return t * t * t;
                case EaseId.OutCubic: return 1f - Pow3(1f - t);
                case EaseId.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - (Pow3(-2f * t + 2f) / 2f);

                case EaseId.InCirc: return 1f - math.sqrt(1f - (t * t));
                case EaseId.OutCirc: return math.sqrt(1f - Pow2(t - 1f));
                case EaseId.InOutCirc:
                    return t < 0.5f
                        ? (1f - math.sqrt(1f - Pow2(2f * t))) / 2f
                        : (math.sqrt(1f - Pow2(-2f * t + 2f)) + 1f) / 2f;

                case EaseId.InBack: { const float c1 = 1.70158f; return ((c1 + 1f) * t * t * t) - (c1 * t * t); }
                case EaseId.OutBack: { const float c1 = 1.70158f; var p = t - 1f; return 1f + ((c1 + 1f) * Pow3(p)) + (c1 * Pow2(p)); }
                case EaseId.InOutBack:
                {
                    const float c2 = 1.70158f * 1.525f;
                    return t < 0.5f
                        ? (Pow2(2f * t) * (((c2 + 1f) * 2f * t) - c2)) / 2f
                        : ((Pow2((2f * t) - 2f) * (((c2 + 1f) * ((t * 2f) - 2f)) + c2)) + 2f) / 2f;
                }

                case EaseId.InElastic: return Elastic(t, true);
                case EaseId.OutElastic: return Elastic(t, false);
                case EaseId.InOutElastic: return ElasticInOut(t);

                case EaseId.InBounce: return 1f - OutBounce(1f - t);
                case EaseId.OutBounce: return OutBounce(t);
                case EaseId.InOutBounce:
                    return t < 0.5f ? (1f - OutBounce(1f - (2f * t))) / 2f : (1f + OutBounce((2f * t) - 1f)) / 2f;

                default: return t; // Linear
            }
        }

        private static float Pow2(float x) => x * x;
        private static float Pow3(float x) => x * x * x;

        private static float Elastic(float t, bool inDir)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = (2f * Pi) / 3f;
            return inDir
                ? -math.pow(2f, (10f * t) - 10f) * math.sin(((t * 10f) - 10.75f) * c4)
                : (math.pow(2f, -10f * t) * math.sin(((t * 10f) - 0.75f) * c4)) + 1f;
        }

        private static float ElasticInOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c5 = (2f * Pi) / 4.5f;
            return t < 0.5f
                ? -(math.pow(2f, (20f * t) - 10f) * math.sin(((20f * t) - 11.125f) * c5)) / 2f
                : ((math.pow(2f, (-20f * t) + 10f) * math.sin(((20f * t) - 11.125f) * c5)) / 2f) + 1f;
        }

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return (n1 * t * t) + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return (n1 * t * t) + 0.9375f; }
            t -= 2.625f / d1;
            return (n1 * t * t) + 0.984375f;
        }
    }
}
