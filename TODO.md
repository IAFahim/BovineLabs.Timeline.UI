# TODO.md — BovineLabs.Timeline.UI production audit

> **Verification status (2026-07-07):** All landed fixes compile clean (editor refresh finished 13:42:35Z, error console empty) and the full `BovineLabs.Timeline.UI` EditMode sweep is green: **96/96 passed, 0 failed, 0 skipped** at superproject HEAD `609b325`. Grid.Influence/VFXForge working-tree dirt is the concurrent session's work (submodule-internal mods / pointer drift from `e6ca6fd`,`ec90289`) — untouched by this audit's commits.

## Executive Summary

The package is a well-layered two-family system (reversible VisualElement effects + ViewModel/driver HUD) with genuinely good bones: pure, tested math helpers, cleanup-component-based revert, bake-time validation messages, and a clean Data/Runtime/Authoring asmdef split. The biggest risks are NOT in what exists — they are in what is **half-wired**:

1. **A large slice of the designer-facing data model is dead.** `BarFeedbackProfile` (collapseTrigger/collapseEvent/maxHoldMs/drainRate/fadeMs/healEase/healDrainMs) and `UIBindingEntry` (GhostMode/GhostKey/GhostDelay/GhostSpeed/FlashOnDamage/FlashDecay/ShowOnHealthChange/AutoHideDelay) bake fields the runtime driver never reads. `HudBarMath` (GhostStep/Flash/TargetAlpha/LowPulse) is a tested behavior kernel that **no runtime system calls**. Designers will tune knobs that do nothing — the worst class of production bug because it's silent and erodes trust in every other knob.
2. **Destructive, single-consumer event read.** `DataUIDriverSystem` clears the simulation-owned `BarFeedbackEvent` buffer; a second consumer (world-space bar) misses everything, and when no HUD panel is mounted the buffer grows unbounded.
3. **A missing dependency-completion in `DataDisplayTrackSystem`** (its two siblings both call `state.Dependency.Complete()`; it doesn't) — a jobs-safety exception waiting for the first project that writes `IdValue` from a job.
4. **Reversible-effect collisions**: two overlapping `UssClassClip`s on the same class/element, or two overlapping `UITextRevealClip`s on the same label, corrupt each other's revert state.
5. **Per-frame GC + layout churn** in the presentation hot path (`Query().ToList()`, `$"card-{slot}"` ×4 per row per panel per frame, `Substring` per frame in text reveal, 3× `ApplyStructure` per bar per frame).

None of these require a rewrite. The architecture direction (see bottom) is: make `HudBarMath` the single behavior kernel, add a per-slot cached presentation state, make feedback consumption non-destructive, and push all silent misconfigurations into bake-time/editor-time errors.

## System Inventory

**Family A — reversible VisualElement effects** (managed `SystemBase`, `TimelineComponentAnimationGroup`):
- `ReversibleEffectSystem<TData,TInverse,TCleanup>` — enter/exit engine keyed on `TimelineActive+ClipActive` with `ICleanupComponentData` for guaranteed revert (incl. entity destruction). Retries `TryApply` each frame until the target exists; warns once per entity.
- `UxmlViewTrackSystem` — instantiates UXML via Anchor `IUXMLService`, attaches per `UxmlAttach.PlanAttach`, removes on exit.
- `UssClassTrackSystem` (`UpdateAfter` Uxml) — adds a USS class, restores prior presence on exit.
- `UITextRevealTrackSystem` (`UpdateAfter` UssClass, `Animated=true`) — typewriter via `TextReveal` + `LocalTime`/`TimeTransform`; restores original text.

**Family B — ViewModel / HUD**:
- `NumberTrackSystem`, `RowsTrackSystem` (debug), `DataDisplayTrackSystem`, `EssenceUITrackSystem` — fold active clips into Anchor `SystemObservableObject` ViewModels via `UIHelper`.
- `DataUIDriverSystem` (`PresentationSystemGroup`) — settings-driven HUD: resolves `UIBindingEntry` rows → entity via `UISourceResolver` → pushes fill/ghost/locked/chips/text into named elements (`card-{i}`, `bar-{i}`, `name-{i}`, `value-{i}`) inside panels classed `vex-hud`.
- `ControllableRegistrySystem` (`InitializationSystemGroup`) — rebuilds `ControllableRegistry` (`NativeArray<Entity>[256]` by `PlayerId.Value` byte) every frame; ties break by lowest entity index (`ControllableSelection`).

**Widgets / Views**: `HudBar` (track/fill/ghost/chip/locked/frame sub-elements, hold→drain chip animation on the VisualElement scheduler), `NodeChip`, `EssenceUIView`, `RowsView` (debug asmdef).

**Data/kernels**: `HudBarMath`, `VexEase`, `TextReveal`, `UxmlAttach`, `UIFraction`, `IdValueLookup`, `NumberFold`, `EssenceUIDecay`, `EssenceUIBounds`; components in `*.Data`.

**Authoring**: 6 track/clip pairs; `DataUISettings` (SettingsBase → `UIPanelEntry`/`UIBindingEntry` buffers, with bake-time warnings); `EssenceBarSource` + `BarFeedbackProfile` (shared world+HUD source-of-truth assets); `HealthSchemaObject` (AutoRef IUID); `UISourceAuthoring`.

**Tests**: solid unit coverage of the pure kernels; `UISourceResolverTests` via ECS fixture. No coverage of `ReversibleEffectSystem` lifecycle, driver, or `HudBar`.

## Dependency & Flow Map

- Timeline core enables/disables `TimelineActive`/`ClipActive` (Timeline systems, `TimelineSystemGroup`) → Family A/B systems react in `TimelineComponentAnimationGroup` (same group, after update).
- `DataUISettings.Bake` → singleton `DataUITag` + `UIBindingEntry` buffer → `DataUIDriverSystem` (requires `DataUITag` + `ControllableRegistry`) → resolves entities → reads `Intrinsic`/`Stat`/`ConditionEvent` buffers + optional `BarGhost` + consumes `BarFeedbackEvent` → mutates `HudBar`/labels found by name in any `.vex-hud` panel.
- **Nothing in this package produces `BarGhost`/`BarFeedbackEvent`** — that contract is fulfilled by game code (sample: `CombatConductorSystem`). The ghost smoothing configured in `EssenceBarSource.ghostMode` is *not* what smooths the HUD ghost; the producer is.
- Family A depends on a live `AnchorApp.Current.RootVisualElement` (+ `IUXMLService` for Uxml track); all retry until ready.
- `EssenceUITrackSystem` `UpdateAfter` the three Timeline-Essence mutation systems so the HUD reads post-mutation values — correct.
- Hidden contract: `Hud.uxml` element names `card-{i}`… must match `DataUISettings.Rows` **list order** (index = slot). Reordering rows silently rebinds every card.

## Critical TODOs

### TODO: Wire or delete the dead bar-behavior config (HudBarMath + baked fields nobody reads)

**Priority:** Critical
**Certainty:** Confirmed
**Lens:** Designer Safety / Architecture
**Files/Systems Involved:** `DataUIDriverSystem.cs`, `HudBarMath.cs`, `DataUISettings.cs`, `BarFeedbackProfile.cs`, `EssenceBarSource.cs`, `UIBindingEntry.cs`, `HudBar.cs`
**Problem:** The driver never reads: `GhostMode`/`GhostKey`/`GhostDelay`/`GhostSpeed` (ghost comes only from the external `BarGhost` component), `FlashOnDamage`/`FlashDecay` (no flash path exists at all — `HudBar` doesn't even build a `__flash` element though `Hud.uss` styles one), `ShowOnHealthChange`/`AutoHideDelay` (show logic is a hardcoded 3-term expression). `BarFeedbackProfile.collapseTrigger`, `collapseEvent`, `maxHoldMs`, `drainRate` (tooltip promises "drainMs==0 → use drainRate"; `HudBar.Collapse` does `max(minDrainMs, drainMs)` so 0 → `minDrainMs`), `fadeMs`, `healEase`, `healDrainMs` are never baked into `UIBindingEntry`. `HudBarMath.GhostStep/Flash/TargetAlpha/LowPulse` — the tested kernel — has zero runtime callers.
**Evidence:** Grep `UIBindingEntry` field reads in `DataUIDriverSystem.OnUpdate/Push`: only `Slot, Source, ValueKind/Key, MaxKind/Key, LockedKey, Label, Format, AlwaysVisible, KeepVisibleWhileNotFull, TrailMode, Accumulate, HoldMs, DrainMs, MinDrainMs, DrainEase, Fade, MinChipFrac` are used. No caller of `HudBarMath.*` outside tests.
**Why It Matters:** A designer sets `ghostMode = ComputedLerp, ghostDelay = 0.8` or `FlashOnDamage` and sees no change. Every silent knob poisons trust in the whole settings asset; "which fields actually work" becomes tribal knowledge — a AAA workflow killer.
**Suggested Change:** Pick per field: **wire it** (preferred for ghost/flash/visibility — the kernel already exists) or **delete it** from the authoring surface. Concretely: (a) give the driver a per-slot runtime state struct (`ghost`, `holdTimer`, `flash`, `idle`, `lastFill`) and call `HudBarMath.GhostStep`, `HudBarMath.Flash`, `HudBarMath.TargetAlpha` with the baked fields; (b) add the `__flash` overlay element to `HudBar` and expose `flash01`; (c) bake `drainRate`/`fadeMs`/`healEase`/`healDrainMs` into `UIBindingEntry` and honor them in `HudBar` (drainRate: `dur = band/rate` when `drainMs==0`); (d) implement `CollapseTrigger.Signaled/Both` by reading the `collapseEvent` ConditionKey on the resolved entity, with `maxHoldMs` as the cap — or delete the three fields and the enum.
**Implementation Path:** 1) Add `struct SlotState { float Ghost, HoldTimer, Flash, Idle, LastFill; }` + `NativeList<SlotState>` sized to entries in `OnCreate`. 2) In the per-entry loop compute `damaged = fill < state.LastFill - epsilon`. 3) Replace the `BarGhost`-only ghost read with: external key when `GhostMode is FromStat/FromIntrinsic` (read `GhostKey`), `HudBarMath.GhostStep` when `ComputedLerp`, `BarGhost` component only as an optional override. 4) `flash = HudBarMath.Flash(e.FlashOnDamage!=0, damaged, state.Flash, dt, e.FlashDecay)`; push to the new `HudBar.flash`. 5) Replace the hand-rolled `show` expression with `HudBarMath.TargetAlpha(...)` fed by `state.Idle` (reset on change). 6) Delete or bake the remaining profile fields.
**How to Verify:** HUD Showcase: set each knob to an extreme (ghostSpeed 0.5 vs 50, flashDecay 2, autoHideDelay 1) and confirm visible change; add a play-mode test asserting `GhostStep` is invoked (or assert dead fields no longer exist).
**Tradeoffs:** Wiring visibility/flash grows driver state; deleting fields breaks serialized assets (needs `FormerlySerializedAs`-style migration or acceptance). Do NOT ship the current middle ground.
**Confidence:** High

