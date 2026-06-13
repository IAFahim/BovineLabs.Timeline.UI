// <copyright file="RowsView.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

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

        public RowsView()
            : base(new RowsViewModel())
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
            ViewModel.PropertyChanged += OnPropertyChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => ViewModel.PropertyChanged -= OnPropertyChanged);
        }

        public void Dispose()
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
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
            // AnchorGridView sets element.dataSource = itemsSource[index] (the Row),
            // so UXML bindings on the template resolve Label/Value against the Row.
            // We use bindItem to handle IsVisible toggles on the row container itself.
            if (element.Q<Text>("detail") is { } detail)
            {
                var row = ViewModel.Value.Rows[index];
                detail.text = row.Value;
            }
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