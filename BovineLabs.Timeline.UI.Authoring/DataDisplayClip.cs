using System;
using BovineLabs.Core.ObjectManagement;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.UI.Authoring
{
    [Serializable]
    public class DataDisplayClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Schema objects whose IdValue is read from the track-bound entity and shown as rows.")]
        public HealthSchemaObject[] Health;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var buffer = context.Baker.AddBuffer<ClipDataId>(clipEntity);

            if (Health != null)
                foreach (var schema in Health)
                {
                    context.Baker.DependsOn(schema);
                    if (schema is IUID uid)
                    {
                        var label = default(Unity.Collections.FixedString32Bytes);
                        label.CopyFromTruncated(schema.name);
                        buffer.Add(new ClipDataId { Id = uid.ID, Label = label });
                    }
                }

            base.Bake(clipEntity, context);
        }
    }
}