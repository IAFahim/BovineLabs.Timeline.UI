// <copyright file="NodeChip.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.UI
{
    using System;
    using BovineLabs.Anchor.Audio;
    using UnityEngine.UIElements;

    /// <summary>
    /// A reusable circular icon-chip primitive for node graphs / choice screens (map rooms, skill trees, dialogue
    /// options). Pure structure + a click event; ALL look-and-feel is USS via the classes <c>vex-chip</c>,
    /// <c>vex-chip--{kind}</c> and the states <c>is-legal</c> / <c>is-current</c> / <c>:disabled</c>. Set
    /// <see cref="glyph"/> for the label and <see cref="kind"/> to pick the palette; subscribe to <see cref="clicked"/>.
    /// Hover and activation play Anchor UI audio cues (<see cref="AnchorAudioCue.Hover"/> / <see cref="AnchorAudioCue.Activate"/>)
    /// from the <see cref="audioProfile"/> profile — the same convention App UI controls use — so map/menu navigation is
    /// audible without per-screen wiring.
    /// </summary>
    [UxmlElement]
    public partial class NodeChip : VisualElement
    {
        private readonly Label label;
        private readonly Clickable clickable;
        private string kindClass;
        private string audioProfileKey = UiAudioCues.Click;

        public NodeChip()
        {
            this.AddToClassList("vex-chip");
            this.focusable = true;

            this.label = new Label { pickingMode = PickingMode.Ignore };
            this.label.AddToClassList("vex-chip__glyph");
            this.Add(this.label);

            this.clickable = new Clickable(this.OnActivated);
            this.AddManipulator(this.clickable);

            // Hover feedback — only while enabled (the click gate below matches).
            this.RegisterCallback<PointerEnterEvent>(this.OnPointerEnter);
        }

        /// <summary>Raised when the chip is clicked (only fires while enabled).</summary>
        public event Action clicked;

        /// <summary>The short text shown in the chip.</summary>
        [UxmlAttribute]
        public string glyph
        {
            get => this.label.text;
            set => this.label.text = value;
        }

        /// <summary>Kind key; toggles the USS class <c>vex-chip--{kind}</c> for per-kind styling.</summary>
        [UxmlAttribute]
        public string kind
        {
            get => this.kindClass;
            set
            {
                if (!string.IsNullOrEmpty(this.kindClass))
                {
                    this.RemoveFromClassList("vex-chip--" + this.kindClass);
                }

                this.kindClass = value;

                if (!string.IsNullOrEmpty(this.kindClass))
                {
                    this.AddToClassList("vex-chip--" + this.kindClass);
                }
            }
        }

        /// <summary>The <see cref="AnchorAudioProfile"/> key used for hover/click cues; defaults to <see cref="UiAudioCues.Click"/>.</summary>
        [UxmlAttribute]
        public string audioProfile
        {
            get => this.audioProfileKey;
            set => this.audioProfileKey = string.IsNullOrEmpty(value) ? UiAudioCues.Click : value;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (this.enabledInHierarchy)
            {
                AnchorAudio.Play(this.audioProfileKey, AnchorAudioCue.Hover, AnchorAudioCueOverride.Inherit);
            }
        }

        private void OnActivated()
        {
            // Clickable only invokes while the element is enabled, so no extra gate is needed here.
            AnchorAudio.Play(this.audioProfileKey, AnchorAudioCue.Activate, AnchorAudioCueOverride.Inherit);
            this.clicked?.Invoke();
        }
    }
}
