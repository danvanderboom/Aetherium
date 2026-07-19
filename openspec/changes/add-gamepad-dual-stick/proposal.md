## Why
The merged `add-xbox-controller-unity` change gives the Unity client a **single-stick, absolute-direction**
gamepad scheme with the **right stick unused**. This change moves to a proper **dual-thumbstick** layout that
matches how characters actually move (heading-relative), adds climb/descend and a get/use button, and
introduces a **piloting control context** so the same sticks fly a vehicle from the cockpit. It directly
implements the requested mapping: left stick move/strafe, right stick turn + up/down, X to pick up or use,
plus supporting buttons.

Two existing facts make this cheap: the server `move` tool already accepts heading-relative directions
(`F`/`B`/`L`/`R`) as well as absolute (`N`/`E`/`S`/`W`), and a `pickup` tool already exists separate from
`use`. So "forward/back/strafe" and "get-or-use" need no server change — only client bindings.

## What Changes
- Rebind **left stick** from absolute N/E/S/W to **heading-relative** move (forward/back) + strafe (left/right),
  sending relative directions to the existing `move` tool.
- Use the **right stick**: X axis = turn (`rotate`), Y axis = level up/down (`changelevel`).
- Add **X** as a context **get-or-use** action (`pickup` if a carriable item is present, else `use`).
- Add **A** (interact/confirm), **B** (cancel/back), **Y** (secondary) actions.
- Add a **Piloting control context** (Avatar vs Piloting): in a pilot seat the sticks drive the controlled
  vehicle in 3D (thrust/strafe/yaw/climb-descend) with an altitude gauge; leaving restores avatar control.
- Reuse unchanged: async tool execution (`ExecuteToolAsync` → `ToolExecutionResultDto`) and the multi-option
  selection flow and its navigate/confirm/cancel controls.
- Keep full keyboard parity; bindings remain remappable.

## Impact
- Affected specs: `client` (adds dual-stick controls and piloting context; refines the pending
  `add-xbox-controller-unity` gamepad scheme).
- Affected code:
  - `Aetherium.Unity/Assets/InputActions.inputactions` (right-stick bindings; relative move; X/A/B/Y actions)
  - `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs` (relative mapping, right-stick rotate/level,
    X context action, Avatar/Piloting context)
  - `docs/unity/README.md` (controls reference)
- Depends on: `add-held-key-repeat-movement` (held-stick repeat) and `add-flying-entities` (Manual flight
  plans + pilot-seat) for the piloting phase; on-foot dual-stick is independent.
- No server changes required; backward compatible with keyboard input.
