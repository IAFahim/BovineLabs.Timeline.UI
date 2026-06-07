namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using BovineLabs.Timeline.Authoring;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Entities;
    using UnityEngine;
    using UnityEngine.Timeline;

    [Serializable]
    public class UITextRevealClip : DOTSClip, ITimelineClipAsset
    {
        public string TargetId;

        [TextArea(3, 10)]
        public string Text;

        public UITextRevealMode Mode = UITextRevealMode.Typewriter;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new UITextRevealData
            {
                TargetId = this.TargetId,
                Text = this.Text,
                Mode = this.Mode
            });

            base.Bake(clipEntity, context);
        }
    }
}
