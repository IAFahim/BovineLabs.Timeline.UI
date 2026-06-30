using BovineLabs.Essence.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>
    /// Singleton config for the always-on screen HUD: which Essence keys are read as each player's health.
    /// The screen-space counterpart of com.vex.healthbar's HealthBarSource (Stat max + Intrinsic current) — use the
    /// SAME schema assets so the world bar and the HUD stay in sync. Baked from <see cref="HudConfigAuthoring"/>.
    /// </summary>
    public struct HudConfig : IComponentData
    {
        /// <summary>Intrinsic holding each player's CURRENT health (raw value).</summary>
        public IntrinsicKey Health;

        /// <summary>Stat holding each player's MAX health (×100 fixed point → read via ValueFloat).</summary>
        public StatKey HealthMax;
    }
}
