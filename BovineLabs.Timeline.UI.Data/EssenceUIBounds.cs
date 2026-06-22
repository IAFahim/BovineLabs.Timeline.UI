using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    [BurstCompile]
    public static class EssenceUIBounds
    {
        public static void ResolveIntrinsicBounds(
            int clipMin, int clipMax,
            bool hasMinStat, float minStatValue,
            bool hasMaxStat, float maxStatValue,
            out int min, out int max)
        {
            min = hasMinStat ? (int)math.floor(minStatValue) : clipMin;
            max = hasMaxStat ? (int)math.floor(maxStatValue) : clipMax;
        }
    }
}
