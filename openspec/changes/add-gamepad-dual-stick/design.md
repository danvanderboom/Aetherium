## Context
The Unity client already has basic Xbox support from the merged `add-xbox-controller-unity`: left stick =
absolute N/E/S/W move, LB/RB = rotate, LT/RT = change level, A = use/confirm, B = cancel, D-Pad = option
navigation; the right stick and X/Y are unused. This change makes the layout dual-stick and heading-relative
and adds a piloting context. Full design: `docs/design/gamepad-dual-stick.md`.

## Goals / Non-Goals
- Goals: dual-stick relative movement; right-stick turn + climb/descend; X = get-or-use; a piloting context
  that drives a vehicle with the same sticks; keyboard parity; remappable.
- Non-Goals: server movement changes (none needed); flight-sim aerodynamics; new option-selection UI (reused).

## Decisions
- **Left stick is heading-relative.** Send `F`/`B`/`L`/`R` to the existing `move` tool, which already maps
  these to `RelativeDirection.Forward/Backward/Left/Right`. No server change; matches the console client and
  the `move` tool schema (`Aetherium.Server/Agents/Tools/Movement/MoveTool.cs`).
- **Right stick carries orientation + altitude.** X → `rotate` (analog-proportional), Y → `changelevel`.
  This frees LB/RB/LT/RT and makes "up/down" muscle memory transfer to piloting climb/descend.
- **X is a context action over two existing tools.** Perception lists visible items/affordances per tile, so
  X runs `pickup` when a carriable is present, else `use` (which already handles the multi-option flow).
- **Piloting is a client control context, resolved server-side by "what this session controls."** In
  `Piloting`, movement tools target the vehicle entity rather than the avatar; entering/leaving is a pilot-seat
  interaction. Depends on `add-flying-entities` Manual flight plans.
- **Held-stick repeat reuses `add-held-key-repeat-movement`** — a held stick is held input paced to the
  character's cadence.

## Risks / Trade-offs
- Reassigning familiar bindings → mitigate with remapping + updated on-screen hints and README.
- Analog turn vs discrete rotate → cap turn rate at the server cadence; treat magnitude as intent.
- Context confusion (avatar vs vehicle) → clear HUD indicator + altitude gauge while piloting.

## Migration Plan
Additive and backward compatible: keyboard bindings unchanged; the merged Xbox change's async execution and
option-selection controls are reused. Ship on-foot dual-stick (Phase 1) first; piloting (Phase 3) lands with
`add-flying-entities`.

## Open Questions
- Default swap-stick preference? (move-on-left default, opt-in swap.)
- Should LB/RB retain a quick-90°-turn / Z-focus role, or become target-cycling? (Deferred to config.)
