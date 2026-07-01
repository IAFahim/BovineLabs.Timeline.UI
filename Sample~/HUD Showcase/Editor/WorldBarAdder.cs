using BovineLabs.Essence.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vex.HealthBar;

namespace Vex.HudShowcase.Editor
{
    /// <summary>
    /// Drops a world-space health bar onto each showcase actor by editing the ALREADY-OPEN subscene and SaveScene — never
    /// NewScene/OpenScene (which pop the modal Save dialog). Lets the world bar be Play-verified alongside the HUD so the
    /// two stacks can be compared. Idempotent.
    /// </summary>
    public static class WorldBarAdder
    {
        private const string StylePath = "Assets/HealthBar/Styles/Player.asset";

        [MenuItem("Showcase/Add World Bars To Open Scene")]
        public static void AddWorldBars()
        {
            var style = AssetDatabase.LoadAssetAtPath<HealthBarStyle>(StylePath);
            if (style == null)
            {
                Debug.LogError($"[WorldBar] no HealthBarStyle at {StylePath}");
                return;
            }

            var added = 0;
            UnityEngine.SceneManagement.Scene targetScene = default;
            for (var i = 0; i < 4; i++)
            {
                var player = GameObject.Find($"Player {i}");
                if (player == null || player.transform.Find("World Health Bar") != null)
                {
                    continue;
                }

                var stat = player.GetComponentInChildren<StatAuthoring>(true);
                var essence = stat != null ? stat.gameObject : player;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "World Health Bar";
                var col = quad.GetComponent<Collider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                }

                EditorSceneManager.MoveGameObjectToScene(quad, player.scene);
                quad.transform.SetParent(player.transform, false);
                quad.transform.localPosition = new Vector3(0f, 2.4f, 0f);

                var hb = quad.AddComponent<HealthBarAuthoring>();
                hb.style = style;
                hb.source = essence;

                added++;
                targetScene = player.scene;
            }

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
                EditorSceneManager.SaveScene(targetScene);
            }

            Debug.Log($"[WorldBar] added {added} world bars to '{targetScene.name}'");
        }
    }
}
