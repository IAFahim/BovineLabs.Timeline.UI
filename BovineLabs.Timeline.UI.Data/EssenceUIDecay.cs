using Unity.Burst;

namespace BovineLabs.Timeline.UI.Data
{
    [BurstCompile]
    public static class EssenceUIDecay
    {
        public static bool TryDecay(float remaining, float dt, out float next)
        {
            next = remaining - dt;
            return next <= 0f;
        }
    }
}
