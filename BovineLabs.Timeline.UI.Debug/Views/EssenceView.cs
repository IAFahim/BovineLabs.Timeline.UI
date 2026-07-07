using System.Text;
using BovineLabs.Anchor;
using BovineLabs.Anchor.Debug.Toolbar;
using BovineLabs.Essence.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    /// <summary>
    /// Debug toolbar tab: which resolved players carry Essence data. For each registry slot (1..N) it reports whether the
    /// entity has <see cref="Stat"/> / <see cref="Intrinsic"/> buffers — a fast "is this player wired for stats/HUD?"
    /// check. Self-polls the default world. (Value-level readouts belong to the in-game EssenceUI panel; this is a
    /// presence probe.)
    /// </summary>
    [Preserve]
    [AutoToolbar("Essence", "Timeline UI")]
    public class EssenceView : View<ToolbarSummaryViewModel>
    {
        public const string UssClassName = "vex-essence-tab";

        private readonly Label output;

        [Preserve]
        public EssenceView()
            : base(new ToolbarSummaryViewModel())
        {
            this.AddToClassList(UssClassName);
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
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ControllableRegistry>());
            if (query.IsEmptyIgnoreFilter)
            {
                this.output.text = "No ControllableRegistry";
                return;
            }

            var registry = query.GetSingleton<ControllableRegistry>();
            if (!registry.ByPlayer.IsCreated)
            {
                this.output.text = "Registry not built";
                return;
            }

            var sb = new StringBuilder();
            var any = false;
            for (var p = 0; p < registry.ByPlayer.Length; p++)
            {
                var e = registry.ByPlayer[p];
                if (e == Entity.Null || !em.Exists(e))
                {
                    continue;
                }

                any = true;
                var hasStat = em.HasBuffer<Stat>(e);
                var hasIntrinsic = em.HasBuffer<Intrinsic>(e);
                sb.Append("P").Append(p).Append(": stat=").Append(hasStat ? "yes" : "no")
                    .Append(" intrinsic=").Append(hasIntrinsic ? "yes" : "no").Append('\n');
            }

            this.output.text = any ? sb.ToString() : "No players";
        }
    }
}

#endif
