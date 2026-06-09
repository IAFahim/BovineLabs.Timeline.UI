using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct UxmlViewData : IComponentData
    {
        public FixedString64Bytes UxmlKey;
        public FixedString64Bytes TargetId;
        public UxmlAttachmentMode Mode;
    }
}