using System.ComponentModel;
using BovineLabs.Timeline.UI.Data.ViewModel;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// Screen-space 4-player HUD overlay. Renders one card per active player, anchored to a screen corner
    /// (P0 top-left, P1 top-right, P2 bottom-left, P3 bottom-right). This is structure + USS class hooks ONLY — every
    /// visual (colors, fonts, sizes, the bar look) is driven by USS classes so designers restyle without code. The
    /// only thing set from code is data: the fill width (= health fraction) and the low-health state class.
    /// Deliberately a plain VisualElement (not <c>View&lt;T&gt;</c>) so it is never auto-registered as a DI service.
    /// </summary>
    public sealed class HudView : VisualElement
    {
        public const string UssRoot = "vex-hud";
        private const float LowHealthThreshold = 0.30f;
        private const int MaxPlayers = 4;

        private static readonly string[] CornerClass =
        {
            "vex-hud__corner--tl",
            "vex-hud__corner--tr",
            "vex-hud__corner--bl",
            "vex-hud__corner--br",
        };

        private readonly HudViewModel viewModel;
        private readonly VisualElement[] corners = new VisualElement[MaxPlayers];

        public HudView(HudViewModel viewModel)
        {
            this.viewModel = viewModel;

            this.AddToClassList(UssRoot);
            this.pickingMode = PickingMode.Ignore;
            this.style.position = Position.Absolute;
            this.style.top = 0;
            this.style.left = 0;
            this.style.right = 0;
            this.style.bottom = 0;

            for (var i = 0; i < MaxPlayers; i++)
            {
                var corner = new VisualElement { name = $"corner-{i}", pickingMode = PickingMode.Ignore };
                corner.AddToClassList("vex-hud__corner");
                corner.AddToClassList(CornerClass[i]);
                corner.style.position = Position.Absolute;

                // baseline corner placement (USS can override) so the package view works standalone
                const float pad = 12f;
                var top = i is 0 or 1;
                var left = i is 0 or 2;
                if (top) corner.style.top = pad; else corner.style.bottom = pad;
                if (left) corner.style.left = pad; else corner.style.right = pad;

                this.corners[i] = corner;
                this.Add(corner);
            }

            // NOTE: do NOT Rebuild() here — the view-model's NativeList isn't Initialized until the driver binds
            // (UIHelper.Bind → ILoadable.Load), which happens after this view is constructed. The first data frame
            // arrives via PropertyChanged; reading Value.Players before init throws.
            this.viewModel.PropertyChanged += this.OnPropertyChanged;
            this.RegisterCallback<DetachFromPanelEvent>(_ => this.viewModel.PropertyChanged -= this.OnPropertyChanged);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UnityEngine.Debug.Log($"[HUD] OnPropertyChanged: {e.PropertyName}");
            this.Rebuild();
        }

        private void Rebuild()
        {
            foreach (var corner in this.corners)
            {
                corner.Clear();
            }

            var players = this.viewModel.Value.Players;
            UnityEngine.Debug.Log($"[HUD] Rebuild: attachedToPanel={this.panel != null} playersCreated={players.IsCreated} count={(players.IsCreated ? players.Length : -1)}");
            if (!players.IsCreated)
            {
                return;
            }

            for (var i = 0; i < players.Length; i++)
            {
                var slot = players[i];
                if (slot.Player < 0 || slot.Player >= MaxPlayers)
                {
                    continue;
                }

                this.corners[slot.Player].Add(BuildCard(slot));
            }
        }

        private static VisualElement BuildCard(HudViewModel.Data.PlayerSlot slot)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("vex-hud__card");
            card.AddToClassList($"vex-hud__card--p{slot.Player}");

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("vex-hud__header");

            var name = new Label(slot.Label);
            name.AddToClassList("vex-hud__name");

            var value = new Label(slot.Display);
            value.AddToClassList("vex-hud__value");

            header.Add(name);
            header.Add(value);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("vex-hud__bar");

            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("vex-hud__fill");
            fill.style.width = Length.Percent(slot.Fraction * 100f);
            if (slot.Fraction <= LowHealthThreshold)
            {
                fill.AddToClassList("vex-hud__fill--low");
            }

            track.Add(fill);

            card.Add(header);
            card.Add(track);
            return card;
        }
    }
}
