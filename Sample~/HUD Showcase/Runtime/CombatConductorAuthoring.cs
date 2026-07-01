using BovineLabs.Essence.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Vex.HudShowcase
{
    /// <summary>
    /// The showcase "conductor" — one GameObject that stands in for the game's combat, PASSING explicit signals to the
    /// (dumb) bar. At each discrete action it changes the health intrinsic AND appends a matching BarFeedbackEvent with
    /// its amount, so the bar plays a damage chip or heal surge because it was TOLD, never because it diffed the fill.
    /// </summary>
    public sealed class CombatConductorAuthoring : MonoBehaviour
    {
        [Tooltip("The intrinsic to drive (CurrentHealth).")]
        public IntrinsicSchemaObject health;

        [Tooltip("Value the intrinsic heals back to.")]
        public int max = 100;

        [Tooltip("Seconds between discrete combat actions (a hit, or a heal when low).")]
        public float interval = 1.3f;

        [Tooltip("Phase offset between actors so they desync.")]
        public float phasePerActor = 0.7f;

        private sealed class ConductorBaker : Baker<CombatConductorAuthoring>
        {
            public override void Bake(CombatConductorAuthoring a)
            {
                if (a.health == null)
                {
                    return;
                }

                this.DependsOn(a.health);
                var e = this.GetEntity(TransformUsageFlags.None);
                this.AddComponent(e, new CombatConductor
                {
                    HealthKey = (ushort)a.health.Key,
                    Max = math.max(1, a.max),
                    Interval = math.max(0.2f, a.interval),
                    PhasePerActor = a.phasePerActor,
                });
            }
        }
    }

    public struct CombatConductor : IComponentData
    {
        public ushort HealthKey;
        public int Max;
        public float Interval;
        public float PhasePerActor;
    }
}
