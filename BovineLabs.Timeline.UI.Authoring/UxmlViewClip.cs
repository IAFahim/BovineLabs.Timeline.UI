namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using BovineLabs.Timeline.Authoring;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Entities;
    using UnityEngine.Timeline;

    [Serializable]
    public class UxmlViewClip : DOTSClip, ITimelineClipAsset
    {
        public string UxmlKey;
        public string TargetId;
        public UxmlAttachmentMode Mode = UxmlAttachmentMode.AppendToRoot;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new UxmlViewData
            {
                UxmlKey = this.UxmlKey,
                TargetId = this.TargetId,
                Mode = this.Mode
            });

            base.Bake(clipEntity, context);
        }
    }
}