### TODO: Make BarFeedbackEvent consumption non-destructive and bounded

**Priority:** Critical
**Certainty:** Confirmed
**Lens:** Event / Architecture
**Files/Systems Involved:** `DataUIDriverSystem.cs`, `BarFeedback.cs`, (producers: game code, sample `CombatConductorSystem`)
**Problem:** The driver does `fb.Clear()` after reading — a destructive read of a simulation-owned buffer. (a) Any second consumer (world-space health bar, kill-feed, damage numbers) misses every event the HUD ate first. (b) The clear only happens when the row is `alive` **and** panels exist; the method early-returns when `AnchorApp` is null or no `.vex-hud` panel is mounted (menus, loading, dedicated worlds) → the buffer grows forever on every damaged entity. (c) Only `FeedbackKind.DamageChip` is used; the other 10 kinds (HealSurge, ShieldBreak, Crit, LockForm…) are silently swallowed *and destroyed*.
**Evidence:** `if (feedback.TryGetBuffer(entity, out var fb) && fb.Length > 0) { …DamageChip only… fb.Clear(); }` inside `if (alive)`, after `if (panels.Count == 0) return;`.
**Why It Matters:** Unbounded memory growth in headless/menu states; impossible to add a second presentation consumer later without a refactor; heal/shield/crit feedback authored by designers vanishes with no error.
**Suggested Change:** Introduce a single owner of consumption: a tiny `BarFeedbackDrainSystem` (end of frame, always runs, no UI dependency) that moves events into per-entity presentation state (or per-reader cursors: keep events one frame with a frame stamp and let the *producer's* group clear last-frame events). The HUD driver becomes a pure reader. Also cap the buffer (`if (fb.Length > 64) fb.RemoveRange(0, fb.Length-64)`) in the drain system as a safety valve, and log-once on unhandled kinds instead of dropping them.
**Implementation Path:** 1) Add `BarFeedbackFrame { uint Frame; }` per event OR a `LastDrainedFrame` per entity. 2) New system in `LateSimulationSystemGroup` (runs in all worlds) clears events older than 1 frame and enforces the cap. 3) Driver reads without clearing. 4) Route non-DamageChip kinds: `HealSurge` → green ghost lead, `Flash` → flash01, unknown → warn-once.
**Snippet/Pseudocode:**
```csharp
// LateSimulation, always-on:
foreach (var fb in Query<DynamicBuffer<BarFeedbackEvent>>()) {
    for (int i = fb.Length - 1; i >= 0; i--)
        if (frame - fb[i].Frame >= 1) fb.RemoveAtSwapBack(i);   // readers had one full frame
    if (fb.Length > Cap) { WarnOnce(); fb.ResizeUninitialized(Cap); }
}
```
**How to Verify:** Play HUD Showcase, unmount the HUD (kill the panel), let the conductor run 5 min, assert buffer length stays ≤ cap; add a second reader system and assert both see the same DamageChip.
**Tradeoffs:** One extra frame of latency for readers that run before the producer; frame-stamp adds 4 bytes per event. Far cheaper than the alternative bug class.
**Confidence:** High

### TODO: Fix missing dependency completion in DataDisplayTrackSystem

**Priority:** Critical
**Certainty:** Strongly Likely
**Lens:** Timing / Event
**Files/Systems Involved:** `DataDisplayTrackSystem.cs`
**Problem:** It reads `SystemAPI.GetBufferLookup<IdValue>(true)` on the main thread inside its query loop without completing dependencies. Its two siblings that read cross-entity lookups (`EssenceUITrackSystem`, `DataUIDriverSystem`) both call `state.Dependency.Complete()` first. `SystemAPI.Query` auto-completes only the queried types (`ClipDataId`, `TrackBinding`) — not `IdValue`. The first project that writes `IdValue` from a scheduled job gets a safety exception (or, with safety off, a race reading half-written values).
**Evidence:** Diff the three systems' OnUpdate preambles; only this one lacks `Complete()`.
**Why It Matters:** Works today because sample producers write `IdValue` on the main thread. Breaks unpredictably in consumer projects — the worst kind of package bug.
**Suggested Change:** Add `state.CompleteDependencyBeforeRO<IdValue>()` (narrow) — prefer this over a blanket `state.Dependency.Complete()`.
**How to Verify:** Test with a producer system that writes `IdValue` via `IJobEntity` scheduled; without the fix the safety system throws.
**Tradeoffs:** None meaningful; narrow completion avoids a full sync point.
**Confidence:** High

