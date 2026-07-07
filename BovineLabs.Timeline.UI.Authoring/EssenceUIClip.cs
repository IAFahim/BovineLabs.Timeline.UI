using System;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
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
        public UISourceAuthoring Source;
        public StatSchemaObject[] Stats;
        public IntrinsicSchemaObject[] Intrinsics;
        public EventUIConfig[] Events;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            RegisterDependencies(context);

            context.Baker.AddComponent(clipEntity, Source.ToComponent(context.Baker));

            BakeStats(clipEntity, context);
            BakeIntrinsics(clipEntity, context);
            BakeEvents(clipEntity, context);

            context.Baker.AddBuffer<ActiveUIEvent>(clipEntity);

            base.Bake(clipEntity, context);
        }

        private void RegisterDependencies(BakingContext context)
        {
            context.Baker.DependsOn(Source.link);
            if (Stats != null)
                foreach (var s in Stats)
                    context.Baker.DependsOn(s);
            if (Intrinsics != null)
                foreach (var i in Intrinsics)
                    context.Baker.DependsOn(i);
            if (Events != null)
                foreach (var e in Events)
                    context.Baker.DependsOn(e.Event);
        }

        private void BakeStats(Entity clipEntity, BakingContext context)
        {
            var statBuffer = context.Baker.AddBuffer<ClipStat>(clipEntity);
            if (Stats == null)
                return;

            foreach (var s in Stats)
                if (s != null)
                    statBuffer.Add(new ClipStat { Key = s.Key.ID, Name = s.name });
        }

        private void BakeIntrinsics(Entity clipEntity, BakingContext context)
        {
            var intBuffer = context.Baker.AddBuffer<ClipIntrinsic>(clipEntity);
            if (Intrinsics == null)
                return;

            foreach (var i in Intrinsics)
                if (i != null)
                    intBuffer.Add(new ClipIntrinsic
                    {
                        Key = i.Key.ID, Name = i.name,
                        Min = i.Range.x, Max = i.Range.y,
                        MinStat = i.MinStat, MaxStat = i.MaxStat
                    });
        }

        private void BakeEvents(Entity clipEntity, BakingContext context)
        {
            var evBuffer = context.Baker.AddBuffer<ClipEvent>(clipEntity);
            if (Events == null)
                return;

            foreach (var e in Events)
                if (e.Event != null)
                    evBuffer.Add(new ClipEvent
                        { Key = new ConditionKey(e.Event.Key), Name = e.Event.name, Duration = e.DisplayDuration });
        }
    }
}