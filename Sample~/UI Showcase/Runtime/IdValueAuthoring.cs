using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine;

namespace UIShowcase.Runtime
{
    public sealed class IdValueAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public int Id;
            public float Value;
        }

        public Entry[] Entries = System.Array.Empty<Entry>();

        public sealed class IdValueBaker : Baker<IdValueAuthoring>
        {
            public override void Bake(IdValueAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<IdValue>(entity);
                foreach (var e in authoring.Entries)
                    buffer.Add(new IdValue { Id = e.Id, Value = e.Value });
            }
        }
    }
}
