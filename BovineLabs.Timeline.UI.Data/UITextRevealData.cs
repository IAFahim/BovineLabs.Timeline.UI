namespace BovineLabs.Timeline.UI.Data
{
    using Unity.Collections;
    using Unity.Entities;

    public struct UITextRevealData : IComponentData
    {
        public FixedString64Bytes TargetId;
        public FixedString512Bytes Text;
        public UITextRevealMode Mode;
    }
}
