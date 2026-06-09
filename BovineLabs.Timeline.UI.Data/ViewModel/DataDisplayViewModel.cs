using System;
using BovineLabs.Anchor;
using Unity.Collections;
using Unity.Properties;

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    [IsService]
    public partial class DataDisplayViewModel : SystemObservableObject<DataDisplayViewModel.Data>, ILoadable
    {
        [CreateProperty(ReadOnly = true)] public bool IsVisible => Value.IsVisible;
        [CreateProperty(ReadOnly = true)] public UIArray<Data.Row> Rows => Value.Rows;

        public void Load()
        {
            Value.Initialize();
        }

        public void Unload()
        {
            Value.Dispose();
        }

        public partial struct Data
        {
            [SystemProperty] private bool isVisible;
            [SystemProperty] private NativeList<Row> rows;

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
                public int Id;
                public FixedString32Bytes Name;
                public float Value;

                [CreateProperty(ReadOnly = true)] public string Label => Name.ToString();
                [CreateProperty(ReadOnly = true)] public string Display => Value.ToString("0.##");

                public bool Equals(Row other)
                {
                    return Id == other.Id && Name.Equals(other.Name) && Value.Equals(other.Value);
                }

                public override int GetHashCode()
                {
                    return unchecked((Id * 397) ^ Name.GetHashCode());
                }
            }
        }
    }
}