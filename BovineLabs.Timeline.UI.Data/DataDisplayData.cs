using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct IdValue : IBufferElementData
    {
        public int Id;
        public float Value;
    }

    public struct ClipDataId : IBufferElementData
    {
        public int Id;
        public FixedString32Bytes Label;
    }
}