# HUD contract — `DataUISettings` → panel UXML

The generic HUD driver (`DataUIDriverSystem`) binds each baked `UIBindingEntry` row to named elements it finds
inside every mounted panel classed **`vex-hud`**. This page is the authoring contract: the element names, the
`HudBar` USS parts, and which baked knobs are actually read at runtime.

## Panel + element names (per row)

A row's **slot key** is its `SlotName` when set, otherwise its list index (`Slot`, a byte). The driver looks up,
in each `.vex-hud` panel:

| Element name        | Type     | Purpose                                             |
| ------------------- | -------- | --------------------------------------------------- |
| `card-{slot}`       | any      | Row container; gets `is-hidden` when the row hides. |
| `bar-{slot}`        | `HudBar` | The fill/ghost/locked/flash/chip bar.               |
| `name-{slot}`       | `Label`  | Static label (set once from `Label`).               |
| `value-{slot}`      | `Label`  | Formatted current/max readout.                      |

Any of these may be absent — the driver skips what it cannot find and warns once per slot if a row resolves to
**no** `card`/`bar` in any mounted panel. Prefer explicit `SlotName` over list-index binding: reordering rows
reassigns index-based slots silently, whereas named slots stay put. Duplicate resolved slots and `>255` rows are
bake errors.

## `HudBar` USS parts

`HudBar` is a dumb renderer — all look is USS; it only maps told numbers to geometry. Style these classes:

- `vex-bar` — root. `vex-bar__track` — clip frame. `vex-bar__frame` — border overlay.
- `vex-bar__fill` / `__fill-inner` — the fill (revealed, never squashed).
- `vex-bar__ghost` / `__ghost-inner` — the delayed-damage / heal-lead band (windowed crop of the blade).
- `vex-bar__chip` / `__chip-inner` — the accumulating damage chip trail (held, then eased collapse).
- `vex-bar__locked` / `__locked-inner` — hatched locked band at the high end (curse).
- `vex-bar__flash` — full-bleed pulse overlay; the bar drives only its opacity, capped by `--vex-flash-max`.
- Modifier classes: `vex-bar--low`, `vex-bar--healing`, `vex-bar--rtl`.
- Custom properties read from USS: `--vex-low-tint` (low-health tint color), `--vex-flash-max` (flash opacity cap).

## Which knobs are live

All `UIBindingEntry` knobs below are read every frame; the ghost/flash/visibility ones flow through the single
`HudBarMath` kernel (`AdvanceSlot`).

- **Value/label**: `ValueKind`/`ValueKey`, `MaxKind`/`MaxKey`, `LockedKey`, `Label`, `Format` (`{0}`=current, `{1}`=max).
- **Ghost**: `GhostMode` (`Off`/`FromStat`/`FromIntrinsic`/`ComputedLerp`), `GhostKey`, `GhostDelay`, `GhostSpeed`.
  A `BarGhost` component on the resolved entity is an optional sim-authoritative override that wins when present.
- **Flash**: `FlashOnDamage`, `FlashDecay` (plus a `FeedbackKind.Flash` event pins flash to 1 for a frame).
- **Visibility**: `AlwaysVisible`, `KeepVisibleWhileNotFull`, `ShowOnHealthChange`, `AutoHideDelay`.
- **Chip trail** (`HudBar.SetTrailConfig`, applied once): `TrailMode`, `Accumulate`, `HoldMs`, `DrainMs`,
  `MinDrainMs`, `DrainEase`, `Fade`, `MinChipFrac`, `DrainRate` (`DrainMs == 0` → duration = `band / DrainRate`).

Default values and the near-full / near-equal epsilons live in one place — `BarFeedbackDefaults` — so adding an
empty `BarFeedbackProfile` never changes behaviour.

## Feedback events (non-destructive)

Producers (game code, e.g. the showcase `CombatConductorSystem`) append `BarFeedbackEvent`s. `BarFeedbackDrainSystem`
(LateSimulation, always-on) stamps each event with its frame, removes it one frame later, and caps the buffer at
`BarFeedbackDefaults.EventCap`. The driver reads without clearing, so multiple consumers (HUD + world-space bar)
each see every event exactly once. Handled kinds: `DamageChip` (chip), `HealSurge` (green ghost lead), `Flash`
(flash pulse). Other kinds are reserved and warn once.

## Clock policy (unscaled, pause-aware)

HUD **feedback** timing — toast decay, ghost/flash catch-up, idle/auto-hide, chip hold/drain — runs on **unscaled**
presentation time, published once per frame as the `UIUnscaledTime` singleton by `UIUnscaledClockSystem`. So
`WorldTimeScale` bullet-time neither stretches a 2 s toast to 20 s nor freezes it at timescale 0, and bl-core
`PauseGame` freezes all HUD feedback (the driver forwards pause to `HudBar.SetPaused`, and the published delta is 0
while paused). Gameplay-driven fill values still track scaled sim time — only feedback presentation reads this clock.
Duration fields (`DisplayDuration`, `AutoHideDelay`) are therefore documented as unscaled wall-clock seconds.

## Seats (players) and vacancy

Rows in `Player` source mode resolve through the `ControllableRegistry`, a 256-slot table indexed by `PlayerId`
(the player's seat) rebuilt each frame by `ControllableRegistrySystem`. Two `Controllable` entities claiming one
seat resolve to the lowest entity index and warn once. When a seat is **vacant** (no live `Controllable` — before
join, after leave/death), the row does not resolve: its bar hides and its per-slot presentation state resets, so a
re-joined seat snaps in clean with no phantom ghost/flash carried over.
