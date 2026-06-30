namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Core.Authoring.Settings;
    using BovineLabs.Core.Settings;
    using BovineLabs.Essence.Authoring;
    using BovineLabs.Reaction.Authoring.Conditions;
    using BovineLabs.Timeline.UI.Data;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The single project-wide DATA-UI settings asset (BovineLabs ▸ Settings ▸ UI). A designer lists generic UI
    /// <see cref="Entry"/> rows — each maps ANY Essence value (intrinsic/stat/event, via the shared
    /// <see cref="ConditionSchemaObject"/> base, so it is NOT health-specific) for a source/player to a presentation
    /// kind (bar/number/text) — plus the UXML panel keys to mount. <see cref="Bake"/> writes them as buffers onto the
    /// shared settings entity; the generic <c>DataUIDriverSystem</c> + <c>DataUIPanelLoader</c> read them. Adding a new
    /// readout (ammo, score, stamina) is editing THIS asset + the UXML + USS — zero new code. Colors/layout/textures
    /// live in USS, never here.
    /// </summary>
    [SettingsGroup("UI")]
    [SettingSubDirectory("UI")]
    public sealed class DataUISettings : SettingsBase
    {
        [Header("Panels")]
        [Tooltip("UXML keys (registered in AnchorSettings ▸ Views) to mount on the app root, e.g. 'hud', 'ammo'.")]
        public List<string> Panels = new();

        [Header("Rows")]
        [Tooltip("Ordered readouts. The list index is the Rows[i] index your UXML binds to. Each row reads any Essence value for a source and presents it as a bar/number/text.")]
        public List<Entry> Rows = new();

        public override void Bake(Baker<SettingsAuthoring> baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.None);
            baker.AddComponent<DataUITag>(entity);

            var panels = baker.AddBuffer<UIPanelEntry>(entity);
            foreach (var key in this.Panels)
            {
                panels.Add(new UIPanelEntry { Key = Clip(key) });
            }

            var rows = baker.AddBuffer<UIBindingEntry>(entity);
            for (var i = 0; i < this.Rows.Count; i++)
            {
                var r = this.Rows[i];

                // DependsOn EVERY managed ref so editing a schema/link re-bakes the SubScene.
                if (r.Value != null) baker.DependsOn(r.Value);
                if (r.Max != null) baker.DependsOn(r.Max);
                if (r.Ghost != null) baker.DependsOn(r.Ghost);
                if (r.Source.Link != null) baker.DependsOn(r.Source.Link);

                rows.Add(new UIBindingEntry
                {
                    Slot = (byte)i,
                    Source = r.Source.ToComponent(),
                    ValueKind = KindOf(r.Value),
                    ValueKey = KeyOf(r.Value),
                    MaxKind = KindOf(r.Max),
                    MaxKey = KeyOf(r.Max),
                    GhostKind = KindOf(r.Ghost),
                    GhostKey = KeyOf(r.Ghost),
                    Kind = r.Kind,
                    Label = Clip(r.Label),
                    Format = Clip(r.Format),
                    Class = Clip(r.Class),
                    GhostMode = r.GhostMode,
                    GhostDelay = r.GhostDelay,
                    GhostSpeed = r.GhostSpeed,
                    FadeInDuration = r.FadeInDuration,
                    FadeOutDuration = r.FadeOutDuration,
                    AutoHideDelay = r.AutoHideDelay,
                    FlashDecay = r.FlashDecay,
                    PulseAmp = r.PulseAmp,
                    PulseSpeed = r.PulseSpeed,
                    PulseThreshold = r.PulseThreshold,
                    AlwaysVisible = B(r.AlwaysVisible),
                    StartVisible = B(r.StartVisible),
                    KeepVisibleWhileNotFull = B(r.KeepVisibleWhileNotFull),
                    ShowOnHealthChange = B(r.ShowOnHealthChange),
                    FlashOnDamage = B(r.FlashOnDamage),
                });
            }
        }

        private static UIValueKind KindOf(ConditionSchemaObject o) => o switch
        {
            StatSchemaObject => UIValueKind.Stat,
            ConditionEventObject => UIValueKind.Event,
            _ => UIValueKind.Intrinsic, // IntrinsicSchemaObject or null
        };

        private static ushort KeyOf(ConditionSchemaObject o) => o != null ? (ushort)o.Key : (ushort)0;

        private static FixedString64Bytes Clip(string s) => string.IsNullOrEmpty(s) ? default : (FixedString64Bytes)s;

        private static byte B(bool v) => (byte)(v ? 1 : 0);

        /// <summary>One generic readout. SOURCE = who; Value/Max/Ghost = which Essence value(s); the rest = presentation.</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Who to read from (player index / route / link). Reuses the timeline UISource resolver.")]
            public UISourceAuthoring Source;

            [Header("Data (any intrinsic / stat / event)")]
            [Tooltip("The value to show (current). Intrinsic, Stat, or Event schema — anything, not just health.")]
            public ConditionSchemaObject Value;

            [Tooltip("Optional denominator (usually a Max stat). None = plain readout, no bar fraction.")]
            public ConditionSchemaObject Max;

            [Tooltip("Optional explicit ghost/chip source (for GhostMode FromStat/FromIntrinsic).")]
            public ConditionSchemaObject Ghost;

            [Header("Presentation")]
            public UIRowKind Kind = UIRowKind.Bar;
            public string Label;
            [Tooltip("Composite format, e.g. '{0:0} / {1:0}' (arg0=current, arg1=max). Empty = auto.")]
            public string Format;
            [Tooltip("Optional USS class hook the designer can target.")]
            public string Class;

            [Header("Bar behaviour (Kind = Bar only)")]
            public HudGhostMode GhostMode = HudGhostMode.ComputedLerp;
            public float GhostDelay = 0.4f;
            public float GhostSpeed = 6f;
            public float FadeInDuration = 0.15f;
            public float FadeOutDuration = 0.4f;
            public float AutoHideDelay = 3f;
            public float FlashDecay = 0.25f;
            public float PulseAmp = 0.5f;
            public float PulseSpeed = 5f;
            public float PulseThreshold = 0.3f;
            public bool AlwaysVisible = true;
            public bool StartVisible = true;
            public bool KeepVisibleWhileNotFull = true;
            public bool ShowOnHealthChange = true;
            public bool FlashOnDamage = true;
        }
    }
}
