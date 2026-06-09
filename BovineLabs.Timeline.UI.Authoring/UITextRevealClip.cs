using System;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.UI.Authoring
{
    [Serializable]
    public class UITextRevealClip : DOTSClip, ITimelineClipAsset
    {
        public string TargetId;

        [TextArea(3, 10)] public string Text;

        public UITextRevealMode Mode = UITextRevealMode.Typewriter;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new UITextRevealData
            {
                TargetId = TargetId,
                Text = Text,
                Mode = Mode
            });

            base.Bake(clipEntity, context);
        }
    }
}