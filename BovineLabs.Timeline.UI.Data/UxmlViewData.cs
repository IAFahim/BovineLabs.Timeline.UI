namespace BovineLabs.Timeline.UI.Data
{
    using Unity.Collections;
    using Unity.Entities;

    public struct UxmlViewData : IComponentData
    {
        public FixedString64Bytes UxmlKey;
        public FixedString64Bytes TargetId;
        public UxmlAttachmentMode Mode;
    }
}
