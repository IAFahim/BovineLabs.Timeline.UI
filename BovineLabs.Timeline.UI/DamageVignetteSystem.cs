using BovineLabs.Anchor;
using BovineLabs.Anchor.Audio;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// Drives a screen-edge damage vignette (and a damage audio sting) from the shared <see cref="BarFeedbackEvent"/>
    /// stream. For every player resolved by the <see cref="ControllableRegistry"/> it reads this frame's DamageChip/Crit
    /// events on that player's entity (non-destructively — the <see cref="BarFeedbackDrainSystem"/> owns removal),
    /// ramps a per-player intensity, decays it over time, and pushes the result onto the vignette element(s).
    ///
    /// PLAYER-AGNOSTIC: the player count is never hardcoded — it iterates the registry (1..N). It prefers a per-player
    /// element <c>damage-vignette-{player}</c>, positioning it to that player's viewport via <see cref="HudViewport"/>
    /// (full-screen today under one shared camera, split-screen later); if only the shared <c>damage-vignette</c> exists,
    /// every player's damage drives that one full-screen overlay at the strongest current intensity.
    /// Managed <see cref="SystemBase"/> (touches <see cref="AnchorApp.Current"/>); never Burst.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial class DamageVignetteSystem : SystemBase
    {
        public const string SharedElementName = "damage-vignette";
        public const string PerPlayerElementPrefix = "damage-vignette-";

        private const float MaxOpacity = 0.65f;   // opacity at full intensity
        private const float HitToIntensity = 0.02f; // raw damage amount → intensity ramp (50 dmg ≈ full)
        private const float FadeSeconds = 0.6f;     // time to fully fade from max

        private float[] intensity; // per-player 0..1; index = PlayerId slot

        protected override void OnCreate()
        {
            // Sized to the registry's player capacity (byte PlayerId → 256 slots); no magic player count.
            this.intensity = new float[256];
            this.RequireForUpdate<ControllableRegistry>();
        }

        protected override void OnUpdate()
        {
            var app = AnchorApp.Current;
            if (app == null)
            {
                return;
            }

            var root = app.RootVisualElement;
            if (root == null)
            {
                return;
            }

            var registry = SystemAPI.GetSingleton<ControllableRegistry>();
            if (!registry.ByPlayer.IsCreated)
            {
                return;
            }

            var currentFrame = SystemAPI.TryGetSingleton<BarFeedbackFrame>(out var bff) ? bff.Frame : 0u;

            var feedback = SystemAPI.GetBufferLookup<BarFeedbackEvent>(true);
            this.EntityManager.CompleteDependencyBeforeRO<BarFeedbackEvent>();

            var dt = math.min((float)SystemAPI.Time.DeltaTime, 0.1f);
            var decay = dt / FadeSeconds;

            var sharedElement = root.Q<VisualElement>(SharedElementName);
            var sharedMax = 0f;

            var count = math.min(this.intensity.Length, registry.ByPlayer.Length);
            for (var p = 0; p < count; p++)
            {
                var entity = registry.ByPlayer[p];

                var hit = 0;
                if (entity != Entity.Null && currentFrame != 0 && feedback.TryGetBuffer(entity, out var fb))
                {
                    for (var i = 0; i < fb.Length; i++)
                    {
                        var evt = fb[i];
                        if (evt.Frame != currentFrame)
                        {
                            continue;
                        }

                        if (evt.Kind == FeedbackKind.DamageChip || evt.Kind == FeedbackKind.Crit)
                        {
                            // Cast to long before abs: math.abs(int.MinValue) overflows.
                            hit += (int)math.min(math.abs((long)evt.Amount), 100000);
                        }
                    }
                }

                var value = this.intensity[p];
                if (hit > 0)
                {
                    value = math.saturate(value + (hit * HitToIntensity));

                    // One sting per damage-frame per player; profile "damage" is authored in AnchorAudioSettings
                    // (absent → silent no-op).
                    AnchorAudio.Play(UiAudioCues.Damage, AnchorAudioCue.Activate, AnchorAudioCueOverride.Inherit);
                }
                else
                {
                    value = math.max(0f, value - decay);
                }

                this.intensity[p] = value;

                // Per-player overlay, if authored: position to this player's viewport and drive its own opacity.
                var perPlayer = root.Q<VisualElement>(PerPlayerElementPrefix + p);
                if (perPlayer != null)
                {
                    ApplyViewport(perPlayer, HudViewport.Resolve(p));
                    perPlayer.style.opacity = value * MaxOpacity;
                }
                else if (entity != Entity.Null)
                {
                    // Falls through to the shared overlay — track the strongest live player's intensity for it.
                    sharedMax = math.max(sharedMax, value);
                }
            }

            if (sharedElement != null)
            {
                sharedElement.style.opacity = sharedMax * MaxOpacity;
            }
        }

        private static void ApplyViewport(VisualElement element, UnityEngine.Rect normalized)
        {
            // Normalized (0..1, origin bottom-left) → UITK percent (origin top-left).
            element.style.left = Length.Percent(normalized.xMin * 100f);
            element.style.width = Length.Percent(normalized.width * 100f);
            element.style.top = Length.Percent((1f - normalized.yMax) * 100f);
            element.style.height = Length.Percent(normalized.height * 100f);
        }
    }
}
