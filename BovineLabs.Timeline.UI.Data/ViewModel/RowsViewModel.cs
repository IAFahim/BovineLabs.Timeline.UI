// <copyright file="RowsViewModel.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    using System;
    using BovineLabs.Anchor;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Properties;

    [IsService]
    [GeneratePropertyBag]
    public partial class RowsViewModel : SystemObservableObject<RowsViewModel.Data>, ILoadable
    {
        [CreateProperty(ReadOnly = true)]
        public UIArray<Data.Row> Rows => this.Value.Rows;

        [CreateProperty(ReadOnly = true)]
        public bool IsVisible => this.Value.IsVisible;

        public void Load()
        {
            this.Value.Initialize();
        }

        public void Unload()
        {
            this.Value.Dispose();
        }

        [GeneratePropertyBag]
        public partial struct Data
        {
            [SystemProperty]
            private NativeList<Row> rows;

            [SystemProperty]
            private bool isVisible;

            internal void Initialize()
            {
                this.rows = new NativeList<Row>(Allocator.Persistent);
            }

            internal void Dispose()
            {
                this.rows.Dispose();
            }

            [GeneratePropertyBag]
            public partial struct Row : IEquatable<Row>
            {
                public Entity Source;

                public FixedString64Bytes RawLabel;

                public int RawValue;

                [CreateProperty(ReadOnly = true)]
                public string Label => this.RawLabel.ToString();

                [CreateProperty(ReadOnly = true)]
                public string Value => this.RawValue.ToString();

                public bool Equals(Row other) =>
                    this.Source.Equals(other.Source) && this.RawLabel.Equals(other.RawLabel) && this.RawValue == other.RawValue;

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hash = this.Source.GetHashCode();
                        hash = (hash * 397) ^ this.RawLabel.GetHashCode();
                        hash = (hash * 31) ^ this.RawValue;
                        return hash;
                    }
                }
            }
        }
    }
}
