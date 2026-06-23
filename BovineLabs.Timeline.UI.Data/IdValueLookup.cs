using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    [BurstCompile]
    public static class IdValueLookup
    {
        public static unsafe float Resolve(DynamicBuffer<IdValue> buffer, int id)
        {
            return Resolve(new ReadOnlySpan<IdValue>(buffer.GetUnsafeReadOnlyPtr(), buffer.Length), id);
        }

        public static float Resolve(ReadOnlySpan<IdValue> entries, int id)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Id == id)
                {
                    return entries[i].Value;
                }
            }

            return 0f;
        }
    }
}