### TODO: Ref-count overlapping reversible effects (USS class double-apply, text-reveal capture corruption)

**Priority:** Critical
**Certainty:** Confirmed
**Lens:** State
**Files/Systems Involved:** `UssClassTrackSystem.cs`, `UITextRevealTrackSystem.cs`, `ReversibleEffectSystem.cs`
**Problem:** Two overlapping `UssClassClip`s adding the same class to the same element: A applies (`wasPresent=false`), B applies (`wasPresent=true`). A ends first → `RemoveFromClassList` while B is still active → styling vanishes mid-clip; when B ends it removes nothing. Symmetrically, two overlapping `UITextRevealClip`s on one label: B captures A's *partially revealed* string as `Original`; whichever exits last restores garbage. Designers WILL overlap clips on shared elements (crossfades, two tracks).
**Evidence:** `AppliedClass.WasPresent` semantics + `Revert`; `CapturedText.Original = target.text` at apply time.
**Why It Matters:** Timeline-driven UI is the package's whole point; overlap is a first-class Timeline idiom. Corruption is order-dependent and unreproducible in bug reports.
**Suggested Change:** (a) USS: keep a static per-(element,class) ref-count in the system; apply on 0→1, remove on 1→0; `WasPresent` only for the first holder. (b) TextReveal: capture `Original` from a per-element "base text" registry — first clip registers the true original; later clips reuse it; last exit restores and unregisters. (c) Optionally: bake-time warning when two clips on the *same track* overlap with identical TargetId/ClassName.
**Implementation Path:** Add `Dictionary<(VisualElement, string), int> refCounts` to `UssClassTrackSystem`; `TryApply` increments, `Revert` decrements and removes at zero. For reveal, `Dictionary<TextElement, string> baseText` with add-on-first/restore-on-last.
**How to Verify:** New play-mode test: two entities, same class/element, staggered ClipActive toggles; assert class present until *both* exit; same for text.
**Tradeoffs:** Static dictionaries need clearing in `OnDestroy` (already the pattern via `outstanding`). Slight complexity; correctness demands it.
**Confidence:** High

## High Priority TODOs

### TODO: Kill per-frame GC + repeated element queries in DataUIDriverSystem

**Priority:** High
**Certainty:** Confirmed
**Lens:** Performance
**Files/Systems Involved:** `DataUIDriverSystem.cs`
**Problem:** Every frame: `RootVisualElement.Query(className).Build().ToList()` (list + query allocs), then per row × per panel: `panel.Q($"card-{slot}")`, `$"bar-{slot}"`, `$"name-{slot}"`, `$"value-{slot}"` (4 interpolated strings + 4 tree walks), `label.ToString()`, `string.Format`/`AutoText` (string allocs), and unconditional `nameLabel.text = …` / `valueLabel.text = …` writes that dirty text layout even when unchanged.
**Evidence:** `OnUpdate` + `Push` bodies.
**Why It Matters:** With 4–8 rows × split-screen panels this is hundreds of allocations and UQuery walks per frame in `PresentationSystemGroup` — GC spikes on the render-critical path, scaling linearly with rows × panels.
**Suggested Change:** Cache a `SlotElements { VisualElement Card; HudBar Bar; Label Name, Value; string LastText; float LastFill, LastGhost, LastLocked; bool LastShow; }[]` per panel, keyed by the panel reference; rebuild only when the panel set changes (track `AttachToPanel`/list identity or an int version from a cheap `Query` count) . Precompute the 4 name strings once per slot at cache build. Write `.text` only when the string actually changed; skip `Set…` calls when values are within epsilon of last-pushed.
**Implementation Path:** 1) `List<PanelCache>` field; `RebuildCache(root)` when `panels.Count`/identity differs. 2) Compare-and-set in `Push`. 3) Replace `AutoText`/`string.Format` with a cached `(int cur,int max) → string` guarded by change detection (ints change far less often than floats).
**How to Verify:** Profiler (GC Alloc column) on HUD Showcase: 0 B/frame steady-state; text layout markers absent when values are static.
**Tradeoffs:** Cache invalidation logic (panel remount, UxmlViewTrack mounting a second `.vex-hud` mid-play) — handle via a re-query every N frames or an Anchor mount hook.
**Confidence:** High

### TODO: Stop UITextRevealTrackSystem allocating and dirtying text every frame

**Priority:** High
**Certainty:** Confirmed
**Lens:** Performance / Animation
**Files/Systems Involved:** `UITextRevealTrackSystem.cs`
**Problem:** `Advance` runs every frame per active clip and does `full.Substring(0, visible)` + `Element.text = …` unconditionally — a fresh string and a full text relayout even when `visible` hasn't changed (typical at high FPS: a 30-char line over 3 s changes ~10×/s but allocates 144×/s at 144 fps).
**Evidence:** `Advance` body; no last-visible cache (CapturedText is immutable).
**Suggested Change:** Track last revealed count per entity (dictionary alongside `outstanding`, or make `TInverse` a small class holding `LastVisible`); early-out when unchanged. Optionally pre-split: for very long strings cache substrings is overkill — the early-out alone removes ~90% of the cost.
**How to Verify:** Profiler during a reveal: allocations only when the count increments.
**Tradeoffs:** None.
**Confidence:** High

### TODO: HudBar — fix the m_Collapsing latch (frozen chip after detach) and batch ApplyStructure

**Priority:** High
**Certainty:** Confirmed (latch: Strongly Likely repro)
**Lens:** State / Performance
**Files/Systems Involved:** `HudBar.cs`, `DataUIDriverSystem.Push`
**Problem:** (a) `Collapse()` starts a `ValueAnimation` and sets `m_Collapsing = true`; `OnCompleted` is the only thing that clears it. If the bar is detached from the panel mid-collapse (panel unmount, UxmlViewTrack clip end, scene reload), the animation dies without completing → `m_Collapsing` stays true forever → `ApplyChip()` early-returns forever → the chip window is permanently frozen on re-attach. `m_HoldItem` is similarly panel-scheduled. (b) The driver sets `bar.value`, `bar.ghost`, `bar.locked` as three separate properties — each setter calls `ApplyStructure()` → 3 full style passes per bar per frame. (c) `SetTrailConfig` is re-applied every frame (cheap but noisy).
**Evidence:** `Collapse`/`ApplyChip`/property setters; `Push` call order.
**Suggested Change:** (a) Register `DetachFromPanelEvent` → stop `m_Collapse`, clear `m_Collapsing`, reset chip to `m_Value`, pause `m_HoldItem`; on `AttachToPanelEvent` re-`ApplyStructure`. (b) Add `SetState(float fill, float ghost, float locked)` doing one `ApplyStructure`; keep setters for UXML/bindings. (c) Only call `SetTrailConfig` when the entry changed (config is bake-static — once at cache build).
**Snippet/Pseudocode:**
```csharp
this.RegisterCallback<DetachFromPanelEvent>(_ => {
    m_Collapse?.Stop(); m_Collapse = null; m_Collapsing = false;
    m_HoldItem?.Pause(); m_HoldItem = null;
    m_ChipHi = m_Value;
});
```
**How to Verify:** Play-mode: start a collapse, `RemoveFromHierarchy`, re-add, deal damage — chip must animate again.
**Confidence:** High

