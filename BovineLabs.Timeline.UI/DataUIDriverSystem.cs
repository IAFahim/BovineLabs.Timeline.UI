using BovineLabs.Anchor;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.UI.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The ONE generic data-UI driver — a DUMB renderer. It infers NOTHING. Each frame it reads the durable STRUCTURE
    /// values the game passes (current intrinsic, max stat, optional ghost-slider value, optional locked channel) and
    /// pushes them as geometry onto the mounted UXML elements (bar-/name-/value-/card-{slot}). The ghost slider's lag is
    /// driven by the game (a <see cref="BarGhost"/> value); the UI just renders it. No deltas, no decisions.
    /// Not [BurstCompile]: touches managed <see cref="AnchorApp.Current"/>.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct DataUIDriverSystem : ISystem
    {
        public const string PanelClass = "vex-hud";

        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> sourcesLookup;
        private UnsafeBufferLookup<EntityLinkEntry> linksLookup;
        private ComponentLookup<BarGhost> ghostLookup;
        private NativeList<float> chipScratch;
        private ulong warnedMissingMask;
        private ulong warnedBadFormatMask;

        public void OnCreate(ref SystemState state)
        {
            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this.sourcesLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            this.linksLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            this.ghostLookup = state.GetComponentLookup<BarGhost>(true);
            this.chipScratch = new NativeList<float>(16, Allocator.Persistent);

            state.RequireForUpdate<DataUITag>();
            state.RequireForUpdate<ControllableRegistry>();
        }

        public void OnDestroy(ref SystemState state)
        {
            this.chipScratch.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var app = AnchorApp.Current;
            if (app == null)
            {
                return;
            }

            var panels = app.RootVisualElement.Query(className: PanelClass).Build().ToList();
            if (panels.Count == 0)
            {
                return;
            }

            var entries = SystemAPI.GetSingletonBuffer<UIBindingEntry>(true);
            var players = SystemAPI.GetSingleton<ControllableRegistry>();
            var intrinsics = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var stats = SystemAPI.GetBufferLookup<Stat>(true);
            var events = SystemAPI.GetBufferLookup<ConditionEvent>(true);
            var feedback = SystemAPI.GetBufferLookup<BarFeedbackEvent>(false);

            this.targetsLookup.Update(ref state);
            this.sourcesLookup.Update(ref state);
            this.linksLookup.Update(ref state);
            this.ghostLookup.Update(ref state);
            state.Dependency.Complete();

            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var slot = e.Slot; // real index for element lookups; only the warn-mask shift is 64-guarded below

                var alive = UISourceResolver.TryResolve(
                    e.Source, Entity.Null, players, this.targetsLookup, this.sourcesLookup, this.linksLookup, out var entity)
                    && entity != Entity.Null;

                float fill = 0f, ghostFrac = 0f, lockedFrac = 0f, current = 0f, max = 0f;
                var ready = false; // a Bar row is "ready" only once its max denominator exists — else it's a false-empty bar
                this.chipScratch.Clear();

                if (alive)
                {
                    var hasIntr = intrinsics.TryGetBuffer(entity, out var intrBuf);
                    var hasStat = stats.TryGetBuffer(entity, out var statBuf);
                    var hasEvent = events.TryGetBuffer(entity, out var evBuf);

                    current = ReadValue(e.ValueKind, e.ValueKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent);
                    max = e.MaxKey != 0 ? ReadValue(e.MaxKind, e.MaxKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent) : 0f;
                    current = math.select(0f, current, math.isfinite(current)); // dumb renderer trusts clean structure; sanitize once
                    max = math.select(0f, max, math.isfinite(max));
                    ready = e.MaxKey == 0 || max > 0f;
                    fill = HudBarMath.Fill(current, max);

                    // ghost slider: a value the GAME passes (lags/leads); the UI just renders it. Default = fill (no band).
                    ghostFrac = fill;
                    if (this.ghostLookup.TryGetComponent(entity, out var bg) && max > 0f)
                    {
                        ghostFrac = math.saturate(bg.Value / max);
                    }

                    if (e.LockedKey != 0 && hasIntr)
                    {
                        var locked = intrBuf.GetValue((IntrinsicKey)e.LockedKey, 0);
                        lockedFrac = max > 0f ? math.saturate(locked / max) : 0f;
                    }

                    // FEEDBACK inbox → chip amounts (drain once, then clear). Damage is TOLD, never inferred.
                    if (max > 0f && feedback.TryGetBuffer(entity, out var fb) && fb.Length > 0)
                    {
                        for (var k = 0; k < fb.Length; k++)
                        {
                            if (fb[k].Kind == FeedbackKind.DamageChip)
                            {
                                this.chipScratch.Add(math.abs(fb[k].Amount) / max);
                            }
                        }

                        fb.Clear();
                    }
                }

                var show = alive && ready && (e.AlwaysVisible != 0 || (e.KeepVisibleWhileNotFull != 0 && fill < 0.999f) || math.abs(ghostFrac - fill) > 0.003f);

                this.Push(panels, slot, show, math.saturate(fill), math.saturate(ghostFrac), math.saturate(lockedFrac), current, max, e.Label, e.Format, in e);
            }
        }

        private void Push(System.Collections.Generic.List<VisualElement> panels, byte slot, bool show,
            float fill, float ghostFrac, float lockedFrac, float current, float max, FixedString64Bytes label, FixedString64Bytes format, in UIBindingEntry e)
        {
            // Value text computed ONCE and GUARDED — a pure renderer must never throw on a designer's malformed Format.
            var valueText = string.Empty;
            if (show)
            {
                if (!format.IsEmpty)
                {
                    try
                    {
                        valueText = string.Format(format.ToString(), (int)math.round(current), (int)math.round(max));
                    }
                    catch (System.FormatException)
                    {
                        valueText = AutoText(current, max);
                        if (slot < 64 && (this.warnedBadFormatMask & (1UL << slot)) == 0)
                        {
                            this.warnedBadFormatMask |= 1UL << slot;
                            UnityEngine.Debug.LogWarning($"[DataUI] Row {slot} ('{label}') Format '{format}' is invalid — using default. Use '{{0}}' (current) / '{{1}}' (max).");
                        }
                    }
                }
                else
                {
                    valueText = AutoText(current, max);
                }
            }

            var saw = false;
            for (var p = 0; p < panels.Count; p++)
            {
                var panel = panels[p];

                if (panel.Q($"card-{slot}") is { } card)
                {
                    saw = true;
                    card.EnableInClassList("is-hidden", !show);
                }

                if (panel.Q($"bar-{slot}") is HudBar bar)
                {
                    saw = true;
                    bar.SetTrailConfig((TrailMode)e.TrailMode, e.Accumulate != 0, e.HoldMs, e.DrainMs, e.MinDrainMs, (EaseId)e.DrainEase, e.Fade != 0, e.MinChipFrac);
                    bar.value = fill; // set BEFORE AddChip — the chip top = live fill + amount
                    bar.ghost = ghostFrac;
                    bar.locked = lockedFrac;
                    for (var k = 0; k < this.chipScratch.Length; k++)
                    {
                        bar.AddChip(this.chipScratch[k]);
                    }
                }

                if (panel.Q<Label>($"name-{slot}") is { } nameLabel)
                {
                    nameLabel.text = label.ToString();
                }

                if (panel.Q<Label>($"value-{slot}") is { } valueLabel)
                {
                    valueLabel.text = valueText;
                }
            }

            if (!saw && slot < 64 && (this.warnedMissingMask & (1UL << slot)) == 0)
            {
                this.warnedMissingMask |= 1UL << slot;
                UnityEngine.Debug.LogWarning($"[DataUI] Row slot {slot} ('{label}') has no 'card-{slot}'/'bar-{slot}' element in the mounted UXML.");
            }
        }

        private static string AutoText(float current, float max) =>
            max > 0f ? $"{(int)math.round(current)} / {(int)math.round(max)}" : ((int)math.round(current)).ToString();

        private static float ReadValue(UIValueKind kind, ushort key,
            in DynamicBuffer<Intrinsic> intr, bool hasIntr, in DynamicBuffer<Stat> st, bool hasStat,
            in DynamicBuffer<ConditionEvent> ev, bool hasEvent)
        {
            switch (kind)
            {
                case UIValueKind.Stat:
                    return hasStat ? st.GetValueFloat((StatKey)key, 0f) : 0f;
                case UIValueKind.Event:
                    return hasEvent && ev.AsMap().TryGetValue((ConditionKey)key, out var c) ? c : 0f;
                default:
                    return hasIntr ? intr.GetValue((IntrinsicKey)key, 0) : 0f;
            }
        }
    }
}
