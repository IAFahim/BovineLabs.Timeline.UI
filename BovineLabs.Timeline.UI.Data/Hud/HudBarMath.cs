using Unity.Mathematics;

namespace BovineLabs.Timeline.UI.Data
{
    public enum HudGhostMode : byte
    {
        Off = 0,
        FromStat = 1,
        FromIntrinsic = 2,
        ComputedLerp = 3,
    }

    /// <summary>
    /// Per-row presentation state the driver advances every frame through <see cref="HudBarMath.AdvanceSlot"/>. It is
    /// the persistent memory the pure kernel needs (ghost catch-up, hold/flash decay, idle-since-change). Blittable so
    /// the driver can hold a <c>NativeArray&lt;HudSlotState&gt;</c> sized to the baked entry count.
    /// </summary>
    public struct HudSlotState
    {
        public float Ghost;
        public float HoldTimer;
        public float Flash;
        public float Idle;
        public float LastFill;
        public bool Init;
    }

    /// <summary>
    /// Pure, deterministic HUD-bar behaviour — fill / ghost(chip) / flash / visibility-fade math. This is a faithful
    /// PORT of the world-space <c>Vex.HealthBar</c> math (HealthBarMath / HealthBarGhost / HealthBarVisibility) so the
    /// screen HUD behaves IDENTICALLY to the in-world bar ("everything the world bar does, the same way") without
    /// coupling Timeline.UI to the game-specific com.vex.healthbar package. Keep the two in sync if the world-bar math
    /// changes. Unit-testable; no Unity/ECS dependencies.
    /// </summary>
    public static class HudBarMath
    {
        /// <summary>Fraction filled, clamped 0..1. Zero/negative max → empty (no divide-by-zero).</summary>
        public static float Fill(float current, float max) => max > 0f ? math.saturate(current / max) : 0f;

        /// <summary>
        /// Advance ONE bar slot's presentation state for a frame and emit the renderable ghost/flash/alpha. This is the
        /// single behaviour kernel: the driver feeds baked per-row config + the live fill, the kernel owns the state.
        /// Pure — no Unity/ECS/UI. <paramref name="externalGhost"/> is the FromStat/FromIntrinsic ghost as a fraction;
        /// <paramref name="healFrac"/> (from a HealSurge feedback event) forces the ghost BELOW fill so the green
        /// gained-band draws; <paramref name="flashEvent"/> (from a Flash feedback event) pins flash to 1.
        /// </summary>
        public static void AdvanceSlot(ref HudSlotState s, HudGhostMode mode, float fill, float externalGhost, float dt,
            float ghostDelay, float ghostSpeed, bool flashOnDamage, float flashDecay, bool flashEvent, float healFrac,
            bool alwaysVisible, bool keepVisibleWhileNotFull, bool showOnHealthChange, float autoHideDelay,
            out float ghost, out float flash, out float alpha, out bool damaged)
        {
            const float changeEps = 1e-4f;

            if (!s.Init)
            {
                // First sight: snap to fill so a freshly-resolved slot shows no phantom band / flash / hide.
                s.Ghost = fill;
                s.LastFill = fill;
                s.Init = true;
            }

            damaged = fill < s.LastFill - changeEps;
            var changed = damaged || fill > s.LastFill + changeEps;
            s.Idle = changed ? 0f : s.Idle + dt;

            ghost = GhostStep(mode, fill, externalGhost, damaged, dt, ghostDelay, ghostSpeed, ref s.Ghost, ref s.HoldTimer);

            // Heal lead: GhostStep keeps ghost >= fill (the damage side); a heal is the ghost sitting BELOW fill.
            if (healFrac > 0f)
            {
                ghost = math.saturate(fill - healFrac);
            }

            s.Flash = Flash(flashOnDamage, damaged, s.Flash, dt, flashDecay);
            if (flashEvent)
            {
                s.Flash = 1f;
            }

            flash = s.Flash;
            alpha = TargetAlpha(alwaysVisible, 0, keepVisibleWhileNotFull, showOnHealthChange, fill, s.Idle, autoHideDelay);

            s.LastFill = fill;
        }

        /// <summary>Ghost/chip band (always &gt;= fill). ComputedLerp = hold then frame-rate-independent exp catch-up.</summary>
        public static float GhostStep(HudGhostMode mode, float fill, float externalGhost, bool damaged, float dt,
            float delay, float speed, ref float ghost, ref float holdTimer)
        {
            switch (mode)
            {
                case HudGhostMode.Off:
                    ghost = fill;
                    holdTimer = 0f;
                    return fill;

                case HudGhostMode.FromStat:
                case HudGhostMode.FromIntrinsic:
                    ghost = math.max(fill, math.saturate(externalGhost));
                    holdTimer = 0f;
                    return ghost;

                default: // ComputedLerp
                    if (fill >= ghost)
                    {
                        ghost = fill;
                        holdTimer = 0f;
                        return ghost;
                    }

                    if (damaged)
                    {
                        holdTimer = 0f;
                    }

                    holdTimer += dt;
                    if (holdTimer >= delay)
                    {
                        if (speed <= 0f)
                        {
                            ghost = fill;
                        }
                        else
                        {
                            var t = 1f - math.exp(-speed * dt);
                            ghost = math.lerp(ghost, fill, t);
                            if (ghost - fill < BarFeedbackDefaults.FullEpsilon)
                            {
                                ghost = fill;
                            }
                        }
                    }

                    return math.max(ghost, fill);
            }
        }

        /// <summary>1 on damage, otherwise decays toward 0 over flashDecay seconds.</summary>
        public static float Flash(bool flashOnDamage, bool damaged, float flash, float dt, float flashDecay)
        {
            if (flashOnDamage && damaged)
            {
                return 1f;
            }

            return math.max(0f, flash - dt / math.max(flashDecay, 1e-4f));
        }

        /// <summary>Target alpha 0/1. visLatch: 0=auto, 1=shown(event), 2=hidden(event).</summary>
        public static float TargetAlpha(bool alwaysVisible, byte visLatch,
            bool keepVisibleWhileNotFull, bool showOnHealthChange, float fill, float idle, float autoHideDelay)
        {
            if (alwaysVisible)
            {
                return 1f;
            }

            if (visLatch == 2)
            {
                return 0f;
            }

            if (visLatch == 1)
            {
                return 1f;
            }

            var notFull = keepVisibleWhileNotFull && fill < 1f - BarFeedbackDefaults.FullEpsilon;
            var recentlyChanged = showOnHealthChange && (autoHideDelay <= 0f || idle < autoHideDelay);
            return notFull || recentlyChanged ? 1f : 0f;
        }

        /// <summary>Low-health alpha throb (multiplier ~[1-amp, 1]); phase advances only while low. 1 when not low.</summary>
        public static float LowPulse(float fill, float pulseThreshold, float pulseAmp, float pulseSpeed, float time)
        {
            if (pulseAmp <= 0f || pulseThreshold <= 0f || fill > pulseThreshold)
            {
                return 1f;
            }

            var s = 0.5f + 0.5f * math.sin(time * pulseSpeed);
            return 1f - pulseAmp * (1f - s);
        }
    }
}
