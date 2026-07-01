// <copyright file="NodeChip.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.UI
{
    using System;
    using UnityEngine.UIElements;

    /// <summary>
    /// A reusable circular icon-chip primitive for node graphs / choice screens (map rooms, skill trees, dialogue
    /// options). Pure structure + a click event; ALL look-and-feel is USS via the classes <c>vex-chip</c>,
    /// <c>vex-chip--{kind}</c> and the states <c>is-legal</c> / <c>is-current</c> / <c>:disabled</c>. Set
    /// <see cref="glyph"/> for the label and <see cref="kind"/> to pick the palette; subscribe to <see cref="clicked"/>.
    /// </summary>
    [UxmlElement]
    public partial class NodeChip : VisualElement
    {
        private readonly Label label;
        private readonly Clickable clickable;
        private string kindClass;

        public NodeChip()
        {
            this.AddToClassList("vex-chip");
            this.focusable = true;

            this.label = new Label { pickingMode = PickingMode.Ignore };
            this.label.AddToClassList("vex-chip__glyph");
            this.Add(this.label);

            this.clickable = new Clickable(() => this.clicked?.Invoke());
            this.AddManipulator(this.clickable);
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
    }
}
