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
                TargetId = Fit(TargetId, nameof(TargetId)),
                ClassName = Fit(ClassName, nameof(ClassName))
            });

            base.Bake(clipEntity, context);
        }

        private FixedString64Bytes Fit(string value, string fieldName)
        {
            var fs = new FixedString64Bytes();
            if (string.IsNullOrEmpty(value))
                return fs;

            fs.CopyFromTruncated(value);
            if (fs.ToString() != value)
                Debug.LogWarning(
                    $"UssClassClip '{name}' {fieldName} exceeds the FixedString64Bytes budget (61 bytes) and was truncated to \"{fs}\"; shorten it.",
                    this);
            return fs;
        }
    }
}