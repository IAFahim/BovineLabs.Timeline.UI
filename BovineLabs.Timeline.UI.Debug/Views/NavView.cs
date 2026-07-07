using BovineLabs.Anchor;
using BovineLabs.Anchor.Debug.Toolbar;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    /// <summary>
    /// Debug toolbar tab: the Anchor navigation state — current destination and whether a back step is available. Reads
    /// the live <see cref="AnchorApp"/> nav host; no ECS. Self-polls every quarter second.
    /// </summary>
    [Preserve]
    [AutoToolbar("Nav", "Timeline UI")]
    public class NavView : View<ToolbarSummaryViewModel>
    {
        public const string UssClassName = "vex-nav-tab";

        private readonly Label output;

        [Preserve]
        public NavView()
            : base(new ToolbarSummaryViewModel())
        {
            this.AddToClassList(UssClassName);
            this.output = new Label { enableRichText = false, style = { whiteSpace = WhiteSpace.Normal } };
            this.Add(this.output);
            this.schedule.Execute(this.Poll).Every(250);
        }

        private void Poll()
        {
            var app = AnchorApp.Current;
            if (app?.NavHost == null)
            {
                this.output.text = "No nav host";
                return;
            }

            var host = app.NavHost;
            var destination = string.IsNullOrEmpty(host.CurrentDestination) ? "(none)" : host.CurrentDestination;
            this.output.text = $"Destination: {destination}\nCan go back: {host.CanGoBack}";
        }
    }
}

#endif
