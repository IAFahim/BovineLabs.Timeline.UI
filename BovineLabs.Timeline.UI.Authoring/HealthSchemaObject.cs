using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PropertyDrawers;
using UnityEngine;

namespace BovineLabs.Timeline.UI.Authoring
{
    // NOTE (TODO.md item 15): no OnValidate() id==0 warning here. AutoRef assigns the id from an
    // AssetPostprocessor (ObjectManagementProcessor) on import — NOT from OnValidate. OnValidate runs
    // on every inspector edit and on load, including the normal window where a freshly created asset
    // legitimately still has id==0 before the post-processor runs, so a guard here would fight the
    // AutoRef flow and spam false positives. The id==0 check lives at the consumption site instead
    // (DataDisplayClip.Bake logs a context-pingable error), which only fires when the schema is
    // actually baked with an unassigned id.
    [AutoRef("GameSettings", "healthSchemas", nameof(HealthSchemaObject), "Schemas/Health")]
    [CreateAssetMenu(menuName = "BovineLabs/UI/Health Schema")]
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