### TODO: Views break permanently after the first panel detach (unsubscribe-without-resubscribe)

**Priority:** High
**Certainty:** Confirmed
**Lens:** Event / State
**Files/Systems Involved:** `EssenceUIView.cs`, `RowsView.cs`
**Problem:** Constructor subscribes `ViewModel.PropertyChanged` and registers `DetachFromPanelEvent → unsubscribe`. There is no `AttachToPanelEvent` counterpart, so after any detach/re-attach (tab switch, panel rebuild, UxmlViewTrack remount of a parent) the view never refreshes again — grids show stale data silently. `Dispose()` duplicates the unsubscribe but nothing calls it reliably.
**Evidence:** Constructor bodies of both views.
**Suggested Change:** Pair the callbacks: subscribe in `AttachToPanelEvent`, unsubscribe in `DetachFromPanelEvent`; drop the constructor subscription (or keep it and guard double-subscribe). Also re-pull `itemsSource` + `Refresh()` on attach so a re-attached view resyncs immediately.
**How to Verify:** Play-mode: remove and re-add the view element; mutate a stat; grid must update.
**Tradeoffs:** None.
**Confidence:** High

### TODO: Remove ServerSimulation from presentation systems' WorldSystemFilter

**Priority:** High
**Certainty:** Confirmed
**Lens:** Architecture / Performance
**Files/Systems Involved:** `UxmlViewTrackSystem`, `UssClassTrackSystem`, `UITextRevealTrackSystem`, `NumberTrackSystem`, `RowsTrackSystem`, `DataDisplayTrackSystem`, `EssenceUITrackSystem` (all declare `ServerSimulation`); contrast `DataUIDriverSystem` (correctly omits it)
**Problem:** Seven UI-mutating / ViewModel systems are registered into server worlds. They no-op via the `AnchorApp.Current == null` guard, but still create queries, tick, `UIHelper.Bind` in `OnStartRunning` (server-side service resolution — behavior depends on Anchor, potentially exceptions), and allocate scratch lists per world.
**Evidence:** `WorldSystemFilterFlags.ServerSimulation` present in the attributes; `DataUIDriverSystem` is the only one without it — the inconsistency shows the intent.
**Suggested Change:** Drop `ServerSimulation` from all Family A/B presentation systems. `ControllableRegistrySystem` may keep it only if server logic reads the registry (nothing in this package does — audit consumers first).
**How to Verify:** Create a server world in a test; assert these systems are absent.
**Confidence:** High

### TODO: Narrow the hard sync points (Dependency.Complete) in EssenceUITrackSystem and DataUIDriverSystem

**Priority:** High
**Certainty:** Confirmed
**Lens:** Performance / Timing
**Files/Systems Involved:** `EssenceUITrackSystem.cs`, `DataUIDriverSystem.cs`
**Problem:** Both call `state.Dependency.Complete()` every frame — a full-graph sync on the main thread. They only need read access to `Stat`/`Intrinsic`/`ConditionEvent`/`IdValue`/`Targets`/link buffers (+ RW on `BarFeedbackEvent`).
**Suggested Change:** Replace with targeted `state.CompleteDependencyBeforeRO<Stat>(); …BeforeRO<Intrinsic>(); …BeforeRO<ConditionEvent>();` and `CompleteDependencyBeforeRW<BarFeedbackEvent>()` in the driver. Main-thread UI reads are unavoidable; syncing *everything* is not.
**How to Verify:** Profiler timeline: worker jobs unrelated to Essence keep running across these systems.
**Tradeoffs:** Slightly more verbose; each new lookup must be added to the completion list — add a comment block tying them together.
**Confidence:** High

### TODO: Handle (or loudly reject) the 10 unhandled FeedbackKind values

