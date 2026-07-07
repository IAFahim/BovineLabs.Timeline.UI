using System.Text;
using BovineLabs.Anchor;
using BovineLabs.Anchor.Debug.Toolbar;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    /// <summary>
    /// Debug toolbar tab: the live <see cref="ControllableRegistry"/> — which player slot resolves to which entity.
    /// Player-agnostic: it lists every non-empty slot (1..N), never a fixed count. Self-polls the default world every
    /// quarter second (FPS-tab style).
    /// </summary>
    [Preserve]
    [AutoToolbar("Players", "Timeline UI")]
    public class PlayersView : View<ToolbarSummaryViewModel>
    {
        public const string UssClassName = "vex-players-tab";

        private readonly Label output;

        [Preserve]
        public PlayersView()
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
                if (e == Entity.Null)
                {
                    continue;
                }

                any = true;
                sb.Append("P").Append(p).Append(" → Entity ").Append(e.Index).Append(':').Append(e.Version).Append('\n');
            }

            this.output.text = any ? sb.ToString() : "No players";
        }
    }
}

#endif
