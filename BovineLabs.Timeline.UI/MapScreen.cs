using System;
using System.ComponentModel;
using BovineLabs.Anchor;
using BovineLabs.Anchor.Nav;
using BovineLabs.Timeline.UI.Data.ViewModel;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The node-graph map / room-select nav screen. Renders one <see cref="NodeChip"/> per <see cref="MapScreenModel.Room"/>,
    /// positioned by its normalized coordinates, and forwards a legal pick to <see cref="MapScreenModel.Select"/>. Chip
    /// hover/click cues and the nav transition cue are played by <see cref="NodeChip"/> and <see cref="TimelineUIAppBuilder"/>
    /// respectively, so this screen only owns layout + selection. Implements <see cref="IAnchorNavigationScreen"/> so the
    /// nav host can drive enter/exit.
    /// </summary>
    [Transient]
    public class MapScreen : View<MapScreenModel>, IAnchorNavigationScreen, IDisposable
    {
        private readonly VisualElement nodes;

        /// <remarks>
        /// The view-model is constructor-injected (the DI singleton), matching the package's view convention.
        /// </remarks>
        public MapScreen(MapScreenModel viewModel)
            : base(viewModel)
        {
            this.AddToClassList("vex-map");

            this.nodes = new VisualElement { name = "map-nodes" };
            this.nodes.AddToClassList("vex-map__nodes");
            this.Add(this.nodes);

            // Subscribe on attach, unsubscribe on detach (registered pair — no bare constructor subscription that would
            // leak or go stale after a detach/re-attach). Rebuild on attach so a re-shown screen is current.
            this.RegisterCallback<AttachToPanelEvent>(_ => this.OnAttach());
            this.RegisterCallback<DetachFromPanelEvent>(_ => this.ViewModel.PropertyChanged -= this.OnPropertyChanged);
        }

        public void OnEnter(AnchorNavArgument[] args)
        {
            this.Rebuild();
        }

        public void OnExit(AnchorNavArgument[] args)
        {
        }

        public void Dispose()
        {
            this.ViewModel.PropertyChanged -= this.OnPropertyChanged;
        }

        private void OnAttach()
        {
            this.ViewModel.PropertyChanged -= this.OnPropertyChanged;
            this.ViewModel.PropertyChanged += this.OnPropertyChanged;
            this.Rebuild();
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapScreenModel.Rooms) || e.PropertyName == nameof(MapScreenModel.SelectedId))
            {
                this.Rebuild();
            }
        }

        private void Rebuild()
        {
            this.nodes.Clear();

            var rooms = this.ViewModel.Rooms;
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];

                var chip = new NodeChip
                {
                    glyph = room.Glyph,
                    kind = room.Kind,
                    style =
                    {
                        position = Position.Absolute,
                        left = Length.Percent(room.X * 100f),
                        top = Length.Percent((1f - room.Y) * 100f),
                    },
                };

                chip.EnableInClassList("is-legal", room.IsLegal);
                chip.EnableInClassList("is-current", room.IsCurrent);
                chip.EnableInClassList("is-selected", room.Id == this.ViewModel.SelectedId);
                chip.SetEnabled(room.IsLegal);

                var id = room.Id;
                chip.clicked += () => this.ViewModel.Select(id);

                this.nodes.Add(chip);
            }
        }
    }
}
