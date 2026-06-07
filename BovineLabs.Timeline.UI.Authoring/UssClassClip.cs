namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using BovineLabs.Timeline.Authoring;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Entities;
    using UnityEngine.Timeline;

    [Serializable]
    public class UssClassClip : DOTSClip, ITimelineClipAsset
    {
        public string TargetId;
        public string ClassName;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new UssClassData
            {
                TargetId = this.TargetId,
                ClassName = this.ClassName
            });

            base.Bake(clipEntity, context);
        }
    }
}
