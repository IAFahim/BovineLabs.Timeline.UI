// <copyright file="RowsViewModel.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using BovineLabs.Anchor;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    [IsService]
    [GeneratePropertyBag]
    public partial class RowsViewModel : SystemObservableObject<RowsViewModel.Data>, ILoadable
    {
        [CreateProperty(ReadOnly = true)] public UIArray<Data.Row> Rows => Value.Rows;

        [CreateProperty(ReadOnly = true)] public bool IsVisible => Value.IsVisible;

        public void Load()
        {
            Value.Initialize();
        }

        public void Unload()
        {
            Value.Dispose();
        }

        [GeneratePropertyBag]
        public partial struct Data
        {
            [SystemProperty] private NativeList<Row> rows;

            [SystemProperty] private bool isVisible;

            internal void Initialize()
            {
                rows = new NativeList<Row>(Allocator.Persistent);
            }

            internal void Dispose()
            {
                rows.Dispose();
            }

            [GeneratePropertyBag]
            public struct Row : IEquatable<Row>
            {
                public Entity Source;

                public FixedString64Bytes RawLabel;

                public int RawValue;

                [CreateProperty(ReadOnly = true)] public string Label => RawLabel.ToString();

                [CreateProperty(ReadOnly = true)] public string Value => RawValue.ToString();

                public bool Equals(Row other)
                {
                    return Source.Equals(other.Source) && RawLabel.Equals(other.RawLabel) && RawValue == other.RawValue;
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hash = Source.GetHashCode();
                        hash = (hash * 397) ^ RawLabel.GetHashCode();
                        hash = (hash * 31) ^ RawValue;
                        return hash;
                    }
                }
            }
        }
    }
}