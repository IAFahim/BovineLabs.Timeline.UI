namespace BovineLabs.Timeline.UI.Data
{
    using Unity.Collections;
    using Unity.Entities;

    public struct UITextRevealCleanup : ICleanupComponentData
    {
        public FixedString512Bytes OriginalText;
    }
}
