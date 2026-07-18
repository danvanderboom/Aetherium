# Design: Xbox Dual-Stick Controls & Piloting

**Status:** Draft design · **OpenSpec change:** `add-gamepad-dual-stick` · **Refines:** merged `add-xbox-controller-unity`

## Summary

The Unity client already has *basic* Xbox support (merged `add-xbox-controller-unity`), but it's a
**single-stick, absolute-direction** scheme and the **right stick is unused**. This change moves to a proper
**dual-thumbstick** layout matching how the character actually moves (heading-relative), adds **climb/descend**
and a **get/use** button, and introduces a **piloting control context** so the same sticks fly a vehicle when
you're in the cockpit.

Requested mapping (from the feature brief):
- **Left stick** — move forward/backward, strafe left/right
- **Right stick** — turn left/right, go up/down
- **X** — pick up or use an item
- plus other buttons as needed

## Current state (grounding)

From `PlayerController.cs` + the merged Xbox change + `docs/unity/README.md`:

| Control | Today | Tool |
|---|---|---|
| Left stick | Move, **cardinalized to absolute N/E/S/W** (`Vector2ToDirection`, `PlayerController.cs:301-313`) | `move` (direction=north/…) |
| Right stick | **unused** | — |
| LB / RB | Rotate CCW / CW (axis) | `rotate` |
| LT / RT | Level down / up (axis) | `changelevel` |
| A | Use / confirm option | `use` |
| B | Cancel option | — |
| D-Pad ↑/↓ | Navigate options | — |
| X, Y | **unused** | — |

Two facts that make the redesign cheap:
- The server **`move` tool already accepts heading-relative directions** `F/B/L/R` as well as absolute
  `N/E/S/W` (`Aetherium.Server/Agents/Tools/Movement/MoveTool.cs:31-32,83-104`). So "forward/back/strafe"
  needs no server change — the client just sends `F/B/L/R` instead of `north/…`.
- A distinct **`pickup` tool** exists (`Aetherium.Server/Agents/Tools/Interaction/PickupTool.cs`), separate
  from `use` — so "X to pick up **or** use" is a context action over two existing tools.

## New control scheme

### On foot (avatar context)

| Physical control | Action | Tool / args |
|---|---|---|
| **Left stick Y+ / Y−** | Move **forward / backward** (relative to heading) | `move` `F` / `B` |
| **Left stick X− / X+** | **Strafe** left / right | `move` `L` / `R` |
| **Right stick X− / X+** | **Turn** left / right | `rotate` (ccw/cw), analog-proportional |
| **Right stick Y+ / Y−** | Level **up / down** (climb stairs / change Z) | `changelevel` up/down |
| **X (buttonWest)** | **Get or use**: `pickup` if a carriable item is on the tile, else `use` | `pickup` / `use` |
| **A (buttonSouth)** | Interact / confirm (open door, board, confirm option) | context / `open` / confirm |
| **B (buttonEast)** | Cancel / back (exits option-select) | cancel |
| **Y (buttonNorth)** | Secondary action (e.g. drop / inspect) | `drop` / inspect |
| **LB / RB** | Cycle Z-level focus / quick-turn 90°, or prev/next target | `changelevel` / `rotate` |
| **LT / RT** | Context (e.g. sprint modifier / ranged attack) | — |
| **D-Pad** | Option navigation (unchanged) + quick-slots | — |
| **Start / Select** | Menu / map | — |

Movement is **heading-relative** (left stick), matching the console client (W=forward, A=strafe) and the
`move` tool's `F/B/L/R`. Turn and climb move to the **right stick**, which is the natural home for "camera/
orientation + altitude" and frees the bumpers/triggers for other actions. Diagonal suppression on the left
stick is kept (dominant-axis pick), but now over the relative axes.

### Piloting a vehicle (piloting context)

When the player is piloting a flyer (a **Manual** [flight plan](flying-entities.md) — e.g. sitting in a
cockpit), the **same sticks drive the vehicle in 3D** instead of the avatar:

| Physical control | Action (vehicle) |
|---|---|
| Left stick Y | Thrust forward / reverse |
| Left stick X | Strafe / bank |
| Right stick X | Yaw (turn) |
| Right stick Y | **Climb / descend** (altitude band) |
| X | Toggle systems / interact with cockpit |
| B | Exit pilot seat (return to avatar context) |

