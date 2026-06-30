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
    /// The ONE generic data-UI driver. For every baked <see cref="UIBindingEntry"/> it resolves the source entity (via
    /// the generic <see cref="UISourceResolver"/>), reads ANY Essence value by kind+key (+ optional max + ghost), runs
    /// the shared <see cref="HudBarMath"/> for bar rows, and pushes the result onto NAMED elements of the mounted UXML
    /// panel(s): a <see cref="HudBar"/> named <c>bar-{slot}</c>, labels <c>name-{slot}</c> / <c>value-{slot}</c>, and a
    /// container <c>card-{slot}</c> (shown/hidden by Alive). Nothing here is health-specific, and the designer only
    /// authors the UXML (named elements + structure) + USS (all styling). Direct element push is used because UITK
    /// runtime DataBinding (data-source-type) does not resolve reliably in this Anchor setup. NOT [BurstCompile]:
    /// touches the managed <see cref="AnchorApp.Current"/>.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public partial struct DataUIDriverSystem : ISystem
    {
        private const int MaxRows = 64;
        public const string PanelClass = "vex-hud";

        private NativeArray<RowRuntime> runtime;

        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> sourcesLookup;
        private UnsafeBufferLookup<EntityLinkEntry> linksLookup;

        public void OnCreate(ref SystemState state)
        {
            this.runtime = new NativeArray<RowRuntime>(MaxRows, Allocator.Persistent);
            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this.sourcesLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            this.linksLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);

            state.RequireForUpdate<DataUITag>();
            state.RequireForUpdate<ControllableRegistry>();
        }

        public void OnDestroy(ref SystemState state)
        {
            this.runtime.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var app = AnchorApp.Current;
            if (app == null)
            {
                return;
            }

            // Every mounted data-UI panel (a UXML whose root carries the .vex-hud class). None yet → nothing to do.
            var panels = app.RootVisualElement.Query(className: PanelClass).Build().ToList();
            if (panels.Count == 0)
            {
                return;
            }

            var dt = math.min((float)SystemAPI.Time.DeltaTime, 0.1f);
            var time = (float)SystemAPI.Time.ElapsedTime;

            var entries = SystemAPI.GetSingletonBuffer<UIBindingEntry>(true);
            var players = SystemAPI.GetSingleton<ControllableRegistry>();
            var intrinsics = SystemAPI.GetBufferLookup<Intrinsic>(true);
            var stats = SystemAPI.GetBufferLookup<Stat>(true);
            var events = SystemAPI.GetBufferLookup<ConditionEvent>(true);

            this.targetsLookup.Update(ref state);
            this.sourcesLookup.Update(ref state);
            this.linksLookup.Update(ref state);
            state.Dependency.Complete();

            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var slot = (byte)math.min(e.Slot, MaxRows - 1);

                var alive = UISourceResolver.TryResolve(
                    e.Source, Entity.Null, players, this.targetsLookup, this.sourcesLookup, this.linksLookup, out var entity)
                    && entity != Entity.Null;

                float fill = 0f, ghost = 0f, flash = 0f, alpha = 1f, current = 0f, max = 0f;

                if (!alive)
                {
                    this.runtime[slot] = default;
                }
                else
                {
                    var hasIntr = intrinsics.TryGetBuffer(entity, out var intrBuf);
                    var hasStat = stats.TryGetBuffer(entity, out var statBuf);
                    var hasEvent = events.TryGetBuffer(entity, out var evBuf);

                    current = ReadValue(e.ValueKind, e.ValueKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent);
                    max = e.MaxKey != 0 ? ReadValue(e.MaxKind, e.MaxKey, intrBuf, hasIntr, statBuf, hasStat, evBuf, hasEvent) : 0f;
                    fill = HudBarMath.Fill(current, max);
                    ghost = fill;

                    var rt = this.runtime[slot];
                    if (e.Kind == UIRowKind.Bar)
                    {
                        var externalGhost = ExternalGhost(in e, intrBuf, hasIntr, statBuf, hasStat, max);
                        var rawCurrent = (int)math.round(current);
                        if (rt.Warmed == 0)
                        {
                            rt.Warmed = 1;
                            rt.Ghost = fill;
                            rt.LastSeenRaw = rawCurrent;
                            rt.IdleTime = e.AutoHideDelay;
                            rt.Alpha = e.StartVisible != 0 ? 1f : HudBarMath.TargetAlpha(e.AlwaysVisible != 0, 0,
                                e.KeepVisibleWhileNotFull != 0, e.ShowOnHealthChange != 0, fill, rt.IdleTime, e.AutoHideDelay);
                        }
                        else
                        {
                            var changed = rawCurrent != rt.LastSeenRaw;
                            var damaged = rawCurrent < rt.LastSeenRaw;
                            if (changed) rt.VisLatch = 0;
                            HudBarMath.GhostStep(e.GhostMode, fill, externalGhost, damaged, dt, e.GhostDelay, e.GhostSpeed, ref rt.Ghost, ref rt.GhostHoldTimer);
                            rt.Flash = HudBarMath.Flash(e.FlashOnDamage != 0, damaged, rt.Flash, dt, e.FlashDecay);
                            rt.IdleTime = changed ? 0f : rt.IdleTime + dt;
                            var target = HudBarMath.TargetAlpha(e.AlwaysVisible != 0, rt.VisLatch, e.KeepVisibleWhileNotFull != 0,
                                e.ShowOnHealthChange != 0, fill, rt.IdleTime, e.AutoHideDelay);
                            rt.Alpha = HudBarMath.StepAlpha(rt.Alpha, target, e.FadeInDuration, e.FadeOutDuration, dt);
                            rt.LastSeenRaw = rawCurrent;
                        }

                        ghost = rt.Ghost;
                        flash = rt.Flash;
                        alpha = rt.Alpha * HudBarMath.LowPulse(fill, e.PulseThreshold, e.PulseAmp, e.PulseSpeed, time);
                    }
                    else
                    {
                        rt.Alpha = HudBarMath.StepAlpha(rt.Alpha, 1f, e.FadeInDuration, e.FadeOutDuration, dt);
                        alpha = rt.Alpha;
                    }

                    this.runtime[slot] = rt;
                }

                Push(panels, slot, alive, math.saturate(fill), math.saturate(ghost), math.saturate(flash), math.saturate(alpha),
                    current, max, e.Label);
            }
        }

        // Push one row onto every mounted panel's named elements (bar-{slot}, name-{slot}, value-{slot}, card-{slot}).
        private static void Push(System.Collections.Generic.List<VisualElement> panels, byte slot, bool alive,
            float fill, float ghost, float flash, float alpha, float current, float max, FixedString64Bytes label)
        {
            for (var p = 0; p < panels.Count; p++)
            {
                var panel = panels[p];

                var card = panel.Q($"card-{slot}");
                if (card != null)
                {
                    card.style.display = alive ? DisplayStyle.Flex : DisplayStyle.None;
                    card.style.opacity = alpha;
                }

                if (panel.Q($"bar-{slot}") is HudBar bar)
                {
                    bar.value = fill;
                    bar.ghost = ghost;
                    bar.flash = flash;
                }

                if (panel.Q<Label>($"name-{slot}") is { } nameLabel)
                {
                    nameLabel.text = label.ToString();
                }

                if (panel.Q<Label>($"value-{slot}") is { } valueLabel)
                {
                    valueLabel.text = !alive ? string.Empty
                        : max > 0f ? $"{(int)math.round(current)} / {(int)math.round(max)}"
                        : ((int)math.round(current)).ToString();
                }
            }
        }

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

        private static float ExternalGhost(in UIBindingEntry e,
            in DynamicBuffer<Intrinsic> intr, bool hasIntr, in DynamicBuffer<Stat> st, bool hasStat, float max)
        {
            if (e.GhostKey == 0)
            {
                return 0f;
            }

            if (e.GhostMode == HudGhostMode.FromStat)
            {
                return HudBarMath.Fill(hasStat ? st.GetValueFloat((StatKey)e.GhostKey, 0f) : 0f, max);
            }

            if (e.GhostMode == HudGhostMode.FromIntrinsic)
            {
                return HudBarMath.Fill(hasIntr ? intr.GetValue((IntrinsicKey)e.GhostKey, 0) : 0, max);
            }

            return 0f;
        }

        private struct RowRuntime
        {
            public float Ghost;
            public float GhostHoldTimer;
            public float Alpha;
            public float IdleTime;
            public float Flash;
            public float PrevFill;
            public int LastSeenRaw;
            public byte Warmed;
            public byte VisLatch;
        }
    }
}
