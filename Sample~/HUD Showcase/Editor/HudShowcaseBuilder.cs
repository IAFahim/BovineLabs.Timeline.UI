using System.Collections.Generic;
using System.Linq;
using BovineLabs.Core.Editor.Settings;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.PlayerInputs.Authoring;
using BovineLabs.Timeline.UI.Authoring;
using BovineLabs.Timeline.UI.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vex.Preload.Editor;

namespace Vex.HudShowcase.Editor
{
    /// <summary>
    /// Builds a clean, runnable showcase for the generic data-UI HUD. NO HUD-specific scene component: the HUD is now
    /// fully data-driven — this just (1) ensures the project's <see cref="DataUISettings"/> asset has 4 health-bar rows
    /// + the "hud" panel (the designer can edit it freely afterward in BovineLabs ▸ Settings ▸ UI), and (2) builds a
    /// bootstrapped scene with 4 players (PlayerId/Controllable) carrying demo health. The Anchor app host + the
    /// SettingsAuthoring that bakes DataUISettings both come from the Preload "Required In Scene" bootstrap prefab.
    /// Menu: Showcase ▸ Build HUD Showcase.
    /// </summary>
    public static class HudShowcaseBuilder
    {
        private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
        private const string HealthIntrinsic = "Assets/Settings/Schemas/Intrinsics/CurrentHealth.asset";
        private const string MaxHealthStat = "Assets/Settings/Schemas/Stats/Max Health.asset";
        private const string EssenceLink = "Assets/Settings/Schemas/EntityLinks/Essence Link.asset";
        private const string BarSourcePath = "Assets/Settings/Schemas/HealthBarSource.asset";
        private const string FeedbackProfilePath = "Assets/Settings/Schemas/HudFeedbackProfile.asset";
        private const string HostScene = "Assets/HudShowcase/HudShowcase.unity";
        private const string SubScene = "Assets/HudShowcase/HudShowcase_Sub.unity";
        private const int PlayerCount = 4;

        private const float MaxHealthRaw = 10000f; // Added raw → ValueFloat 100
        private static readonly int[] DemoCurrentHealth = { 100, 70, 25, 90 };

        [MenuItem("Showcase/Build HUD Showcase")]
        public static void Build()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var health = AssetDatabase.LoadAssetAtPath<IntrinsicSchemaObject>(HealthIntrinsic);
            var maxHealth = AssetDatabase.LoadAssetAtPath<StatSchemaObject>(MaxHealthStat);
            var essenceLink = AssetDatabase.LoadAssetAtPath<EntityLinkSchema>(EssenceLink);

            if (player == null || health == null || maxHealth == null || essenceLink == null)
            {
                Debug.LogError($"[HUD Showcase] Missing asset: player={player} health={health} maxHealth={maxHealth} essenceLink={essenceLink}");
                return;
            }

            FlashTextureGenerator.EnsureExists();
            ConfigureSettings(health, maxHealth, essenceLink);

            // Avoid the modal "Save Scene?" halt: NewScene(Single) prompts if an open scene is dirty. Save ONLY our own
            // showcase scenes first (throwaway, safe) — never blind-save the designer's scenes.
            for (var i = EditorSceneManager.sceneCount - 1; i >= 0; i--)
            {
                var open = EditorSceneManager.GetSceneAt(i);
                if (open.isDirty && open.path.StartsWith("Assets/HudShowcase/"))
                {
                    EditorSceneManager.SaveScene(open);
                }
            }

            var scene = PreloadSceneBuilder.CreateBootstrappedScene(HostScene, SubScene);
            if (!scene.IsValid)
            {
                Debug.LogError("[HUD Showcase] Failed to create bootstrapped scene (check Project Settings ▸ Vex ▸ Preload).");
                return;
            }

            for (var i = 0; i < PlayerCount; i++)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(player, scene.Sub);
                p.name = $"Player {i}";
                p.transform.position = new Vector3((i - 1.5f) * 2.5f, 0f, 0f);

                var ic = p.GetComponentInChildren<InputConsumerAuthoring>(true);
                if (ic != null)
                {
                    ic.PlayerId = (byte)i;
                    ic.Controllable = true;
                    EditorUtility.SetDirty(ic);
                }
                else
                {
                    Debug.LogError($"[HUD Showcase] '{p.name}' has no InputConsumerAuthoring — the HUD registry won't see it.");
                }