This is why "right stick = up/down" is the right call: on foot it's Z-level, in the cockpit it's
climb/descend — the muscle memory transfers. While piloting, an **altitude gauge** — a ladder of `N`
discrete steps across the flyer's altitude envelope (see
[`adaptive-depth-visualization`](adaptive-depth-visualization.md) and [`flying-entities`](flying-entities.md))
— shows current altitude and where the ground/landing pad sits, so climb-out and descent-to-land are legible.
Control context is a small client state (`Avatar` vs `Piloting`) toggled when the player enters/leaves a pilot
seat; in `Piloting`, movement tools target the **vehicle entity** rather than the avatar (the server routes by
"what is this session currently controlling").

## Held-stick repeat

A held stick is "held input" — it uses the **same auto-repeat paced to the character's cadence** as held keys
(see [`movement-cadence-and-held-input`](movement-cadence-and-held-input.md)). Holding forward walks
continuously at the avatar's move rate; holding right-stick-left turns repeatedly at the turn cadence. Analog
magnitude may scale intent (full deflection = full cadence; slight = slow/step), but the ceiling is the
server-enforced cadence.

## Implementation notes

- **InputActions asset:** add `RightStick` bindings; rebind `Move` to emit relative `F/B/L/R` (either send the
  letters, or keep a `Vector2` and translate in `PlayerController`); add `Get`/`Use` on **X**, `Interact` on
  **A**, `Cancel` on **B**, `Secondary` on **Y**. Keep keyboard bindings intact (backward compatible).
- **`PlayerController`:** replace absolute `Vector2ToDirection` with a relative mapping; route `rotate` from
  right-stick-X (analog) and `changelevel` from right-stick-Y; implement the X context action (query
  perception for a carriable on the current tile → `pickup`, else `use`); add the `Avatar`/`Piloting` context
  switch.
- **Context action for X:** perception already lists visible items/affordances per tile
  (`perception` spec) — pick `pickup` when the current tile has a carriable, else fall through to `use`
  (which already handles the multi-option flow).
- **Discoverability:** update `docs/unity/README.md` and the on-screen HUD hints; provide a controls overlay.

## Accessibility & config
- **Remappable bindings** and optional **swap sticks** (some players prefer move-on-right).
- **Invert Y** for climb/descend and for turn.
- **Dead-zone / sensitivity** per stick; **hold-to-repeat rate** follows cadence but a comfort cap can lower it.
- Keep full keyboard parity so nothing is gamepad-only.

## Phasing
- **Phase 1 — Dual-stick on foot.** Right-stick turn + climb; left-stick relative move/strafe; X = get/use;
  A/B/Y actions. Update asset + `PlayerController` + docs.
- **Phase 2 — Held-stick repeat** integrated with cadence (depends on `add-held-key-repeat-movement`).
- **Phase 3 — Piloting context.** Avatar/Piloting switch; movement tools target the controlled vehicle
  (depends on `add-flying-entities` Manual plans and a pilot-seat interaction).
- **Phase 4 — Remapping/accessibility UI.**

## Reconciliation with `add-xbox-controller-unity`
That merged change established gamepad bindings, async tool execution (`ExecuteToolAsync` →
`ToolExecutionResultDto`), and the multi-option selection flow — all **reused unchanged**. This change
**modifies** the movement/rotation/level bindings (single→dual stick, absolute→relative), **adds** right-stick
+ X/Y actions and the piloting context. The option-selection controls (navigate/confirm/cancel) are kept.

## Key source references
- `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs` (input handlers, `Vector2ToDirection`)
- `Aetherium.Unity/Assets/InputActions.inputactions` (bindings), `docs/unity/README.md` (documented controls)
- `Aetherium.Server/Agents/Tools/Movement/MoveTool.cs` (relative `F/B/L/R` support), `RotateTool.cs`, `ChangeLevelTool.cs`
- `Aetherium.Server/Agents/Tools/Interaction/PickupTool.cs`, `UseTool.cs`
- `Aetherium.Model/SharedEnums.cs` (`RelativeDirection`); merged `openspec/changes/add-xbox-controller-unity`
