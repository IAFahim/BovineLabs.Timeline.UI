using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct UssClassData : IComponentData
    {
        public FixedString64Bytes TargetId;
        public FixedString64Bytes ClassName;
    }
}