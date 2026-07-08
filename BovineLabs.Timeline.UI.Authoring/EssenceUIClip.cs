using System;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.UI.Authoring
{
    [Serializable]
    public struct EventUIConfig
    {
        public ConditionEventObject Event;

        [Tooltip("How long the event toast stays on screen, in seconds, UNSCALED wall-clock time: bullet-time " +
                 "(WorldTimeScale) does not stretch it and game pause freezes it (UI clock, TODO.md item 13). " +
                 "Clamped against frame hitches.")]
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
            // Guard for clarity: Source.link is an optional schema reference (null when the source
            // routes via Player/Self rather than an Essence link).
            if (Source.link != null)
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

            for (var i = 0; i < Stats.Length; i++)
            {
                var s = Stats[i];
                if (s == null)
                {
                    Debug.LogWarning($"{nameof(EssenceUIClip)} '{name}': Stats[{i}] is null and will be skipped.", this);
                    continue;
                }

                statBuffer.Add(new ClipStat { Key = s.Key, Name = s.name });
            }
        }

        private void BakeIntrinsics(Entity clipEntity, BakingContext context)
        {
            var intBuffer = context.Baker.AddBuffer<ClipIntrinsic>(clipEntity);
            if (Intrinsics == null)
                return;

            for (var idx = 0; idx < Intrinsics.Length; idx++)
            {
                var i = Intrinsics[idx];
                if (i == null)
                {
                    Debug.LogWarning(
                        $"{nameof(EssenceUIClip)} '{name}': Intrinsics[{idx}] is null and will be skipped.", this);
                    continue;
                }

                intBuffer.Add(new ClipIntrinsic
                {
                    Key = i.Key, Name = i.name,
                    Min = i.Range.x, Max = i.Range.y,
                    MinStat = i.MinStat, MaxStat = i.MaxStat
                });
            }
        }

        private void BakeEvents(Entity clipEntity, BakingContext context)
        {
            var evBuffer = context.Baker.AddBuffer<ClipEvent>(clipEntity);
            if (Events == null)
                return;

            for (var i = 0; i < Events.Length; i++)
            {
                var e = Events[i];
                if (e.Event == null)
                {
                    Debug.LogWarning(
                        $"{nameof(EssenceUIClip)} '{name}': Events[{i}].Event is null and will be skipped.", this);
                    continue;
                }

                evBuffer.Add(new ClipEvent
                    { Key = new ConditionKey(e.Event.Key), Name = e.Event.name, Duration = e.DisplayDuration });
            }
        }
    }
}