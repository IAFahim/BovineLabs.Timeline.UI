using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    [BurstCompile]
    public static class UIFraction
    {
        public static float Saturated(float numerator, float denominator)
        {
            return denominator > 0f ? math.saturate(numerator / denominator) : 0f;
        }
    }
}