**Priority:** High
**Certainty:** Confirmed
**Lens:** Designer Safety / Event
**Files/Systems Involved:** `BarFeedback.cs`, `DataUIDriverSystem.cs`, `HudBar.cs`
**Problem:** `FeedbackKind` advertises DamageChip, HealSurge, Flash, ShieldHit/Break/Gain, Overheal, Block, Crit, LockForm/Lift. The driver consumes only `DamageChip`; everything else is dropped (and, per the destructive-read bug above, destroyed). `PoolKey`/`Element`/`Flags` are likewise dead.
**Suggested Change:** Minimum bar for production: warn-once per unhandled kind at the drain point, and document the enum as "reserved". Better: implement the cheap ones now — `HealSurge` → drive the existing green ghost-lead path; `Flash` → `flash01 = 1` once the flash overlay exists (see Critical #1); `LockForm/Lift` → they're already representable via `LockedKey`, so either delete the kinds or emit them from a lock producer.
**How to Verify:** Emit each kind from a test conductor; no silent drops — every kind either renders or warns.
**Confidence:** High

### TODO: Typewriter reveal corrupts rich text; grapheme clusters partially handled

**Priority:** High
**Certainty:** Confirmed (rich text) / Strongly Likely (clusters)
**Lens:** Animation / Designer Safety
**Files/Systems Involved:** `UITextRevealTrackSystem.cs`, `TextReveal.cs`, `UITextRevealClip.cs`
**Problem:** `Substring(0, visible)` on a `TextElement` with `enableRichText` (default true) reveals partial tags — `<b` flashes as literal text, `<color=…>` half-applied states flicker. `BumpHighSurrogate` fixes lone surrogate pairs but not ZWJ sequences (👨‍👩‍👧) or combining marks — partial clusters render as tofu for a frame.
**Suggested Change:** (a) Simplest correct fix for markup: when the source text contains `<`, reveal by index over a *tag-stripped* mapping — precompute at `TryApply` an array mapping visible-char-index → source-substring-length that always includes complete tags (and always closes open tags, or simply always emit the full string with the invisible tail wrapped in `<alpha=#00>`; TextElement supports TMP-style alpha tag via UITK text — verify against the project's text backend, else fall back to stripping). (b) Replace `BumpHighSurrogate` with `System.Globalization.StringInfo` grapheme boundaries, precomputed once into an int[] at apply time (never per frame).
**Implementation Path:** Precompute `int[] revealOffsets` in `CapturedText` (grapheme- and tag-aware); `Advance` indexes it.
**How to Verify:** Unit tests: `"a<b>bold</b>"` never yields a string with an unclosed/partial tag; `"👨‍👩‍👧x"` reveals in 2 steps not 6.
**Tradeoffs:** Precompute cost at clip start (trivial ≤512 bytes). The `<alpha>` approach avoids relayout entirely if supported — measure both.
**Confidence:** High

### TODO: DataUISettings row identity is list-index — reordering silently rewires every HUD card

**Priority:** High
**Certainty:** Confirmed
**Lens:** Designer Safety
**Files/Systems Involved:** `DataUISettings.cs`, `Hud.uxml` contract (`card-{i}` naming), `UIBindingEntry.Slot`
**Problem:** `Slot = (byte)i` couples a settings-list position to UXML element names. Inserting a row above another shifts every subsequent binding to a different card — no error, wrong data on every card below the insertion. Also `(byte)i` silently wraps past 255 rows, and the runtime warn masks (`slot < 64`) go silent past 64.
**Suggested Change:** Add an explicit optional `SlotName` (string) per Entry; when set, bind `card-{SlotName}` instead of the index. Keep index as fallback for existing content. Bake-time: error on duplicate resolved slots; error (not wrap) on >255 rows; extend or remove the 64-slot warn-mask limit (a `NativeHashSet<byte>`/`bool[256]` costs nothing).
**How to Verify:** Reorder rows in the showcase with SlotNames set — cards keep their data. Bake with a duplicate slot → error.
**Tradeoffs:** Two addressing modes; the explicit one should become the documented default.
**Confidence:** High

## Medium Priority TODOs

### TODO: Decide scaled vs unscaled time for UI feedback (event decay, chip drain, pause)

**Priority:** Medium
**Certainty:** Confirmed (mechanics) / Risk (desired behavior)
**Lens:** Timing
**Files/Systems Involved:** `EssenceUITrackSystem.DecayActiveEvents` (`SystemAPI.Time.DeltaTime`), `HudBar` hold/drain (`schedule.Execute` + `experimental.animation` = wall-clock ms), `HudBarMath.GhostStep` callers-to-be
**Problem:** Two clocks are mixed. `ActiveUIEvent.TimeRemaining` decays with *scaled* delta — under `WorldTimeScaleTrack` bullet-time (×0.1) an event toast configured for 2 s lingers 20 s; at timescale 0 it never expires. Meanwhile `HudBar` chip hold/drain runs on the *unscaled* VisualElement scheduler — chips keep draining while the game is paused (bl-core `PauseGame`) or frozen, so a paused screenshot shows feedback mid-animation drift.
**Suggested Change:** Pick one policy and encode it: HUD feedback timing = **unscaled** presentation time (usual AAA choice) — decay `ActiveUIEvent` with `UnityEngine.Time.unscaledDeltaTime` (or an unscaled clock singleton) and *keep* HudBar on the scheduler, but suspend HudBar animations while `PauseGame` is active if paused-HUD-freeze is desired (expose `HudBar.SetPaused(bool)` toggled by the driver from the pause singleton).
**How to Verify:** WorldTimeScale 0.1 → toast still ~2 s wall time; pause mid-chip → chip frozen (if freeze policy chosen).
**Tradeoffs:** Whichever policy — document it on the fields (`DisplayDuration` tooltip: "seconds, unscaled").
**Confidence:** Medium (policy), High (current inconsistency)

### TODO: ControllableRegistry hardening — duplicate PlayerId warning, dead Version, disposal aliasing

**Priority:** Medium
**Certainty:** Confirmed
**Lens:** Validation / Architecture
**Files/Systems Involved:** `ControllableRegistrySystem.cs`, `ControllableRegistry.cs`, `ControllableSelection.cs`
**Problem:** (a) Two `Controllable` entities with the same `PlayerId` are resolved silently by lowest `Entity.Index` — an arbitrary, spawn-order-dependent winner; designer sees "HUD shows the wrong character" with no clue. (b) `Version` is incremented every frame and read by nobody — it can't be used for change detection as-is (it changes even when content didn't). (c) The singleton component holds a `NativeArray` the system disposes in `OnDestroy` — any consumer caching the component across teardown reads freed memory; also the array is written in `InitializationSystemGroup` while readers hold no safety handles on it (raw aliasing, works only because everything is main-thread).
**Suggested Change:** (a) warn-once when `byPlayer[idx]` is already non-null with a *different* entity. (b) Bump `Version` only when an entry changed (compare-before-write), or delete the field. (c) Replace `NativeArray`-in-component with a `DynamicBuffer<ControllableEntry>` on the singleton (safety-tracked, survives system order changes) — index by player, 256 entries.
**How to Verify:** Spawn two Controllables PlayerId=0 → one warning; registry tests still pass.
**Confidence:** High

### TODO: Bake-time validation gaps (Id==0 schemas, null array entries, >255 rows, panel keys)

**Priority:** Medium
**Certainty:** Confirmed
**Lens:** Validation
**Files/Systems Involved:** `DataDisplayClip.cs`, `HealthSchemaObject.cs`, `EssenceUIClip.cs`, `DataUISettings.cs`, `UxmlViewClip.cs`
**Problem:** (a) `HealthSchemaObject.Id == 0` (AutoRef not yet run — the UIShowcase builder even has a workaround re-run message) bakes `ClipDataId{Id=0}` which happily matches `IdValue{Id=0}` — wrong data, no warning. (b) Null entries in `DataDisplayClip.Health`, `EssenceUIClip.Stats/Intrinsics/Events` are skipped silently. (c) `DataUISettings.Rows.Count > 255` wraps `Slot`. (d) `Panels` keys and `UxmlViewClip.UxmlKey` are never checked against registered Anchor views until runtime warn.
**Suggested Change:** In each `Bake`: `Debug.LogError` (with context object) on Id==0, warn on null array entries naming the clip and index, error past 255 rows. For UXML keys, add an editor-time validator (see Debugging/Tooling) that cross-references AnchorSettings' view registry — bake can't reach the runtime service, but the settings asset is inspectable in-editor.
**How to Verify:** Author each bad state; every one produces a ping-able message before play mode.
**Confidence:** High

### TODO: Deduplicate defaults and thresholds (profile fallbacks, fill-full epsilon)

**Priority:** Medium
**Certainty:** Confirmed
**Lens:** Architecture / Validation
**Files/Systems Involved:** `DataUISettings.Bake` (prof==null → HoldMs 400/DrainMs 500), `BarFeedbackProfile` (field defaults 350/450), `DataUIDriverSystem` (`fill < 0.999f`), `HudBarMath.TargetAlpha` (`1f - 1e-3f`), `HudBar` (`0.003f` ghost epsilon vs driver's `0.003f` — currently equal by luck)
**Problem:** The "no profile" defaults in the baker differ from the profile asset's own defaults, so adding an empty profile *changes* behavior; near-full/near-equal epsilons are scattered literals.
**Suggested Change:** `public static class BarFeedbackDefaults { public const float HoldMs = 350f; … public const float FullEpsilon = 1e-3f; public const float GhostEpsilon = 0.003f; }` in the Data assembly; baker, profile field initializers, driver, HudBar, and HudBarMath all reference it.
**Confidence:** High

### TODO: UxmlViewTrackSystem failure message is misleading; per-frame GetService

**Priority:** Medium
**Certainty:** Confirmed
**Lens:** Debugging
**Files/Systems Involved:** `UxmlViewTrackSystem.cs`, `ReversibleEffectSystem.Enter`
**Problem:** When `uxml.Instantiate(key)` returns null (unknown/typo'd key) the base logs "unresolved target for Entity(…)" — no key, no hint it's a UXML registry miss; the entity id is useless to a designer. Also `Ready()` resolves `IUXMLService` from the service container every frame.
**Suggested Change:** Cache the service (invalidate if `AnchorApp.Current` changes). Give `TryApply` an optional failure-reason out (or log inside the override): `"UxmlView: key '{key}' not registered in AnchorSettings ▸ Views (clip on {entity})"`. Same for TextReveal: name the missing `TargetId`.
**Confidence:** High

### TODO: ViewModel scratch-list lifetime and per-frame rebind

**Priority:** Medium
**Certainty:** Strongly Likely
**Lens:** Architecture / Performance
**Files/Systems Involved:** `DataDisplayTrackSystem`, `EssenceUITrackSystem`, `RowsTrackSystem` (+ `EssenceUIView`, `RowsView`)
**Problem:** Systems assign their persistent `scratch` NativeLists into the ViewModel every frame (`data.Rows = scratch`). The VM (UI-side, potentially outliving the system across world reloads) then references system-owned memory — after `OnDestroy` disposes scratch, a view that reads `ViewModel.Value.Rows` (e.g. `RowsView.BindRow`) touches disposed memory. Additionally, if Anchor raises `PropertyChanged` on every assignment, the GridViews rebuild every frame (`itemsSource = …; Refresh()`), which is a full re-bind of all rows.
**Suggested Change:** (a) On `OnStopRunning`/`OnDestroy`, push an empty/owned copy into the VM before disposing (or have `Unload` clear rows — verify Anchor's `ILoadable` ordering vs system teardown). (b) Verify `SystemProperty` change detection: it should only notify when the list *content version* changed; if it notifies per assignment, gate the assignment on a dirty flag (only assign when scratch differs from last frame).
**How to Verify:** Reload the world with a view open — no native safety error; Profiler: GridView bind calls only on actual change.
**Confidence:** Medium

### TODO: EssenceUITrackSystem correctness nits — per-player event identity, stale-clear scope, Entity.Index as "Player"

**Priority:** Medium
**Certainty:** Confirmed
**Lens:** State
**Files/Systems Involved:** `EssenceUITrackSystem.cs`, `EssenceUIComponents.cs`
**Problem:** (a) `ActiveUIEvent` lives on the clip entity with no player field; if a clip's resolved source changes mid-clip (link retarget), surviving toasts are re-labeled to the new `playerIndex`. (b) The stale-clear query is `WithDisabled<ClipActive>` only — a clip whose *timeline* deactivates without disabling ClipActive (director destroyed same frame) is caught by cleanup elsewhere, but verify; safer: `.WithAny` on either disabled flag. (c) Rows expose `Player = player.Index` (entity index) — unstable across respawn and misleading in UI ("P7" after pooling).
**Suggested Change:** Store `Entity SourceAtCapture` in `ActiveUIEvent` and drop rows whose source changed; clear when either active flag is disabled; expose the resolved `PlayerId.Value` (read the component) instead of entity index, falling back to index only for non-player sources.
**Confidence:** Medium

## Low Priority TODOs

- **Logging consistency** (`Confirmed`, Debugging): `DataUISettings.Bake`, `DataUIDriverSystem`, samples use `UnityEngine.Debug.Log*`; `ReversibleEffectSystem`/`UxmlViewTrackSystem` use `BLGlobalLogger`. Workspace standard (shattered-debug-logging) is BovineLabs logging everywhere outside editors — migrate the runtime call sites; bakers may keep `Debug.LogError` for context-object ping.
- **Namespace/style drift** (`Confirmed`): half the files use file-scoped `using` inside namespace + `this.` prefix (BL style), half don't (`DataDisplayClip`, `EssenceUIClip`, ViewModels, systems). Align to the BovineLabs convention; enables the package to pass BL lint.
- **`ReversibleEffectSystem.Enter`**: `warned.IntersectWith(entities)` boxes the `NativeArray` enumerator every frame while `warned` is non-empty — swap to a manual loop over a temp HashSet only when `warned.Count > 0` (it already guards; just noting the boxing).
- **`ControllableRegistrySystem` Version churn** — covered above; also `OnUpdate` clears 256 entries per frame; fine, but a `changed` early-out is one line.
- **`EssenceUIView` hardcoded colors** (`TrackColor`, `FillColor`) — move to USS classes (`vex-essence__fill`) so themes/App UI tokens can restyle; per project standard, visuals live in USS (`Hud.uss` precedent).
- **`NodeChip.clicked` event** — no way to remove the `Clickable` or dispose; fine for now, add `RemoveManipulator` on detach if pooling views.
- **Localization** (`Risk`): `UIBindingEntry.Label`/`DataUISettings.Entry.Label` are raw strings shown to players. Project standard is `@TABLE:key` tokens via `LocalizedTextElement`. The driver writes `Label.text` directly — decide whether HUD labels are localizable; if yes, route through tokens (name-{i} becomes a LocalizedTextElement and the driver passes the token through).
- **Docs**: no `Documentation~` for the package. The Hud.uxml comment block is the only contract doc for `card-{i}`. Write a one-page "HUD contract" (element names, classes, USS parts of HudBar, which knobs are live) once Critical #1 lands.
- **`HudBar` first-layout flicker** (`Confirmed`, cosmetic): before the first `GeometryChangedEvent`, `m_TrackWidth == 0` → inner widths unset → textures collapse for a frame. Guard: skip rendering fills until width > 0 by keeping clips `display:none` when `w <= 0`.

## Designer Safety TODOs

(Consolidated; items above cross-referenced.)

1. **Dead knobs must die or work** — Critical #1. This is the single largest designer-safety item.
2. **Explicit slot names** over list-index binding — High. Reordering must never silently rewire cards.
3. **Custom inspector for `DataUISettings`** (ElementEditor per bl-core-inspectors): per-row summary line ("P0 · CurrentHealth / Max Health · Bar · card-0"), a **Validate** button running the full rule set (below), red inline errors for Binding-mode rows and missing Max, and a read-only preview of the resolved slot name. Hide `Value` when `Bar` is set (it's ignored — today both fields show, only a tooltip explains).
4. **`EssenceBarSource` inspector**: show which ghost fields are relevant for the chosen `ghostMode` (hide `ghostIntrinsic` unless FromIntrinsic, etc.); warn inline when `max == null` ("bar renders empty").
5. **`BarFeedbackProfile`**: group live vs reserved fields; until Critical #1 lands, mark unwired fields `[InspectorReadOnly]` with a "not yet implemented" help box — do not let designers tune dead values.
6. **`UxmlViewClip`/`UssClassClip`/`UITextRevealClip` inspectors**: TargetId/UxmlKey as dropdowns sourced from the registered Anchor views / a scan of registered UXML element names where feasible; at minimum a "Check now" button that instantiates the UXML in-editor and verifies the TargetId exists.
7. **Duplicate PlayerId warning** in the registry (Medium above) plus a bake-time cross-check: two `DataUISettings` rows with identical `(Mode=Player, Player=N)` and the same slot target.
8. **HealthSchemaObject Id==0** bake error (Medium above) — the AutoRef delay is a known trap; make it loud.

## Validation & Guard TODOs

**Editor-time** (before play mode):
- `DataUISettings` Validate: Binding-mode rows (already a bake error — good; surface it in the inspector too), missing `Bar.max`, Event-kind Value rows (already warned), invalid `Format` (already checked — keep), rows > 255, duplicate resolved slots, panel keys present in Anchor views, `card-{slot}` present in each mounted panel UXML asset.
- Clip inspectors: empty `UxmlKey`/`ClassName`/`TargetId` warnings at author time (empty `ClassName` currently just silently no-ops at runtime via the `TryApply` false path — it then *warns as unresolved target every entity once*, a misleading message).
- FixedString budget warnings already exist at bake (`Fit64`/`Fit512`) — good; mirror them in inspectors so the designer sees truncation before baking.

**Bake-time:**
- Id==0 / null-entry / slot-overflow errors (Medium above).
- `EssenceUIClip.Source.link == null` with `Route != Self/None` → warn (route through links can't resolve without a schema — verify `BakeRef` semantics and encode the answer as a check).

**Runtime guards:**
- `ControllableRegistrySystem`: assert `PlayerId.Value < byPlayer.Length` (currently safe only because `PlayerId.Value` is `byte` and capacity is 256 — one refactor of PlayerId to `ushort` away from an OOB write; make the invariant explicit with a `Hint.Assume`/debug assert or `math.min`).
- `DataUIDriverSystem`: the finite-value sanitization (`math.isfinite`) exists — good; extend to `lockedFrac` inputs and chip amounts (`Amount` could be `int.MinValue`; `math.abs(int.MinValue)` overflows — cast to long or clamp).
- `ReversibleEffectSystem.OnDestroy` reverts outstanding — good; also clear the new USS ref-count registry there.

## Timing / Physics / Animation TODOs

- **Scaled vs unscaled clock policy** — Medium above. Encode once, document on every duration field.
- **Text reveal timing is timeline-authoritative** (`LocalTime`/`TimeTransform`) — correct and low-FPS safe (position-based, not accumulative). Keep it; add the tag/grapheme fix (High).
- **Ghost/chip math is frame-rate independent** where it exists (`GhostStep` uses `1-exp(-speed*dt)`; sample conductor clamps dt) — when wiring Critical #1, keep the exponential form; never lerp by `speed*dt` directly.
- **Low-FPS chip merging**: multiple `BarFeedbackEvent`s in one frame already merge into one chip window via `m_ChipHi` accumulate — good. Add a test pinning it (below).
- **`EssenceUIDecay` at dt spikes**: a 2 s toast with a 3 s hitch expires next frame — acceptable; if not, clamp dt like the sample (`math.min(dt, 0.1f)`) at the decay site.
- **Pause**: presentation groups keep running under bl-core pause; values freeze but HudBar wall-clock animations continue — covered by the clock-policy TODO.
- **Order dependency inside the frame**: driver sets `bar.value` *before* `AddChip` (comment says chip top = live fill + amount) — fragile implicit contract; after the batching refactor make `SetState` take chips in the same call so ordering is internal.

## Architecture TODOs

### TODO: Make HudBarMath the single behavior kernel; driver owns state, HudBar owns pixels

**Priority:** High
**Certainty:** Confirmed (current duplication)
**Lens:** Architecture
**Files/Systems Involved:** `DataUIDriverSystem`, `HudBar`, `HudBarMath`, sample `CombatConductorSystem`
**Problem:** Bar behavior currently lives in three places: `HudBarMath` (unused), `HudBar` (chip hold/drain state machine on the element), and the *sample producer* (ghost lerp in `CombatConductorSystem` — behavior that belongs to presentation was pushed into game code because the driver doesn't do it). A consumer project must re-implement the conductor's ghost logic to get a ghost at all.
**Suggested Change:** Ownership boundaries: **simulation** emits facts (`Intrinsic/Stat` values + `BarFeedbackEvent`); **driver** owns all per-slot presentation *state* (ghost, flash, idle, visibility) computed via `HudBarMath` with baked config; **HudBar** is a stateless-ish renderer (fill/ghost/locked/flash floats + chip animation only). `BarGhost` component becomes an optional *override* for games that want sim-authoritative ghosts, not the primary path. Delete the ghost logic from the sample conductor once the driver does it.
**Migration:** implement Critical #1 → flip sample to rely on driver ghost → deprecate `BarGhost` primary path in docs.
**Confidence:** High

- **`ReversibleEffectSystem` per-element coordination layer** (ref-counts / base-text registry — Critical #4) is the missing abstraction: effects compose on shared elements; today each effect assumes exclusivity.
- **Driver panel cache** (High GC TODO) doubles as the seam for a future "HUD contract" object — a small class that resolves and validates a panel's slots once, warns once, and is the only thing that touches UQuery.
- **Consider splitting `DataUIDriverSystem`** resolve/read (Burst-able, produces a `NativeArray<SlotRender>`) from push (managed) — makes the read half testable without UI and removes the last excuse for the broad `Complete()`.

## Debugging / Tooling TODOs

- **HUD debug overlay** (BL_DEBUG): a panel listing each slot → resolved entity, raw current/max, ghost, chip queue length, show-reason ("AlwaysVisible" / "NotFull" / "GhostDelta" / hidden-because-…). The #1 diagnostic for "my bar is empty/hidden" — today that requires a debugger. Cheap: build from the same per-slot state introduced in Critical #1.
- **Feedback event tracer**: BL_DEBUG ring buffer of the last N `BarFeedbackEvent`s per entity with frame stamps; dump via a debug window or log command. Diagnoses "the chip never showed" (was it emitted? eaten? filtered by MinChipFrac?).
- **Warn-once registries need reset hooks**: `warnedMissingMask`/`warnedBadFormatMask` and `ReversibleEffectSystem.warned` never reset on domain-reload-less replays or when the panel remounts — add clearing when the panel cache rebuilds so a fixed UXML re-warns correctly if broken again.
- **Editor menu "Vex/UI/Validate HUD Setup"**: runs the full editor-time rule set (settings rows × panel UXMLs × Anchor views) and prints a single report. Wire the same routine into a build preprocessor (`IPreprocessBuildWithReport`) so broken HUD config fails the build, not the demo.
- **`GameViewCapture`/showcase builders** are good agent/QA affordances — keep; add a headless "drive one damage event and assert chip visible" smoke to them.

## Testing TODOs

Each test pins a specific risk from above:

1. **Overlapping USS class clips** (play-mode, Critical #4): staggered enter/exit on same element+class → class present until last exit. Proves the ref-count.
2. **Overlapping text reveals** (play-mode): B starts mid-A; after both end, text == true original. Proves base-text registry.
3. **Revert-on-destroy** (play-mode): destroy a clip entity while active → view removed / class removed / text restored via cleanup path. Pins the `ICleanupComponentData` contract.
4. **Detach mid-collapse** (UI test): HudBar removed and re-added during chip drain → next AddChip animates. Pins the m_Collapsing latch fix.
5. **Feedback drain bounded** (ECS test): producer emits 1000 events with no HUD mounted → buffer ≤ cap; two readers both observe events. Pins Critical #2.
6. **DataDisplay jobs safety** (ECS test): job writes `IdValue`, system updates same frame → no safety exception. Pins Critical #3.
7. **Chip merge under low FPS** (unit): 3 DamageChips one frame → single chip window equal to sum-capped top. Pins accumulate semantics.
8. **GhostStep frame-rate independence** (unit, exists implicitly — make explicit): 1×1.0 s step vs 60×1/60 steps land within epsilon.
9. **Registry duplicate PlayerId** (ECS test): warning fired, deterministic winner. Pins Medium registry TODO.
10. **Reveal markup/grapheme** (unit): no partial tags; ZWJ cluster atomic. Pins High reveal TODO.
11. **Slot addressing** (bake test): duplicate resolved slots → bake error; >255 rows → error not wrap.
12. **Driver zero-alloc** (performance test, `Unity.PerformanceTesting`): steady-state frame allocates 0 B. Pins the GC TODO against regression.

## Suggested Architecture Direction

**Current weakness.** Presentation *behavior* has no single owner: the tested kernel (`HudBarMath`) is orphaned, the widget (`HudBar`) grew its own chip state machine, the sample producer (`CombatConductorSystem`) carries ghost smoothing that consumers must re-invent, and the driver hardcodes a third visibility rule. Meanwhile the data model bakes config that nothing reads. Effects that share elements assume exclusivity.

**Desired boundaries & ownership.**
- **Simulation (game code)** owns facts: `Intrinsic/Stat/ConditionEvent` values and `BarFeedbackEvent` emissions. It never smooths, never times UI.
- **Drain system (new, always-on, LateSimulation)** owns event lifetime: frame-stamps, TTL clear, cap. Producers and all readers agree events live exactly one frame.
- **`DataUIDriverSystem`** owns presentation *state*: per-slot `SlotState` (ghost, flash, idle, lastFill) advanced exclusively through `HudBarMath` with baked `UIBindingEntry` config; per-panel `SlotElements` cache owns all UQuery. Split conceptually into resolve/read (Burst-friendly, emits `SlotRender[]`) and push (managed, change-only writes).
- **`HudBar`** owns pixels only: floats in (`SetState(fill, ghost, locked, flash, chips)`), USS classes out; its chip animation is the one piece of element-local state, made detach-safe.
- **`ReversibleEffectSystem` family** gains a per-element coordination registry (class ref-counts, base-text) so overlapping clips compose instead of corrupting.

**Data flow:** authoring assets → bake (validated loudly) → `UIBindingEntry` → driver state → widget. One direction, no back-channel.
**Event flow:** producer → frame-stamped buffer → drain enforces lifetime → N readers, none destructive.
**Validation flow:** inspector Validate button = bake checks = build preprocessor — one shared rule routine, three entry points.
**Debugging flow:** the same `SlotState` powering rendering feeds the BL_DEBUG overlay (slot → entity → raw values → show-reason) and the feedback tracer, so diagnosis never requires a debugger.

**Migration steps:** (1) drain system + non-destructive reads; (2) `SlotState`/`SlotElements` caches in the driver (zero-alloc); (3) wire `HudBarMath` + flash element, delete or bake remaining profile fields; (4) ref-counts/base-text in reversible systems; (5) move sample ghost logic out of the conductor; (6) validation routine + inspector + build hook; (7) tests 1–12.
**Risks:** step 3 changes visual behavior of existing content (visibility now honors ShowOnHealthChange/AutoHideDelay) — flag in changelog and default new fields to reproduce old behavior where possible. Steps 1–2 are behavior-preserving and safe to land first.
**Verify the design:** HUD Showcase must look identical after steps 1–2 (screenshot diff via `GameViewCapture`), knobs must all do something after step 3, and the zero-alloc perf test locks the hot path.

## Implementation Snippets

**Per-slot state + kernel wiring (Critical #1):**
```csharp
private struct SlotState { public float Ghost, HoldTimer, Flash, Idle, LastFill; public bool Init; }
private NativeArray<SlotState> slots; // sized to entries.Length, persistent

var s = slots[i];
if (!s.Init) { s.Ghost = fill; s.LastFill = fill; s.Init = true; }
bool damaged = fill < s.LastFill - 1e-4f;
s.Idle = (damaged || fill > s.LastFill + 1e-4f) ? 0f : s.Idle + dt;

float extGhost = e.GhostMode switch {
    HudGhostMode.FromStat      => ReadValue(UIValueKind.Stat, e.GhostKey, ...) / max,
    HudGhostMode.FromIntrinsic => ReadValue(UIValueKind.Intrinsic, e.GhostKey, ...) / max,
    _ => 0f };
float ghost = HudBarMath.GhostStep(e.GhostMode, fill, extGhost, damaged, dt,
    e.GhostDelay, e.GhostSpeed, ref s.Ghost, ref s.HoldTimer);
s.Flash = HudBarMath.Flash(e.FlashOnDamage != 0, damaged, s.Flash, dt, e.FlashDecay);
float alpha = HudBarMath.TargetAlpha(e.AlwaysVisible != 0, 0,
    e.KeepVisibleWhileNotFull != 0, e.ShowOnHealthChange != 0, fill, s.Idle, e.AutoHideDelay);
s.LastFill = fill; slots[i] = s;
```

**Panel/slot element cache (High GC):**
```csharp
private sealed class SlotElements {
    public VisualElement Card; public HudBar Bar; public Label Name, Value;
    public string LastValueText; public float LastFill = -1, LastGhost = -1, LastLocked = -1, LastFlash = -1;
    public bool LastShow; public bool ConfigApplied;
}
// Rebuild when panel set changes; names computed once:
cache[p][slot] = new SlotElements {
    Card = panel.Q("card-" + slot), Bar = panel.Q<HudBar>("bar-" + slot),
    Name = panel.Q<Label>("name-" + slot), Value = panel.Q<Label>("value-" + slot) };
// Push: change-only
if (se.LastShow != show) { se.Card?.EnableInClassList("is-hidden", !show); se.LastShow = show; }
if (se.Value != null && se.LastValueText != valueText) { se.Value.text = valueText; se.LastValueText = valueText; }
```

**USS class ref-count (Critical #4):**
```csharp
private readonly Dictionary<(VisualElement, string), int> refs = new();
// TryApply:
var key = (target, className);
refs.TryGetValue(key, out var n);
if (n == 0 && !target.ClassListContains(className)) target.AddToClassList(className);
refs[key] = n + 1;
inverse = new AppliedClass(target, className, wasPresent: n > 0 || preExisting);
// Revert:
if (refs.TryGetValue(key, out n) && --n <= 0) { refs.Remove(key); if (!preExisting) target.RemoveFromClassList(className); }
else refs[key] = n;
```

**Shared validation routine (one body, three entry points):**
```csharp
public static IReadOnlyList<string> ValidateDataUI(DataUISettings s) {
    var errs = new List<string>();
    if (s.Rows.Count > byte.MaxValue) errs.Add($"{s.Rows.Count} rows > 255 (Slot is a byte).");
    var seen = new HashSet<string>();
    for (var i = 0; i < s.Rows.Count; i++) {
        var r = s.Rows[i]; var slot = ResolveSlotName(r, i);
        if (!seen.Add(slot)) errs.Add($"Row {i}: duplicate slot '{slot}'.");
        if (r.Source.Mode == UISourceMode.Binding) errs.Add($"Row {i} ('{r.Label}'): Binding mode never resolves for HUD rows.");
        if (r.Bar != null && r.Bar.max == null) errs.Add($"Row {i}: Bar source '{r.Bar.name}' has no Max stat.");
        // + format check, panel-key check, card-{slot} presence per panel UXML…
    }
    return errs;
}
// Inspector button, Bake(), and IPreprocessBuildWithReport all call this.
```

## Final Ranked TODO List

1. **[Critical]** Wire or delete dead bar config; adopt `HudBarMath` as the kernel (ghost/flash/visibility/drainRate/collapse trigger).
2. **[Critical]** Non-destructive, bounded `BarFeedbackEvent` consumption (drain system, frame stamps, cap, multi-reader).
3. **[Critical]** `DataDisplayTrackSystem`: `CompleteDependencyBeforeRO<IdValue>()`.
4. **[Critical]** Overlap safety in reversible effects: USS class ref-count + text-reveal base-text registry.
5. **[High]** Driver zero-alloc: panel/slot element cache, precomputed names, change-only writes.
6. **[High]** Explicit slot names + duplicate/overflow bake errors (kill index-reorder trap).
7. **[High]** Text reveal: rich-text-safe + grapheme-cluster reveal; per-frame Substring early-out.
8. **[High]** HudBar: detach-safe collapse latch, `SetState` batching, config-apply-once.
9. **[High]** Views resubscribe on attach; verify GridView refresh cadence.
10. **[High]** Remove `ServerSimulation` from presentation systems.
11. **[High]** Narrow sync points to targeted `CompleteDependencyBefore*`.
12. **[High]** Handle or loudly reject unhandled `FeedbackKind`s.
13. **[Medium]** Clock policy: unscaled UI decay, pause behavior for HudBar animations.
14. **[Medium]** Registry hardening: duplicate PlayerId warn, buffer-based storage, meaningful Version.
15. **[Medium]** Bake validation: Id==0, null entries, link/route mismatch; UXML-key editor check.
16. **[Medium]** Centralize defaults/epsilons (`BarFeedbackDefaults`).
17. **[Medium]** ViewModel scratch lifetime on teardown; failure messages name the actual cause (UxmlKey, TargetId).
18. **[Medium]** `ActiveUIEvent` source identity; stale-clear scope; PlayerId (not Entity.Index) in rows.
19. **[Medium]** Debug overlay (slot → entity → values → show-reason) + feedback tracer + "Validate HUD Setup" menu + build preprocessor.
20. **[Low]** Logging migration to BLGlobalLogger; namespace/style alignment; USS-driven view colors; localization decision for labels; first-layout flicker guard; docs page for the HUD contract.
21. **[Tests]** Land the 12 targeted tests alongside their fixes (each pins one item above).
