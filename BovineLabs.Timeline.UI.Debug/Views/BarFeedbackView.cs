using System;
using System.Text;
using BovineLabs.Anchor;
using BovineLabs.Anchor.Debug.Toolbar;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    /// <summary>
    /// Debug toolbar tab: the current <see cref="BarFeedbackEvent"/> traffic — how many events per <see cref="FeedbackKind"/>
    /// exist this frame and the published drain frame. The #1 diagnostic for "the chip/vignette never showed" (was it
    /// emitted? which kind?). Self-polls the default world.
    /// </summary>
    [Preserve]
    [AutoToolbar("BarFeedback", "Timeline UI")]
    public class BarFeedbackView : View<ToolbarSummaryViewModel>
    {
        public const string UssClassName = "vex-barfeedback-tab";

        private static readonly int KindCount = Enum.GetValues(typeof(FeedbackKind)).Length;

        private readonly Label output;
        private readonly int[] counts;

        [Preserve]
        public BarFeedbackView()
            : base(new ToolbarSummaryViewModel())
        {
            this.AddToClassList(UssClassName);
            this.counts = new int[KindCount];
            this.output = new Label { enableRichText = false, style = { whiteSpace = WhiteSpace.Normal } };
            this.Add(this.output);
            this.schedule.Execute(this.Poll).Every(250);
        }

        private void Poll()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world is not { IsCreated: true })
            {
                this.output.text = "No world";
                return;
            }

            var em = world.EntityManager;

            Array.Clear(this.counts, 0, this.counts.Length);
            var total = 0;

            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<BarFeedbackEvent>()))
            {
                using var entities = query.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < entities.Length; i++)
                {
                    var buffer = em.GetBuffer<BarFeedbackEvent>(entities[i], true);
                    for (var j = 0; j < buffer.Length; j++)
                    {
                        var kind = (int)buffer[j].Kind;
                        if ((uint)kind < (uint)this.counts.Length)
                        {
                            this.counts[kind]++;
                        }

                        total++;
                    }
                }
            }

            var frame = 0u;
            using (var frameQuery = em.CreateEntityQuery(ComponentType.ReadOnly<BarFeedbackFrame>()))
            {
                if (!frameQuery.IsEmptyIgnoreFilter)
                {
                    frame = frameQuery.GetSingleton<BarFeedbackFrame>().Frame;
                }
            }

            var sb = new StringBuilder();
            sb.Append("Drain frame: ").Append(frame).Append('\n');
            sb.Append("Total events: ").Append(total).Append('\n');
            for (var k = 0; k < this.counts.Length; k++)
            {
                if (this.counts[k] > 0)
                {
                    sb.Append((FeedbackKind)k).Append(": ").Append(this.counts[k]).Append('\n');
                }
            }

            this.output.text = sb.ToString();
        }
    }
}

#endif
