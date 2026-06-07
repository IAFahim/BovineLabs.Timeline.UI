namespace BovineLabs.Timeline.UI.Data
{
    using Unity.Collections;
    using Unity.Entities;

    public struct UssClassData : IComponentData
    {
        public FixedString64Bytes TargetId;
        public FixedString64Bytes ClassName;
    }
}
