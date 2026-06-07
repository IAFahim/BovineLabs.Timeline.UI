// <copyright file="RowsView.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Anchor;
    using BovineLabs.Timeline.UI.Data.ViewModel;
    using Unity.AppUI.UI;
    using UnityEngine.UIElements;

    [Transient]
    public class RowsView : View<RowsViewModel>, IDisposable
    {
        private readonly GridView grid;

        public RowsView()
            : base(new RowsViewModel())
        {
            this.grid = new GridView
            {
                dataSource = this.ViewModel,
                selectionType = SelectionType.None,
                makeItem = this.MakeRow,
                bindItem = this.BindRow,
                itemsSource = this.ViewModel.Rows,
            };

            this.Add(this.grid);
            this.ViewModel.PropertyChanged += this.OnPropertyChanged;
        }

        public void Dispose()
        {
            this.ViewModel.PropertyChanged -= this.OnPropertyChanged;
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
                var row = this.ViewModel.Value.Rows[index];
                detail.text = row.Value.ToString();
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RowsViewModel.Rows))
            {
                this.grid.itemsSource = this.ViewModel.Rows;
                this.grid.Refresh();
            }
        }
    }
}

#endif
