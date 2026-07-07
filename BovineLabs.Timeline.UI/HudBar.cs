using BovineLabs.Timeline.UI.Data;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// A DUMB, data-bindable bar PRIMITIVE — zero logic, infers nothing. It renders the STRUCTURE values it is told:
    /// <list type="bullet">
    /// <item><see cref="value"/> — current/max fill.</item>
    /// <item><see cref="ghost"/> — the GHOST SLIDER (delayed damage / heal-lead). The band between fill and ghost is
    /// drawn as a CROPPED slice of the blade (full-size, windowed — never squashed), colored by which side the ghost is
    /// on (lost = warm, gained = green). The game drives the ghost value's lag; the bar only renders it.</item>
    /// <item><see cref="locked"/> — a hatched locked band (curse) at the high end; a drop under it is never damage.</item>
    /// <item>flash — a full-bleed overlay pulse (damage/heal feedback). Its visual comes from the project's
    /// <c>.vex-bar__flash</c> USS class; the bar only maps a 0..1 amount to opacity, capped by <c>--vex-flash-max</c>.</item>
    /// </list>
    /// All look is USS; this only maps numbers to geometry.
    /// </summary>
    [UxmlElement]
    public partial class HudBar : VisualElement
    {
        private static readonly CustomStyleProperty<Color> LowTintProp = new("--vex-low-tint");
        private static readonly CustomStyleProperty<float> FlashMaxProp = new("--vex-flash-max");

        private readonly VisualElement track;
        private readonly VisualElement fillClip;
        private readonly VisualElement fillInner;
        private readonly VisualElement ghostClip;
        private readonly VisualElement ghostInner;
        private readonly VisualElement lockedClip;
        private readonly VisualElement lockedInner;
        private readonly VisualElement chipClip;
        private readonly VisualElement chipInner;
        private readonly VisualElement flashOverlay;

        private float m_Value;
        private float m_Ghost;
        private float m_Locked;
        private float m_Flash;
        private float m_FlashMax = 0.45f;
        private float m_LowThreshold = 0.5f;
        private float m_TrackWidth;
        private Color m_LowColor = new Color(1f, 0.35f, 0.27f, 1f);
        private bool m_RightToLeft;

        // Accumulating chip trail (TOLD via AddChip; held then eased-collapse). Config is set by the driver from the profile.
        private float m_ChipHi;
        private bool m_Collapsing;
        private TrailMode m_TrailMode = TrailMode.Both;
        private bool m_Accumulate = true;
        private bool m_Fade = true;
        private float m_HoldMs = 400f;
        private float m_DrainMs = 500f;
        private float m_MinDrainMs = 120f;
        private float m_DrainRate = 1.5f;
        private float m_MinChipFrac = 0.005f;
        private EaseId m_DrainEase = EaseId.OutCubic;
        private IVisualElementScheduledItem m_HoldItem;
        private UnityEngine.UIElements.Experimental.ValueAnimation<float> m_Collapse;

        public HudBar()
        {
            this.AddToClassList("vex-bar");
            this.pickingMode = PickingMode.Ignore;

            this.track = Part("vex-bar__track");
            this.track.style.position = Position.Relative;
            this.track.style.overflow = Overflow.Hidden;
            this.Add(this.track);

            this.fillClip = Clip("vex-bar__fill");
            this.fillInner = Part("vex-bar__fill-inner");
            this.fillClip.Add(this.fillInner);
            this.track.Add(this.fillClip);

            // ghost slider — drawn ABOVE the fill so a heal-lead band shows over the gained fill; a damage band shows in
            // the spent region beside the fill. Windowed (clip + full-width inner) so the blade is cropped, not squashed.
            this.ghostClip = Clip("vex-bar__ghost");
            this.ghostInner = Part("vex-bar__ghost-inner");
            this.ghostClip.Add(this.ghostInner);
            this.track.Add(this.ghostClip);

            // chip trail — windowed like the others (cropped, never squashed). Drawn above the slider; below locked/frame.
            this.chipClip = Clip("vex-bar__chip");
            this.chipInner = Part("vex-bar__chip-inner");
            this.chipClip.Add(this.chipInner);
            this.track.Add(this.chipClip);

            // locked band is ALSO windowed (Clip + inner) so the blade is cropped to the [1-lk,1] tip, never squashed.
            this.lockedClip = Clip("vex-bar__locked");
            this.lockedInner = Part("vex-bar__locked-inner");
            this.lockedClip.Add(this.lockedInner);
            this.track.Add(this.lockedClip);

            this.track.Add(Fill("vex-bar__frame"));

            // flash overlay — added AFTER the frame so it draws on top; full-bleed. The visual (color/image) is the
            // project's .vex-bar__flash USS; the bar only drives its opacity from the told flash amount.
            this.flashOverlay = Fill("vex-bar__flash");
            this.track.Add(this.flashOverlay);

            this.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                this.m_TrackWidth = this.track.resolvedStyle.width;
                this.ApplyStructure();
            });
            this.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                var changed = false;
                if (e.customStyle.TryGetValue(LowTintProp, out var c))
                {
                    this.m_LowColor = c;
                    changed = true;
                }

                if (e.customStyle.TryGetValue(FlashMaxProp, out var f))
                {
                    this.m_FlashMax = f;
                    changed = true;
                }

                if (changed)
                {
                    this.ApplyStructure();
                }
            });

            // Detach-safe collapse latch: a panel unmount / clip end / scene reload kills the panel-scheduled collapse
            // animation WITHOUT firing OnCompleted, so m_Collapsing would stay latched true forever and ApplyChip would
            // early-return forever (frozen chip on re-attach). Tear the trail state down here so re-attach starts clean.
            this.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                this.m_Collapse?.Stop();
                this.m_Collapse = null;
                this.m_Collapsing = false;
                this.m_HoldItem?.Pause();
                this.m_HoldItem = null;
                this.m_ChipHi = this.m_Value;
                this.chipClip.style.translate = new Translate(0f, 0f);
                this.chipClip.style.opacity = 1f;
            });
            this.RegisterCallback<AttachToPanelEvent>(_ => this.ApplyStructure());

            this.ApplyStructure();
        }

        [UxmlAttribute]
        [CreateProperty]
        public float value
        {
            get => this.m_Value;
            set { this.m_Value = value; this.ApplyStructure(); }
        }

        [UxmlAttribute]
        [CreateProperty]
        public float ghost
        {
            get => this.m_Ghost;
            set { this.m_Ghost = value; this.ApplyStructure(); }
        }

        [UxmlAttribute]
        [CreateProperty]
        public float locked
        {
            get => this.m_Locked;
            set { this.m_Locked = value; this.ApplyStructure(); }
        }

        [UxmlAttribute]
        [CreateProperty]
        public float flash
        {
            get => this.m_Flash;
            set { this.m_Flash = value; this.ApplyStructure(); }
        }

        [UxmlAttribute]
        public float lowThreshold
        {
            get => this.m_LowThreshold;
            set { this.m_LowThreshold = value; this.ApplyStructure(); }
        }

        [UxmlAttribute]
        public bool rightToLeft
        {
            get => this.m_RightToLeft;
            set { this.m_RightToLeft = value; this.ApplyStructure(); }
        }

        /// <summary>
        /// Set fill / ghost / locked / flash in ONE shot and apply the layout ONCE. The driver calls this instead of the
        /// four property setters, which would each trigger a full <see cref="ApplyStructure"/> pass (4× the style churn
        /// per bar per frame). The properties remain for UXML authoring and data bindings.
        /// </summary>
        public void SetState(float fill, float ghost, float locked, float flash)
        {
            this.m_Value = fill;
            this.m_Ghost = ghost;
            this.m_Locked = locked;
            this.m_Flash = flash;
            this.ApplyStructure();
        }

        private void ApplyStructure()
        {
            var w = this.m_TrackWidth;
            var laidOut = w > 0f;
            var fill = Saturate(this.m_Value);
            var ghost = Saturate(this.m_Ghost);

            if (!laidOut)
            {
                // First-layout flicker guard: before the first GeometryChangedEvent the track has no resolved width, so
                // the windowed inners would collapse to zero for a frame. Keep every clip hidden until width is known;
                // they re-show on the first GeometryChangedEvent which re-runs this pass with w > 0.
                this.fillClip.style.display = DisplayStyle.None;
                this.ghostClip.style.display = DisplayStyle.None;
                this.chipClip.style.display = DisplayStyle.None;
                this.lockedClip.style.display = DisplayStyle.None;
            }
            else
            {
                // fill (reveal, never squash): full-width inner inside a %-clip pinned to the origin side.
                this.fillClip.style.display = DisplayStyle.Flex;
                AnchorClip(this.fillClip, this.fillInner, 0f, fill, w, this.m_RightToLeft);

                // ghost slider: the band between fill and ghost, cropped from the blade. healing = ghost sits below fill.
                // Shown only when the trail mode includes the slider (chip is the separate accumulating band below).
                var lo = math.min(fill, ghost);
                var hi = math.max(fill, ghost);
                var healing = ghost < fill - 0.001f;
                var showGhost = (this.m_TrailMode == TrailMode.GhostSlider || this.m_TrailMode == TrailMode.Both) && (hi - lo) > 0.003f;
                this.ghostClip.style.display = showGhost ? DisplayStyle.Flex : DisplayStyle.None;
                if (showGhost)
                {
                    AnchorClip(this.ghostClip, this.ghostInner, lo, hi, w, this.m_RightToLeft);
                    this.EnableInClassList("vex-bar--healing", healing);
                }

                this.ApplyChip(); // the chip window [fill, chipHi] tracks the moving fill

                // locked band (curse): the cropped blade tip [1-lk,1] at the HIGH end. Structure, never damage.
                var lk = Saturate(this.m_Locked);
                this.lockedClip.style.display = lk > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                if (lk > 0f)
                {
                    AnchorClip(this.lockedClip, this.lockedInner, 1f - lk, 1f, w, this.m_RightToLeft);
                }
            }

            // flash overlay: full-bleed pulse independent of track width. Opacity is the told amount scaled by the
            // USS-driven cap (--vex-flash-max, default 0.45). Hidden entirely when negligible so it never eats picking
            // (it already ignores picking) or draws a 0-opacity layer.
            var flashAmt = Saturate(this.m_Flash);
            if (flashAmt > 0.001f)
            {
                this.flashOverlay.style.display = DisplayStyle.Flex;
                this.flashOverlay.style.opacity = flashAmt * this.m_FlashMax;
            }
            else
            {
                this.flashOverlay.style.display = DisplayStyle.None;
            }

            // continuous low-health recolor (works on tinted blades; on a pure-red blade it reads via fill amount).
            var lowAmt = this.m_LowThreshold > 0f ? Saturate(1f - (fill / this.m_LowThreshold)) : 0f;
            this.fillInner.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, this.m_LowColor, lowAmt);
            this.EnableInClassList("vex-bar--low", this.m_LowThreshold > 0f && fill <= this.m_LowThreshold);
            this.EnableInClassList("vex-bar--rtl", this.m_RightToLeft);
        }

        /// <summary>Driver sets the trail behaviour from the baked profile. Called before AddChip.</summary>
        public void SetTrailConfig(TrailMode mode, bool accumulate, float holdMs, float drainMs, float minDrainMs, EaseId ease, bool fade, float minChipFrac, float drainRate)
        {
            this.m_TrailMode = mode;
            this.m_Accumulate = accumulate;
            this.m_HoldMs = math.max(0f, holdMs);
            this.m_DrainMs = math.max(0f, drainMs); // keep 0 so drainRate can take over in ComputeCollapseDurationMs.
            this.m_MinDrainMs = math.max(1f, minDrainMs);
            this.m_DrainEase = ease;
            this.m_Fade = fade;
            this.m_MinChipFrac = math.max(0f, minChipFrac);
            this.m_DrainRate = math.max(0f, drainRate);
        }

        /// <summary>TOLD a damage chip of <paramref name="amountFrac"/> (fraction of max). Accumulates the held band
        /// (high-water) and arms the hold; the eased collapse runs on timeout. The bar never infers this — the driver
        /// calls it from an explicit signal.</summary>
        public void AddChip(float amountFrac)
        {
            if (amountFrac < this.m_MinChipFrac)
            {
                return;
            }

            this.m_Collapse?.Stop();
            this.m_Collapse = null;
            this.m_Collapsing = false;
            this.chipClip.style.translate = new Translate(0f, 0f);
            this.chipClip.style.opacity = 1f;

            var top = Saturate(this.m_Value + amountFrac);
            this.m_ChipHi = Saturate(this.m_Accumulate ? math.max(this.m_ChipHi, math.max(top, this.m_Value)) : top);

            this.m_HoldItem?.Pause();
            this.m_HoldItem = this.schedule.Execute(this.Collapse).StartingIn((long)this.m_HoldMs);
            this.ApplyChip();
        }

        // The "swish": the held chunk DETACHES and falls (frozen geometry, eased translate + fade) — a clean drop-off.
        private void Collapse()
        {
            this.m_HoldItem = null;
            if (this.m_ChipHi <= this.m_Value + this.m_MinChipFrac)
            {
                this.m_ChipHi = this.m_Value;
                this.ApplyChip();
                return;
            }

            // freeze the chip at its held window, then animate the fall (it is detached, so it does not track fill)
            AnchorClip(this.chipClip, this.chipInner, Saturate(this.m_Value), Saturate(this.m_ChipHi), this.m_TrackWidth, this.m_RightToLeft);
            this.chipClip.style.display = DisplayStyle.Flex;
            this.m_Collapsing = true;

            var band = this.m_ChipHi - this.m_Value;
            var dur = ComputeCollapseDurationMs(band, this.m_DrainMs, this.m_MinDrainMs, this.m_DrainRate);
            var h = this.track.resolvedStyle.height;
            h = h > 0f ? h : 20f;
            this.m_Collapse = this.experimental.animation.Start(0f, 1f, dur, (_, p) =>
            {
                this.chipClip.style.translate = new Translate(0f, h * p * 1.4f);
                this.chipClip.style.opacity = this.m_Fade ? 1f - p : 1f;
            }).Ease(VexEase.Get(this.m_DrainEase));

            this.m_Collapse.OnCompleted(() =>
            {
                this.m_Collapsing = false;
                this.m_ChipHi = this.m_Value;
                this.chipClip.style.translate = new Translate(0f, 0f);
                this.chipClip.style.opacity = 1f;
                this.ApplyChip();
            });
            this.m_Collapse.KeepAlive();
        }

        /// <summary>
        /// Eased-collapse duration in ms. Honors the <see cref="BarFeedbackProfile"/> tooltip "0 = use drainRate": when
        /// <paramref name="drainMs"/> is 0 and a positive <paramref name="drainRate"/> (units/sec) is set, the duration
        /// is <c>band / rate</c> seconds, clamped to [minDrainMs, 5000ms]; otherwise it is <c>max(minDrainMs, drainMs)</c>.
        /// Pure so it can be unit-tested without a panel.
        /// </summary>
        public static int ComputeCollapseDurationMs(float band, float drainMs, float minDrainMs, float drainRate)
        {
            float dur;
            if (drainMs <= 0f && drainRate > 0f)
            {
                dur = math.clamp((math.max(0f, band) / drainRate) * 1000f, minDrainMs, 5000f);
            }
            else
            {
                dur = math.max(minDrainMs, drainMs);
            }

            return (int)dur;
        }

        private void ApplyChip()
        {
            if (this.m_Collapsing)
            {
                return; // the detached chunk is mid-fall — its geometry is frozen
            }

            var show = (this.m_TrailMode == TrailMode.DropChip || this.m_TrailMode == TrailMode.Both)
                && (this.m_ChipHi - this.m_Value) > this.m_MinChipFrac;
            this.chipClip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                AnchorClip(this.chipClip, this.chipInner, Saturate(this.m_Value), Saturate(this.m_ChipHi), this.m_TrackWidth, this.m_RightToLeft);
            }
        }

        // Position a [lo,hi] window over the blade: the clip spans [lo,hi]%, the inner is the FULL-width blade shifted so
        // its [lo,hi] slice lands in the clip — the texture is cropped to the window, never scaled into it.
        private static void AnchorClip(VisualElement clip, VisualElement inner, float lo, float hi, float w, bool rtl)
        {
            clip.style.top = 0;
            clip.style.bottom = 0;
            clip.style.width = Length.Percent((hi - lo) * 100f);
            inner.style.position = Position.Absolute;
            inner.style.top = 0;
            inner.style.bottom = 0;

            if (w > 0f)
            {
                inner.style.width = w;
            }

            if (rtl)
            {
                clip.style.right = Length.Percent(lo * 100f);
                clip.style.left = StyleKeyword.Auto;
                inner.style.right = new Length(w > 0f ? -(lo * w) : 0f);
                inner.style.left = StyleKeyword.Auto;
            }
            else
            {
                clip.style.left = Length.Percent(lo * 100f);
                clip.style.right = StyleKeyword.Auto;
                inner.style.left = new Length(w > 0f ? -(lo * w) : 0f);
                inner.style.right = StyleKeyword.Auto;
            }
        }

        private static VisualElement Part(string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            return e;
        }

        private static VisualElement Clip(string cls)
        {
            var e = Part(cls);
            e.style.position = Position.Absolute;
            e.style.top = 0;
            e.style.bottom = 0;
            e.style.overflow = Overflow.Hidden;
            return e;
        }

        private static VisualElement Fill(string cls)
        {
            var e = Part(cls);
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.right = 0;
            e.style.top = 0;
            e.style.bottom = 0;
            return e;
        }

        // NaN-safe: ordered so a NaN input falls to 0 (empty), never 1 (a phantom full bar).
        private static float Saturate(float v) => v > 0f ? (v < 1f ? v : 1f) : 0f;
    }
}
