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

                // DependsOn EVERY managed ref so editing a schema/source/link re-bakes the SubScene.
                if (r.Source.link != null) baker.DependsOn(r.Source.link);

                // Bar rows take value/max/ghost + ghost/flash timing from the shared EssenceBarSource (the SAME asset
                // the world bar uses — single source of truth, no duplication). Number/Text rows with no source use the
                // generic single Value below.
                UIValueKind vKind, mKind;
                ushort vKey, mKey, gKey, lockedKey;
                HudGhostMode gMode;
                float gDelay, gSpeed, fDecay;

                if (r.Bar != null)
                {
                    baker.DependsOn(r.Bar);
                    if (r.Bar.current != null) baker.DependsOn(r.Bar.current);
                    if (r.Bar.max != null) baker.DependsOn(r.Bar.max);
                    if (r.Bar.ghostIntrinsic != null) baker.DependsOn(r.Bar.ghostIntrinsic);
                    if (r.Bar.ghostStat != null) baker.DependsOn(r.Bar.ghostStat);

                    vKind = UIValueKind.Intrinsic;
                    vKey = (ushort)(r.Bar.current != null ? r.Bar.current.Key.ID : 0);
                    mKind = UIValueKind.Stat;
                    mKey = (ushort)(r.Bar.max != null ? r.Bar.max.Key.ID : 0);
                    gMode = r.Bar.ghostMode;
                    gKey = gMode == HudGhostMode.FromStat
                        ? (ushort)(r.Bar.ghostStat != null ? r.Bar.ghostStat.Key.ID : 0)
                        : (ushort)(r.Bar.ghostIntrinsic != null ? r.Bar.ghostIntrinsic.Key.ID : 0);
                    gDelay = r.Bar.ghostDelay;
                    gSpeed = r.Bar.ghostSpeed;
                    fDecay = r.Bar.flashDecay;
                    if (r.Bar.locked != null) baker.DependsOn(r.Bar.locked);
                    lockedKey = (ushort)(r.Bar.locked != null ? r.Bar.locked.Key.ID : 0);
                }
                else
                {
                    if (r.Value != null) baker.DependsOn(r.Value);

                    vKind = KindOf(r.Value);
                    vKey = KeyOf(r.Value);
                    mKind = UIValueKind.Intrinsic;
                    mKey = 0; // generic single readout — no max fraction
                    gMode = HudGhostMode.Off;
                    gKey = 0;
                    gDelay = 0.4f;
                    gSpeed = 6f;
                    fDecay = 0.25f;
                    lockedKey = 0;
                }

                // Bake-time diagnostics — surface silent misconfigurations ONCE instead of a blank bar at runtime.
                if (r.Source.Mode == UISourceMode.Binding)
                {
                    UnityEngine.Debug.LogError($"[DataUI] Row {i} ('{r.Label}'): Source Mode is Binding — a HUD row has no bound self, so it never resolves and stays hidden. Set Mode = Player.");
                }

                if (r.Bar != null && r.Bar.max == null)
                {
                    UnityEngine.Debug.LogWarning($"[DataUI] Row {i} ('{r.Label}'): the shared source has no Max stat — the bar renders empty until one is assigned.");
                }

                if (r.Bar == null && r.Value is ConditionEventObject)
                {
                    UnityEngine.Debug.LogWarning($"[DataUI] Row {i} ('{r.Label}'): Event-kind readouts read a per-frame-cleared buffer from presentation and show 0 — use an Intrinsic/Stat value instead.");
                }

                if (!string.IsNullOrEmpty(r.Format))
                {
                    try
                    {
                        _ = string.Format(r.Format, 0, 0);
                    }
                    catch (System.FormatException)
                    {
                        UnityEngine.Debug.LogError($"[DataUI] Row {i} ('{r.Label}'): Format '{r.Format}' is invalid — use '{{0}}' (current) / '{{1}}' (max).");
                    }
                }

                var prof = r.Bar != null ? r.Bar.feedback : null;
                if (prof != null) baker.DependsOn(prof);

                rows.Add(new UIBindingEntry
                {
                    Slot = (byte)i,
                    Source = r.Source.ToComponent(baker),
                    ValueKind = vKind,
                    ValueKey = vKey,
                    MaxKind = mKind,
                    MaxKey = mKey,
                    GhostKey = gKey,
                    LockedKey = lockedKey,
                    Kind = r.Kind,
                    Label = Clip(r.Label),
                    Format = Clip(r.Format),
                    Class = Clip(r.Class),
                    GhostMode = gMode,
                    GhostDelay = gDelay,
                    GhostSpeed = gSpeed,
                    FlashDecay = fDecay,
                    AutoHideDelay = r.AutoHideDelay,
                    AlwaysVisible = B(r.AlwaysVisible),
                    KeepVisibleWhileNotFull = B(r.KeepVisibleWhileNotFull),
                    ShowOnHealthChange = B(r.ShowOnHealthChange),
                    FlashOnDamage = B(r.FlashOnDamage),
                    TrailMode = (byte)(prof != null ? (byte)prof.trailMode : (byte)TrailMode.Both),
                    Accumulate = B(prof == null || prof.accumulate),
                    Fade = B(prof == null || prof.fade),
                    DrainEase = (byte)(prof != null ? (byte)prof.drainEase : (byte)EaseId.OutCubic),
                    HoldMs = prof != null ? prof.holdMs : 400f,
                    DrainMs = prof != null ? prof.drainMs : 500f,
                    MinDrainMs = prof != null ? prof.minDrainMs : 120f,
                    MinChipFrac = prof != null ? prof.minChipFrac : 0.005f,
                });
            }
        }

        private static UIValueKind KindOf(ConditionSchemaObject o) => o switch
        {
            StatSchemaObject => UIValueKind.Stat,
            ConditionEventObject => UIValueKind.Event,
            _ => UIValueKind.Intrinsic, // IntrinsicSchemaObject or null
        };

        private static ushort KeyOf(ConditionSchemaObject o) => o != null ? (ushort)o.Key.ID : (ushort)0;

        private static FixedString64Bytes Clip(string s) => string.IsNullOrEmpty(s) ? default : (FixedString64Bytes)s;

        private static byte B(bool v) => (byte)(v ? 1 : 0);

        /// <summary>One generic readout. SOURCE = who; Value/Max/Ghost = which Essence value(s); the rest = presentation.</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Who to read from (player index / route / link). Reuses the timeline UISource resolver.")]
            public UISourceAuthoring Source;

            [Header("What to show")]
            [Tooltip("Bar rows: a shared EssenceBarSource — the SAME asset the world-space bar uses — supplies the " +
                     "value/max/ghost + ghost/flash timing (single source of truth). Configure the data once, share it " +
                     "world + screen.")]
            public EssenceBarSource Bar;

            [Tooltip("Number/Text rows only: the single value to show (Intrinsic / Stat / Event). Ignored when a shared " +
                     "Bar source is set above. Use for one-off readouts (ammo, score, combo).")]
            public ConditionSchemaObject Value;

            [Header("Presentation")]
            public UIRowKind Kind = UIRowKind.Bar;
            public string Label;
            [Tooltip("Composite format, e.g. '{0:0} / {1:0}' (arg0=current, arg1=max). Empty = auto.")]
            public string Format;
            [Tooltip("Optional USS class hook the designer can target.")]
            public string Class;

            // Visibility is the per-medium concern (USS owns the fade/colors); these only decide WHEN to add .is-hidden.
            [Header("Visibility")]
            [Tooltip("Always shown (no auto-hide). The common HUD case.")]
            public bool AlwaysVisible = true;
            [Tooltip("Stay visible while the bar is not full (e.g. show damaged bars).")]
            public bool KeepVisibleWhileNotFull = true;
            [Tooltip("Pop in when the value changes, then auto-hide after the delay.")]
            public bool ShowOnHealthChange = true;
            [Tooltip("Seconds of no change before auto-hiding (when not Always Visible). 0 = never.")]
            public float AutoHideDelay = 3f;
            [Tooltip("Flash the bar on damage (flash timing comes from the shared source).")]
            public bool FlashOnDamage = true;
        }
    }
}
