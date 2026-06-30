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
                            if (ghost - fill < 1e-3f)
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

            var notFull = keepVisibleWhileNotFull && fill < 1f - 1e-3f;
            var recentlyChanged = showOnHealthChange && (autoHideDelay <= 0f || idle < autoHideDelay);
            return notFull || recentlyChanged ? 1f : 0f;
        }

        /// <summary>Move alpha toward target over fadeIn (rising) / fadeOut (falling) seconds.</summary>
        public static float StepAlpha(float alpha, float target, float fadeIn, float fadeOut, float dt)
        {
            var dur = target > alpha ? fadeIn : fadeOut;
            var step = dur <= 1e-4f ? 1f : dt / dur;
            return MoveTowards(alpha, target, step);
        }

        public static float MoveTowards(float a, float b, float maxDelta)
        {
            var diff = b - a;
            if (math.abs(diff) <= maxDelta)
            {
                return b;
            }

            return a + math.sign(diff) * maxDelta;
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
