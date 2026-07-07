using BovineLabs.Anchor;
using BovineLabs.Anchor.Audio;
using BovineLabs.Anchor.Nav;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The app builder for the Timeline.UI HUD / nav app. Beyond the standard Anchor scaffolding it wires navigation
    /// audio: every destination change plays the <see cref="UiAudioCues.Nav"/> cue through <see cref="AnchorAudio"/>.
    /// Per-control click/hover cues are owned by the elements themselves (see <see cref="NodeChip"/>), so this only
    /// needs to cover the app-level nav transitions the individual controls can't see.
    /// </summary>
    public class TimelineUIAppBuilder : AnchorAppBuilder
    {
        private IAnchorNavHost boundNavHost;

        protected override void OnAppInitialized(AnchorApp app)
        {
            base.OnAppInitialized(app);

            // ToolbarOnly apps never build a nav host — nothing to wire.
            if (app.NavHost != null)
            {
                this.boundNavHost = app.NavHost;
                this.boundNavHost.DestinationChanged += OnDestinationChanged;
            }
        }

        protected override void OnAppShuttingDown(AnchorApp app)
        {
            if (this.boundNavHost != null)
            {
                this.boundNavHost.DestinationChanged -= OnDestinationChanged;
                this.boundNavHost = null;
            }

            base.OnAppShuttingDown(app);
        }

        private static void OnDestinationChanged(AnchorNavHost host, string destination)
        {
            AnchorAudio.Play(UiAudioCues.Nav, AnchorAudioCue.Activate, AnchorAudioCueOverride.Inherit);
        }
    }
}
