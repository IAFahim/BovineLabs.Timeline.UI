using System.Collections.Generic;
using BovineLabs.Anchor;
using BovineLabs.Anchor.MVVM;
using BovineLabs.Anchor.Nav;
using BovineLabs.Anchor.Services;
using BovineLabs.Timeline.UI.Data.ViewModel;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// Hosts the screen-space HUD as an always-on overlay. Unlike the default Anchor app it does NOT build a nav graph
    /// or toolbar (a HUD is not a navigable page) — it just resolves the singleton <see cref="HudViewModel"/> from DI
    /// and adds a <see cref="HudView"/> straight onto the app root. Attach to a GameObject that also carries a
    /// PanelRenderer (Unity 6.5+) / UIDocument. Assign one or more USS stylesheets to skin the HUD.
    /// </summary>
    [AddComponentMenu("BovineLabs/Timeline UI/HUD App Builder")]
    public class HudAppBuilder : AnchorAppBuilder
    {
        [SerializeField]
        [Tooltip("USS stylesheet(s) that skin the HUD (.vex-hud, .vex-hud__card, .vex-hud__bar, .vex-hud__fill, ...). " +
                 "Edit these to restyle without touching code.")]
        private List<StyleSheet> hudStyleSheets = new();

        private HudView view;

        protected override void OnAppInitialized(AnchorApp app)
        {
            // Pure overlay HUD: we skip the base start-destination navigation (don't want AnchorSettings' page under the
            // HUD), but Anchor's NavigationStateSystem derefs AnchorApp.Current.NavHost every frame — so give the app a
            // real (un-navigated, empty) nav host to avoid a per-frame NullReferenceException.
            app.RootVisualElement.pickingMode = PickingMode.Ignore;
            if (app.NavHost == null)
            {
                var navHost = new AnchorNavHost(AnchorSettings.I.Actions, AnchorSettings.I.Animations);
                app.NavHost = navHost;
                app.RootVisualElement.Add(navHost);
            }

            var viewModel = app.Services.GetRequiredService<IViewModelService>().Load<HudViewModel>();
            this.view = new HudView(viewModel);

            foreach (var sheet in this.hudStyleSheets)
            {
                if (sheet != null)
                {
                    this.view.styleSheets.Add(sheet);
                }
            }

            app.RootVisualElement.Add(this.view);
            UnityEngine.Debug.Log($"[HUD] app initialized; view added; vm={(viewModel != null)}; sheets={this.hudStyleSheets.Count}");
        }

        protected override void OnAppShuttingDown(AnchorApp app)
        {
            // intentionally NOT calling base: we never created nav state to save.
            this.view?.RemoveFromHierarchy();
            this.view = null;
        }
    }
}
