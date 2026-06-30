using BovineLabs.Essence.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.UI.Authoring
{
    /// <summary>
    /// Place ONE in a (sub)scene to enable the always-on screen HUD. Picks which Essence keys the HUD reads as each
    /// player's health — use the SAME schema assets your pawns' StatAuthoring/IntrinsicAuthoring use (and the same the
    /// world-space com.vex.healthbar uses) so the screen HUD and world bars stay in sync.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BovineLabs/Timeline UI/HUD Config")]
    public class HudConfigAuthoring : MonoBehaviour
    {
        [Tooltip("Intrinsic holding each player's CURRENT health (raw value).")]
        public IntrinsicSchemaObject health;

        [Tooltip("Stat holding each player's MAX health.")]
        public StatSchemaObject maxHealth;

        private class Baker : Baker<HudConfigAuthoring>
        {
            public override void Bake(HudConfigAuthoring authoring)
            {
                if (authoring.health == null || authoring.maxHealth == null)
                {
                    Debug.LogError(
                        $"HudConfig '{authoring.name}': assign both the Health intrinsic and the Max Health stat.",
                        authoring);
                    return;
                }

                this.DependsOn(authoring.health);
                this.DependsOn(authoring.maxHealth);

                var entity = this.GetEntity(TransformUsageFlags.None);
                this.AddComponent(entity, new HudConfig
                {
                    Health = authoring.health,
                    HealthMax = authoring.maxHealth,
                });
            }
        }
    }
}
