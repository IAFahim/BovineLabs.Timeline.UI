using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PropertyDrawers;
using UnityEngine;

namespace BovineLabs.Timeline.UI.Authoring
{
    [AutoRef("GameSettings", "healthSchemas", nameof(HealthSchemaObject), "Schemas/Health")]
    [CreateAssetMenu(menuName = "YoYo")]
    public sealed class HealthSchemaObject : ScriptableObject, IUID
    {
        [SerializeField] [InspectorReadOnly] private int id;

        public int Id => id;

        int IUID.ID
        {
            get => id;
            set => id = value;
        }
    }
}