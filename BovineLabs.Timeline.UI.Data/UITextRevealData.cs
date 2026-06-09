using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    public struct UITextRevealData : IComponentData
    {
        public FixedString64Bytes TargetId;
        public FixedString512Bytes Text;
        public UITextRevealMode Mode;
    }
}