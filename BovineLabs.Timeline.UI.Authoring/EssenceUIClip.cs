using System;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.UI.Authoring
{
    [Serializable]
    public struct EventUIConfig
    {
        public ConditionEventObject Event;
        public float DisplayDuration;
    }

    [Serializable]
    public class EssenceUIClip : DOTSClip, ITimelineClipAsset
    {
        public StatSchemaObject[] Stats;
        public IntrinsicSchemaObject[] Intrinsics;
        public EventUIConfig[] Events;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var statBuffer = context.Baker.AddBuffer<ClipStat>(clipEntity);
            if (Stats != null)
            {
                foreach (var stat in Stats)
                {
                    if (stat != null)
                    {
                        statBuffer.Add(new ClipStat { Key = stat.Key, Name = stat.name });
                    }
                }
            }

            var intrinsicBuffer = context.Baker.AddBuffer<ClipIntrinsic>(clipEntity);
            if (Intrinsics != null)
            {
                foreach (var intrinsic in Intrinsics)
                {
                    if (intrinsic != null)
                    {
                        intrinsicBuffer.Add(new ClipIntrinsic
                        {
                            Key = intrinsic.Key,
                            Name = intrinsic.name,
                            Min = intrinsic.Range.x,
                            Max = intrinsic.Range.y,
                            MinStat = intrinsic.MinStat,
                            MaxStat = intrinsic.MaxStat,
                        });
                    }
                }
            }

            var eventBuffer = context.Baker.AddBuffer<ClipEvent>(clipEntity);
            if (Events != null)
            {
                foreach (var config in Events)
                {
                    if (config.Event != null)
                    {
                        eventBuffer.Add(new ClipEvent
                        {
                            Key = config.Event.Key,
                            Name = config.Event.name,
                            Duration = config.DisplayDuration,
                        });
                    }
                }
            }

            context.Baker.AddBuffer<ActiveUIEvent>(clipEntity);

            base.Bake(clipEntity, context);
        }
    }
}
