using BovineLabs.Core.Asset;
using System;
using BovineLabs.Nerve.ObjectManagement;
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
                for (var i = 0; i < Health.Length; i++)
                {
                    var schema = Health[i];
                    if (schema == null)
                    {
                        Debug.LogWarning(
                            $"{nameof(DataDisplayClip)} '{name}': Health[{i}] is null and will be skipped.", this);
                        continue;
                    }

                    context.Baker.DependsOn(schema);
                    if (schema is IUID uid)
                    {
                        // Id==0 means the AutoRef post-processor has not assigned an ID to the schema yet.
                        // ClipDataId{Id=0} would silently match IdValue{Id=0} and show the wrong data, so
                        // surface it loudly with the asset pingable as context (TODO.md item 15).
                        if (uid.ID == 0)
                            Debug.LogError(
                                $"{nameof(DataDisplayClip)} '{name}': Health schema '{schema.name}' has ID 0 — " +
                                "AutoRef has not assigned an ID yet. Re-import/save the asset before baking.",
                                schema);

                        var label = default(FixedString32Bytes);
                        label.CopyFromTruncated(schema.name);
                        buffer.Add(new ClipDataId { Id = uid.ID, Label = label });
                    }
                }

            base.Bake(clipEntity, context);
        }
    }
}