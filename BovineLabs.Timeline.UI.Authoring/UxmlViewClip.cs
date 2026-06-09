using System;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.UI.Authoring
{
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
                UxmlKey = UxmlKey,
                TargetId = TargetId,
                Mode = Mode
            });

            base.Bake(clipEntity, context);
        }
    }
}