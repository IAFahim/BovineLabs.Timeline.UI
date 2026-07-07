using System;
using System.ComponentModel;
using BovineLabs.Anchor;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.AppUI.UI;
using UnityEngine.UIElements;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    [Transient]
    public class RowsView : View<RowsViewModel>, IDisposable
    {
        private readonly GridView grid;

        /// <remarks>
        /// The view-model MUST be constructor-injected: the track systems pin the DI singleton via
        /// <c>UIHelper.Bind()</c> → <c>IViewModelService.Load</c>. A view that news its own VM renders
        /// a different object and never sees system data.
        /// </remarks>
        public RowsView(RowsViewModel viewModel)
            : base(viewModel)
        {
            grid = new GridView
            {
                dataSource = ViewModel,
                selectionType = SelectionType.None,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = ViewModel.Rows
            };

            Add(grid);

            // Subscribe on attach, unsubscribe on detach (registered pair — no constructor subscription).
            // Without the attach counterpart a single detach/re-attach left the grid unsubscribed forever,
            // silently showing stale rows. On attach we resync itemsSource + Refresh so it is current.
            RegisterCallback<AttachToPanelEvent>(_ => OnAttach());
            RegisterCallback<DetachFromPanelEvent>(_ => ViewModel.PropertyChanged -= OnPropertyChanged);
        }

        public void Dispose()
        {
            // Final safety unsubscribe for callers that dispose without a detach event firing.
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnAttach()
        {
            // Guard against double-subscribe if attach fires without an intervening detach.
            ViewModel.PropertyChanged -= OnPropertyChanged;
            ViewModel.PropertyChanged += OnPropertyChanged;

            grid.itemsSource = ViewModel.Rows;
            grid.Refresh();
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Text { name = "label", style = { flexGrow = 1 } });
            row.Add(new Text { name = "detail", style = { width = 60 } });
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            var row = ViewModel.Value.Rows[index];

            if (element.Q<Text>("label") is { } label) label.text = row.Label;

            if (element.Q<Text>("detail") is { } detail) detail.text = row.Value;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RowsViewModel.Rows))
            {
                grid.itemsSource = ViewModel.Rows;
                grid.Refresh();
            }
        }
    }
}

#endif