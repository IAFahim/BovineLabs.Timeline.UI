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
                UxmlKey = Fit(UxmlKey, nameof(UxmlKey)),
                TargetId = Fit(TargetId, nameof(TargetId)),
                Mode = Mode
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
                    $"UxmlViewClip '{name}' {fieldName} \"{value}\" exceeds the FixedString64Bytes budget (61 bytes) " +
                    $"and was truncated to \"{fs}\"; shorten it (a truncated key resolves to the wrong element / none).",
                    this);

            return fs;
        }
    }
}