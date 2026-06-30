using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.UI.Data
{
    /// <summary>What a row reads + how it presents. The "any data, not just health" lives in <see cref="ValueKind"/>.</summary>
    public enum UIRowKind : byte
    {
        Number = 0, // plain readout (current, or current/max)
        Bar = 1,    // animated fill bar (uses ghost/flash/visibility behaviour)
        Text = 2,   // label/text only
    }

    /// <summary>Which Essence buffer a key reads from (Intrinsic = live counter, Stat = scaling value, Event = condition).</summary>
    public enum UIValueKind : byte
    {
        Intrinsic = 0,
        Stat = 1,
        Event = 2,
    }

    /// <summary>Marks the singleton entity that carries the baked <see cref="UIBindingEntry"/> / <see cref="UIPanelEntry"/> buffers.</summary>
    public struct DataUITag : IComponentData
    {
    }

    /// <summary>A UXML key (in AnchorSettings.Views) the data-UI loader should mount. Designer-listed in the settings asset.</summary>
    public struct UIPanelEntry : IBufferElementData
    {
        public FixedString64Bytes Key;
    }

    /// <summary>
    /// ONE generic data-to-UI row, baked from a designer entry in the DataUISettings asset. Drives a single readout
    /// (a bar, a number, or text) for ANY Essence value — there is nothing health-specific here. The generic driver
    /// resolves <see cref="Source"/> to an entity and reads <see cref="ValueKey"/> (+ optional Max/Ghost) by kind; the
    /// bar behaviour knobs are per-row so every widget animates independently. Colors/layout/textures are USS — not here.
    /// </summary>
    public struct UIBindingEntry : IBufferElementData
    {
        public byte Slot; // == settings list index → the Rows[Slot] the UXML binds

        public UISource Source;

        public UIValueKind ValueKind;
        public ushort ValueKey;   // numerator (current)
        public UIValueKind MaxKind;
        public ushort MaxKey;     // optional denominator; 0 = none → plain readout
        public ushort GhostKey;   // optional explicit ghost source (FromStat/FromIntrinsic); kind follows GhostMode

        public UIRowKind Kind;
        public FixedString64Bytes Label;
        public FixedString64Bytes Format; // e.g. "{0:0} / {1:0}"; empty = auto
        public FixedString64Bytes Class;  // optional USS class hook

        // Bar dynamics — resolved from the shared EssenceBarSource (Kind == Bar only). USS owns fade + low-pulse.
        public HudGhostMode GhostMode;
        public float GhostDelay;
        public float GhostSpeed;
        public float FlashDecay;

        // Visibility logic (per-row / per-medium) → drives the .is-hidden USS class.
        public float AutoHideDelay;
        public byte AlwaysVisible;
        public byte KeepVisibleWhileNotFull;
        public byte ShowOnHealthChange;
        public byte FlashOnDamage;
    }
}