                // Seed demo health (the stock Player has no Max Health stat): Max Health = 100 + varied current.
                var essence = p.GetComponentInChildren<StatAuthoring>(true);
                if (essence != null)
                {
                    var stats = new List<StatModifierAuthoring>(essence.StatDefaults);
                    if (stats.All(s => s.Stat != maxHealth))
                    {
                        stats.Add(new StatModifierAuthoring { Stat = maxHealth, ModifyType = StatAuthoringType.Added, Value = MaxHealthRaw });
                        essence.StatDefaults = stats.ToArray();
                    }

                    var intrinsics = new List<IntrinsicDefault>(essence.IntrinsicDefaults);
                    var hp = DemoCurrentHealth[i % DemoCurrentHealth.Length];
                    var existing = intrinsics.FirstOrDefault(d => d.Intrinsic == health);
                    if (existing != null)
                    {
                        existing.Value = hp;
                    }
                    else
                    {
                        intrinsics.Add(new IntrinsicDefault { Intrinsic = health, Value = hp });
                        essence.IntrinsicDefaults = intrinsics.ToArray();
                    }

                    EditorUtility.SetDirty(essence);
                }
            }

            // The conductor: one GameObject that simulates combat by driving every actor's health through a damage→heal
            // cycle, so the HUD shows its FULL dynamic range (ghost chips on each hit, white flash, low-health red) live.
            var conductorGo = new GameObject("Combat Conductor");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(conductorGo, scene.Sub);
            var conductor = conductorGo.AddComponent<Vex.HudShowcase.CombatConductorAuthoring>();
            conductor.health = health;
            conductor.max = 100;
            EditorUtility.SetDirty(conductorGo);

            PreloadSceneBuilder.Save(scene);
            Debug.Log("[HUD Showcase] Built. Open " + HostScene + " and press Play. Edit BovineLabs ▸ Settings ▸ UI to change the HUD.");
        }

        // Ensure the project DataUISettings asset has the 4 health-bar rows + the "hud" panel. EditorSettingsUtility
        // creates the asset (and auto-adds it to the Default Settings Authoring) if missing.
        private static void ConfigureSettings(IntrinsicSchemaObject health, StatSchemaObject maxHealth, EntityLinkSchema essenceLink)
        {
            var barSource = GetOrCreateBarSource(health, maxHealth);

            var settings = EditorSettingsUtility.GetSettings<DataUISettings>();
            settings.Panels = new List<string> { "hud" };
            settings.Rows = new List<DataUISettings.Entry>();
            for (var i = 0; i < PlayerCount; i++)
            {
                settings.Rows.Add(new DataUISettings.Entry
                {
                    Source = new UISourceAuthoring { Mode = UISourceMode.Player, Player = i, Route = Target.Self, link = essenceLink },
                    Bar = barSource, // the SHARED source the world bar can also use
                    Kind = UIRowKind.Bar,
                    Label = $"P{i + 1}",
                    AlwaysVisible = true,
                });
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        // The shared bar definition (which Essence value/max). The world-space HealthBarStyle can reference this SAME
        // asset so the data is configured once across world + screen.
        private static EssenceBarSource GetOrCreateBarSource(IntrinsicSchemaObject health, StatSchemaObject maxHealth)
        {
            var source = AssetDatabase.LoadAssetAtPath<EssenceBarSource>(BarSourcePath);
            if (source == null)
            {
                source = ScriptableObject.CreateInstance<EssenceBarSource>();
                AssetDatabase.CreateAsset(source, BarSourcePath);
            }

            source.current = health;
            source.max = maxHealth;
            source.feedback = GetOrCreateProfile();
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
            return source;
        }

        // The shared feedback profile (trail behaviour). Created as Both (slider for heal + chip for damage); the designer
        // can retune it freely afterward — the builder only sets the mode on first creation.
        private static BarFeedbackProfile GetOrCreateProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<BarFeedbackProfile>(FeedbackProfilePath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<BarFeedbackProfile>();
                p.trailMode = BovineLabs.Timeline.UI.Data.TrailMode.Both;
                AssetDatabase.CreateAsset(p, FeedbackProfilePath);
                EditorUtility.SetDirty(p);
                AssetDatabase.SaveAssetIfDirty(p);
            }

            return p;
        }
    }
}
