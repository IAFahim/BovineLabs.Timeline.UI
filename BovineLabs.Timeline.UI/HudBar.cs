using Unity.Properties;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// A reusable, data-bindable health/progress bar PRIMITIVE for UXML + USS — the building block the HUD is made of.
    /// It contains ZERO styling: it only maps three bound floats to geometry — <see cref="value"/>/<see cref="ghost"/>
    /// drive the fill/chip widths and <see cref="flash"/> drives a flash overlay's opacity. EVERYTHING visual (colors,
    /// height, corners, the blade <c>background-image</c>, the flash color, the low-health look) is set in USS on the
    /// sub-element classes below. Bind it in UXML, e.g.
    /// <code>&lt;hud:HudBar&gt;&lt;Bindings&gt;&lt;ui:DataBinding property="value" data-source-path="Players[0].Fill" update-trigger="EveryUpdate"/&gt;…&lt;/Bindings&gt;&lt;/hud:HudBar&gt;</code>.
    /// USS hooks: <c>.vex-bar</c> (root), <c>__track __ghost __ghost-inner __fill __fill-inner __flash __frame</c>, and
    /// the state class <c>.vex-bar--low</c> (toggled when value ≤ <see cref="lowThreshold"/>) + <c>--rtl</c>.
    /// The fill/ghost use a fixed-width inner inside a width-%% clip so a background-image REVEALS (never squashes).
    /// </summary>
    [UxmlElement]
    public partial class HudBar : VisualElement
    {
        private readonly VisualElement track;
        private readonly VisualElement ghostClip;
        private readonly VisualElement ghostInner;
        private readonly VisualElement fillClip;
        private readonly VisualElement fillInner;
        private readonly VisualElement flashOverlay;

        private float m_Value;
        private float m_Ghost;
        private float m_Flash;
        private float m_LowThreshold = 0.3f;
        private bool m_RightToLeft;

        public HudBar()
        {
            this.AddToClassList("vex-bar");
            this.pickingMode = PickingMode.Ignore;

            this.track = Part("vex-bar__track");
            this.track.style.position = Position.Relative;
            this.track.style.overflow = Overflow.Hidden;
            this.Add(this.track);

            this.ghostClip = Clip("vex-bar__ghost");
            this.ghostInner = Part("vex-bar__ghost-inner");
            this.ghostClip.Add(this.ghostInner);
            this.track.Add(this.ghostClip);

            this.fillClip = Clip("vex-bar__fill");
            this.fillInner = Part("vex-bar__fill-inner");
            this.fillClip.Add(this.fillInner);
            this.track.Add(this.fillClip);

            this.flashOverlay = Fill("vex-bar__flash");
            this.track.Add(this.flashOverlay);

            // frame overlay (designer puts a frame background-image on .vex-bar__frame; empty otherwise).
            this.track.Add(Fill("vex-bar__frame"));

            this.RegisterCallback<GeometryChangedEvent>(_ => this.SyncInnerWidths());
            this.ApplyAnchors();
            this.Apply();
        }

        [UxmlAttribute]
        [CreateProperty]
        public float value
        {
            get => this.m_Value;
            set { this.m_Value = value; this.Apply(); }
        }

        [UxmlAttribute]
        [CreateProperty]
        public float ghost
        {
            get => this.m_Ghost;
            set { this.m_Ghost = value; this.Apply(); }
        }

        [UxmlAttribute]
        [CreateProperty]
        public float flash
        {
            get => this.m_Flash;
            set { this.m_Flash = value; this.Apply(); }
        }

        [UxmlAttribute]
        public float lowThreshold
        {
            get => this.m_LowThreshold;
            set { this.m_LowThreshold = value; this.Apply(); }
        }

        [UxmlAttribute]
        public bool rightToLeft
        {
            get => this.m_RightToLeft;
            set { this.m_RightToLeft = value; this.ApplyAnchors(); }
        }

        private static VisualElement Part(string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            return e;
        }

        // A clip pinned to the fill-origin side; its width (%) reveals the fixed-width inner.
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

        private void SyncInnerWidths()
        {
            var w = this.track.resolvedStyle.width;
            if (w <= 0f)
            {
                return;
            }

            this.ghostInner.style.width = w;
            this.fillInner.style.width = w;
        }

        private void ApplyAnchors()
        {
            foreach (var clip in new[] { this.ghostClip, this.fillClip })
            {
                if (this.m_RightToLeft) { clip.style.left = StyleKeyword.Auto; clip.style.right = 0; }
                else { clip.style.right = StyleKeyword.Auto; clip.style.left = 0; }
            }

            foreach (var inner in new[] { this.ghostInner, this.fillInner })
            {
                inner.style.position = Position.Absolute;
                inner.style.top = 0;
                inner.style.bottom = 0;
                if (this.m_RightToLeft) { inner.style.left = StyleKeyword.Auto; inner.style.right = 0; }
                else { inner.style.right = StyleKeyword.Auto; inner.style.left = 0; }
            }

            this.EnableInClassList("vex-bar--rtl", this.m_RightToLeft);
        }

        private void Apply()
        {
            this.ghostClip.style.width = Length.Percent(Saturate(this.m_Ghost) * 100f);
            this.fillClip.style.width = Length.Percent(Saturate(this.m_Value) * 100f);
            this.flashOverlay.style.opacity = Saturate(this.m_Flash);
            this.EnableInClassList("vex-bar--low", this.m_LowThreshold > 0f && this.m_Value <= this.m_LowThreshold);
        }

        private static float Saturate(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
