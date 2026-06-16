using System;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
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
                TargetId = Fit64(TargetId, nameof(TargetId)),
                Text = Fit512(Text, nameof(Text)),
                Mode = Mode
            });

            base.Bake(clipEntity, context);
        }

        // string->FixedString assignment THROWS on overflow; CopyFromTruncated fits without throwing so a
        // clean truncation warning fires instead of an unhandled bake exception (see UxmlViewClip).
        private FixedString64Bytes Fit64(string value, string fieldName)
        {
            var fs = new FixedString64Bytes();
            if (string.IsNullOrEmpty(value))
                return fs;

            fs.CopyFromTruncated(value);
            if (fs.ToString() != value)
                Debug.LogWarning($"UITextRevealClip '{name}' {fieldName} exceeds the FixedString64Bytes budget (61 bytes) and was truncated to \"{fs}\"; shorten it.", this);
            return fs;
        }

        private FixedString512Bytes Fit512(string value, string fieldName)
        {
            var fs = new FixedString512Bytes();
            if (string.IsNullOrEmpty(value))
                return fs;

            fs.CopyFromTruncated(value);
            if (fs.ToString() != value)
                Debug.LogWarning($"UITextRevealClip '{name}' {fieldName} exceeds the FixedString512Bytes budget (509 bytes) and was truncated; shorten it.", this);
            return fs;
        }
    }
}