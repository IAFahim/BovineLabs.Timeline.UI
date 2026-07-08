using BovineLabs.Nerve.Pause;
using System.Collections.Generic;
using BovineLabs.Anchor;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Nerve.ObjectManagement;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;
#if !BL_DISABLE_PAUSE
using BovineLabs.Core.Pause;
#endif

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The ONE generic data-UI driver. It resolves each baked <see cref="UIBindingEntry"/> row to an entity, reads its
    /// Essence value(s), advances a per-slot presentation state through the single <see cref="HudBarMath"/> kernel
    /// (ghost / flash / visibility from the baked per-row config), and pushes the result onto the mounted UXML elements
    /// (bar-/name-/value-/card-{slot}). A <see cref="BarGhost"/> component, when present, is an OPTIONAL sim-authoritative
    /// ghost override. Managed <see cref="SystemBase"/> (touches <see cref="AnchorApp.Current"/> + holds a managed
    /// per-panel element cache); never Burst.
    ///
    /// FEEDBACK CONSUMPTION (non-destructive, multi-reader): this driver NEVER clears the
    /// <see cref="BarFeedbackEvent"/> buffer. Lifetime is owned by <see cref="BarFeedbackDrainSystem"/> (LateSimulation),
    /// which stamps unstamped events with its current frame and removes them one frame later. The driver consumes only
    /// events whose <see cref="BarFeedbackEvent.Frame"/> == the drain's current frame (published as the
    /// <see cref="BarFeedbackFrame"/> singleton), so every reader sees each event exactly once and none destroys it. If
    /// no drain runs in this world the singleton is absent (frame 0) and nothing is consumed — safe no-op, never a
    /// double-consume.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial class DataUIDriverSystem : SystemBase
    {
        public const string PanelClass = "vex-hud";

        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> sourcesLookup;
        private UnsafeBufferLookup<EntityLinkEntry> linksLookup;
        private ComponentLookup<BarGhost> ghostLookup;

        private NativeList<float> chipScratch;
        private NativeArray<HudSlotState> slots; // per-row presentation state; sized to the baked entry count
        private bool[] warnedMissing;            // per-slot 0..255 (Slot is a byte) — no 64-entry cap
        private bool[] warnedBadFormat;
        private int warnedKindMask;              // per-FeedbackKind warn-once for unhandled kinds

        // Managed per-panel element cache. Rebuilt ONLY when the mounted panel set changes — this kills the per-frame
        // per-slot Q()/string-interpolation storm that was the driver's GC hot path.
        private readonly List<VisualElement> currentPanels = new();
        private readonly List<PanelCache> panelCaches = new();

        // Clock policy (TODO.md item 13): the driver forwards the built-in bl-core pause state to every HudBar so the
        // scheduler-driven chip hold/collapse freezes while paused. PauseGame lives on a system entity → IncludeSystems.
#if !BL_DISABLE_PAUSE
        private EntityQuery pauseQuery;
#endif
        private bool paused;

        protected override void OnCreate()
        {
            ref var state = ref this.CheckedStateRef;
            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this.sourcesLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            this.linksLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            this.ghostLookup = this.GetComponentLookup<BarGhost>(true);
            this.chipScratch = new NativeList<float>(16, Allocator.Persistent);
            this.warnedMissing = new bool[256];
            this.warnedBadFormat = new bool[256];

#if !BL_DISABLE_PAUSE
            using var pauseBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PauseGame>()
                .WithOptions(EntityQueryOptions.IncludeSystems);
            this.pauseQuery = pauseBuilder.Build(ref state);
#endif

            this.RequireForUpdate<DataUITag>();
            this.RequireForUpdate<ControllableRegistry>();
        }

        protected override void OnDestroy()
        {
            this.chipScratch.Dispose();
            if (this.slots.IsCreated)
            {
                this.slots.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            var app = AnchorApp.Current;
            if (app == null)
            {
                return;
            }

            // One query to find the mounted HUD panels, into a reused list (no per-frame ToList alloc). If the panel set
            // changed, rebuild the per-slot element cache; otherwise the cache is reused as-is.
            this.currentPanels.Clear();
            foreach (var pnl in app.RootVisualElement.Query(className: PanelClass).Build())
            {
                this.currentPanels.Add(pnl);
            }

            if (this.currentPanels.Count == 0)
            {
                return;
            }

            var entries = SystemAPI.GetSingletonBuffer<UIBindingEntry>(true);

            if (this.PanelsChanged())
            {
                this.RebuildCache(entries);
            }

            var players = SystemAPI.GetSingleton<ControllableRegistry>();
            var intrinsics = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var stats = SystemAPI.GetBufferLookup<Stat>(true);
            var events = SystemAPI.GetBufferLookup<ConditionEvent>(true);
            var feedback = SystemAPI.GetBufferLookup<BarFeedbackEvent>(true); // READ-ONLY: the drain system owns removal

            // Narrow sync: main-thread UI reads need these types settled, but a full-graph Dependency.Complete() is not
            // required. One completion per lookup/consumer below — ADD to this list if you add a lookup.
            var em = this.EntityManager;
            em.CompleteDependencyBeforeRO<Intrinsic>();        // ReadValue: numerator / max / locked
            em.CompleteDependencyBeforeRO<Stat>();             // ReadValue: max / stat
            em.CompleteDependencyBeforeRO<ConditionEvent>();   // ReadValue: event
            em.CompleteDependencyBeforeRO<Targets>();          // UISourceResolver route
            em.CompleteDependencyBeforeRO<EntityLinkSource>(); // UISourceResolver link source
            em.CompleteDependencyBeforeRO<EntityLinkEntry>();  // UISourceResolver link map
            em.CompleteDependencyBeforeRO<BarGhost>();         // optional sim-authoritative ghost override
            em.CompleteDependencyBeforeRO<BarFeedbackEvent>(); // non-destructive feedback read (drain is the sole writer)

            this.targetsLookup.Update(this);
            this.sourcesLookup.Update(this);
            this.linksLookup.Update(this);
            this.ghostLookup.Update(this);

            this.EnsureSlots(entries.Length);

            var currentFrame = SystemAPI.TryGetSingleton<BarFeedbackFrame>(out var bff) ? bff.Frame : 0u;

            // Clock policy (TODO.md item 13 — RESOLVED): kernel dt (ghost catch-up, flash decay, idle/auto-hide) is the
            // UNSCALED presentation clock, so bullet-time no longer slows HUD feedback and pause freezes it (0 while
            // paused). Fallback: the old clamped scaled step for worlds without the clock system (tests).
            var dt = SystemAPI.TryGetSingleton<UIUnscaledTime>(out var uiTime)
                ? uiTime.DeltaTime
                : math.min((float)SystemAPI.Time.DeltaTime, UIClock.MaxStep);

#if !BL_DISABLE_PAUSE
            this.paused = !this.pauseQuery.IsEmptyIgnoreFilter;
#endif

            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];

                var alive = UISourceResolver.TryResolve(
                    e.Source, Entity.Null, players, this.targetsLookup, this.sourcesLookup, this.linksLookup, out var entity)
                    && entity != Entity.Null;

                float fill = 0f, ghostFrac = 0f, lockedFrac = 0f, current = 0f, max = 0f, flash = 0f, alpha = 0f;
                var ready = false;
                this.chipScratch.Clear();

                var s = this.slots[i];

                if (alive)
                {
                    var hasIntr = intrinsics.TryGetBuffer(entity, out var intrBuf);
                    var hasStat = stats.TryGetBuffer(entity, out var statBuf);
                    var hasEvent = events.TryGetBuffer(entity, out var evBuf);

                    current = ReadValue(e.ValueKind, e.ValueKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent);
                    max = e.MaxKey != 0 ? ReadValue(e.MaxKind, e.MaxKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent) : 0f;
                    current = math.select(0f, current, math.isfinite(current)); // sanitize once; the renderer trusts clean structure
                    max = math.select(0f, max, math.isfinite(max));
                    ready = e.MaxKey == 0 || max > 0f;
                    fill = HudBarMath.Fill(current, max);

                    // FEEDBACK inbox → non-destructive read of THIS frame's stamped events (see class remarks).
                    var healFrac = 0f;
                    var flashEvent = false;
                    if (currentFrame != 0u && feedback.TryGetBuffer(entity, out var fb))
                    {
                        for (var k = 0; k < fb.Length; k++)
                        {
                            var evt = fb[k];
                            if (evt.Frame != currentFrame)
                            {
                                continue; // not this frame's event (drain removes it next frame)
                            }

                            switch (evt.Kind)
                            {
                                case FeedbackKind.DamageChip:
                                    if (max > 0f)
                                    {
                                        this.chipScratch.Add((float)System.Math.Abs((long)evt.Amount) / max);
                                    }

                                    break;
                                case FeedbackKind.HealSurge:
                                    if (max > 0f)
                                    {
                                        healFrac += (float)System.Math.Abs((long)evt.Amount) / max;
                                    }

                                    break;
                                case FeedbackKind.Flash:
                                    flashEvent = true;
                                    break;
                                default:
                                    this.WarnUnhandledKind(evt.Kind);
                                    break;
                            }
                        }
                    }

                    // External ghost source (FromStat/FromIntrinsic) as a fraction; the kernel consumes it.
                    var extGhost = 0f;
                    if (max > 0f && (e.GhostMode == HudGhostMode.FromStat || e.GhostMode == HudGhostMode.FromIntrinsic))
                    {
                        var gKind = e.GhostMode == HudGhostMode.FromStat ? UIValueKind.Stat : UIValueKind.Intrinsic;
                        extGhost = ReadValue(gKind, e.GhostKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent) / max;
                    }

                    // ONE behaviour kernel: ghost / flash / visibility from the baked per-row config + slot state.
                    HudBarMath.AdvanceSlot(ref s, e.GhostMode, fill, extGhost, dt,
                        e.GhostDelay, e.GhostSpeed, e.FlashOnDamage != 0, e.FlashDecay, flashEvent, healFrac,
                        e.AlwaysVisible != 0, e.KeepVisibleWhileNotFull != 0, e.ShowOnHealthChange != 0, e.AutoHideDelay,
                        out ghostFrac, out flash, out alpha, out _);

                    // BarGhost is an OPTIONAL sim-authoritative override (e.g. the sample conductor); it wins when present.
                    if (this.ghostLookup.TryGetComponent(entity, out var bg) && max > 0f)
                    {
                        ghostFrac = math.saturate(bg.Value / max);
                    }

                    if (e.LockedKey != 0 && hasIntr)
                    {
                        var locked = intrBuf.GetValue((IntrinsicKey)e.LockedKey, 0);
                        lockedFrac = max > 0f ? math.saturate(locked / max) : 0f;
                    }
                }
                else
                {
                    s = default; // reset so a re-resolve snaps clean (no phantom ghost/flash/idle carryover)
                }

                this.slots[i] = s;

                var show = alive && ready && alpha > 0.5f;
                this.Push(i, show, math.saturate(fill), math.saturate(ghostFrac), math.saturate(lockedFrac), math.saturate(flash), current, max, in e);
            }
        }

        private void Push(int i, bool show, float fill, float ghost, float locked, float flash, float current, float max, in UIBindingEntry e)
        {
            var curInt = (int)math.round(current);
            var maxInt = (int)math.round(max);

            for (var p = 0; p < this.panelCaches.Count; p++)
            {
                var se = this.panelCaches[p].Slots[i];
                if (se == null)
                {
                    continue;
                }

                if (!se.Primed || se.LastShow != show)
                {
                    se.Card?.EnableInClassList("is-hidden", !show);
                    se.LastShow = show;
                }

                if (se.Bar != null)
                {
                    // Pause freeze is change-driven per slot (SetPaused early-outs, but skip the call storm anyway).
                    if (!se.PauseApplied || se.LastPaused != this.paused)
                    {
                        se.Bar.SetPaused(this.paused);
                        se.LastPaused = this.paused;
                        se.PauseApplied = true;
                    }

                    if (!se.ConfigApplied)
                    {
                        // Config is bake-static → SetTrailConfig ONCE per cache build, not every frame.
                        se.Bar.SetTrailConfig((TrailMode)e.TrailMode, e.Accumulate != 0, e.HoldMs, e.DrainMs, e.MinDrainMs, (EaseId)e.DrainEase, e.Fade != 0, e.MinChipFrac, e.DrainRate);
                        se.ConfigApplied = true;
                    }

                    if (!se.Primed || Changed(se.LastFill, fill) || Changed(se.LastGhost, ghost) || Changed(se.LastLocked, locked) || Changed(se.LastFlash, flash))
                    {
                        se.Bar.SetState(fill, ghost, locked, flash); // ONE ApplyStructure vs three property setters
                        se.LastFill = fill;
                        se.LastGhost = ghost;
                        se.LastLocked = locked;
                        se.LastFlash = flash;
                    }

                    // Chips are TOLD per explicit event; SetState (above) set the fill first so the chip top = fill + amount.
                    for (var k = 0; k < this.chipScratch.Length; k++)
                    {
                        se.Bar.AddChip(this.chipScratch[k]);
                    }
                }

                if (se.Value != null)
                {
                    if (show)
                    {
                        // Recompute the value string only when the ROUNDED ints change — string.Format is the alloc we kill.
                        if (!se.Primed || curInt != se.LastCur || maxInt != se.LastMax)
                        {
                            se.LastCur = curInt;
                            se.LastMax = maxInt;
                            var valueText = this.FormatValue(e, curInt, maxInt);
                            if (se.LastValueText != valueText)
                            {
                                se.Value.text = valueText;
                                se.LastValueText = valueText;
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(se.LastValueText))
                    {
                        se.Value.text = string.Empty;
                        se.LastValueText = string.Empty;
                        se.LastCur = int.MinValue;
                        se.LastMax = int.MinValue;
                    }
                }

                se.Primed = true;
            }
        }

        private string FormatValue(in UIBindingEntry e, int curInt, int maxInt)
        {
            if (!e.Format.IsEmpty)
            {
                try
                {
                    return string.Format(e.Format.ToString(), curInt, maxInt);
                }
                catch (System.FormatException)
                {
                    var slot = e.Slot;
                    if (!this.warnedBadFormat[slot])
                    {
                        this.warnedBadFormat[slot] = true;
                        UnityEngine.Debug.LogWarning($"[DataUI] Row {slot} ('{e.Label}') Format '{e.Format}' is invalid — using default. Use '{{0}}' (current) / '{{1}}' (max).");
                    }

                    return AutoText(curInt, maxInt);
                }
            }

            return AutoText(curInt, maxInt);
        }

        private bool PanelsChanged()
        {
            if (this.currentPanels.Count != this.panelCaches.Count)
            {
                return true;
            }

            for (var p = 0; p < this.currentPanels.Count; p++)
            {
                if (this.panelCaches[p].Panel != this.currentPanels[p])
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildCache(DynamicBuffer<UIBindingEntry> entries)
        {
            this.panelCaches.Clear();

            // Reset the missing-element warn-once so a fixed UXML re-warns if it is still broken after a remount.
            System.Array.Clear(this.warnedMissing, 0, this.warnedMissing.Length);

            for (var p = 0; p < this.currentPanels.Count; p++)
            {
                var panel = this.currentPanels[p];
                var pc = new PanelCache { Panel = panel, Slots = new SlotElements[entries.Length] };

                for (var i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    var key = e.SlotName.IsEmpty ? e.Slot.ToString() : e.SlotName.ToString(); // computed ONCE per cache build
                    var se = new SlotElements
                    {
                        Card = panel.Q("card-" + key),
                        Bar = panel.Q<HudBar>("bar-" + key),
                        Name = panel.Q<Label>("name-" + key),
                        Value = panel.Q<Label>("value-" + key),
                    };

                    if (se.Name != null)
                    {
                        se.Name.text = e.Label.ToString(); // static → set once, never per frame
                    }

                    pc.Slots[i] = se;
                }

                this.panelCaches.Add(pc);
            }

            // Missing-element warn-once: a row with no card/bar in ANY mounted panel.
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var found = false;
                for (var p = 0; p < this.panelCaches.Count; p++)
                {
                    var se = this.panelCaches[p].Slots[i];
                    if (se.Card != null || se.Bar != null)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && !this.warnedMissing[e.Slot])
                {
                    this.warnedMissing[e.Slot] = true;
                    var key = e.SlotName.IsEmpty ? e.Slot.ToString() : e.SlotName.ToString();
                    UnityEngine.Debug.LogWarning($"[DataUI] Row slot '{key}' ('{e.Label}') has no 'card-{key}'/'bar-{key}' element in the mounted UXML.");
                }
            }
        }

        private void EnsureSlots(int count)
        {
            if (this.slots.IsCreated && this.slots.Length == count)
            {
                return;
            }

            if (this.slots.IsCreated)
            {
                this.slots.Dispose();
            }

            this.slots = new NativeArray<HudSlotState>(count, Allocator.Persistent); // default → Init=false → first frame snaps
        }

        private void WarnUnhandledKind(FeedbackKind kind)
        {
            var bit = 1 << (int)kind;
            if ((this.warnedKindMask & bit) != 0)
            {
                return;
            }

            this.warnedKindMask |= bit;
            UnityEngine.Debug.LogWarning($"[DataUI] Feedback kind '{kind}' is not yet rendered by the HUD driver (only DamageChip/HealSurge/Flash). Event ignored (reserved).");
        }

        private static bool Changed(float a, float b) => math.abs(a - b) > BarFeedbackDefaults.GhostEpsilon;

        private static string AutoText(int curInt, int maxInt) =>
            maxInt > 0 ? $"{curInt} / {maxInt}" : curInt.ToString();

        private static float ReadValue(UIValueKind kind, ushort key,
            in DynamicBuffer<Intrinsic> intr, bool hasIntr, in DynamicBuffer<Stat> st, bool hasStat,
            in DynamicBuffer<ConditionEvent> ev, bool hasEvent)
        {
            switch (kind)
            {
                case UIValueKind.Stat:
                    return hasStat ? st.GetValueFloat((StatKey)key, 0f) : 0f;
                case UIValueKind.Event:
                    return hasEvent && ev.AsMap().TryGetValue(new ConditionKey(new BovineLabs.Core.BLId(key)), out var c) ? c.Read<int>() : 0f;
                default:
                    return hasIntr ? intr.GetValue((IntrinsicKey)key, 0) : 0f;
            }
        }

        /// <summary>Per-panel resolved elements for every row, rebuilt only when the panel set changes.</summary>
        private sealed class PanelCache
        {
            public VisualElement Panel;
            public SlotElements[] Slots;
        }

        /// <summary>One row's resolved elements + change-detection state for a single panel.</summary>
        private sealed class SlotElements
        {
            public VisualElement Card;
            public HudBar Bar;
            public Label Name;
            public Label Value;

            public float LastFill;
            public float LastGhost;
            public float LastLocked;
            public float LastFlash;
            public string LastValueText;
            public int LastCur = int.MinValue;
            public int LastMax = int.MinValue;
            public bool LastShow;
            public bool ConfigApplied;
            public bool LastPaused;
            public bool PauseApplied; // first push always forwards the pause state, then change-only
            public bool Primed; // first push writes every channel unconditionally, then change-only
        }
    }
}
