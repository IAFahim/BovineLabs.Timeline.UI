using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    [BurstCompile]
    public static class NumberFold
    {
        public static void Accumulate(ref int folded, ref bool visible, int value)
        {
            visible = true;
            folded = math.max(folded, value);
        }
    }
}
