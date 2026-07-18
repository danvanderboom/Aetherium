## Why
Holding a movement key or stick today moves the character exactly once — in the Unity client `PlayerController.OnMove` fires only on `context.performed`, and there is no server-side cap on how fast a character may act. Players expect "hold to keep moving," and multiplayer fairness requires the server to be the authority on each character's action rate. See `docs/design/movement-cadence-and-held-input.md`.

## What Changes
- Add a server-authoritative `ActionCadence` (moves per second) per character, authored per-world with a per-entity override, that rate-limits/coalesces `move`/`rotate`/`changelevel` actions.
- Surface the current cadence (or interval) to the client in the perception/HUD payload so clients can pace their repeats instead of guessing.
- Pace autonomous flight-plan stepping with the same cadence clock (one concept for "how fast a bird flies its route" and "how fast the player walks when holding forward").
- Unity client: re-issue held movement input (move, rotate, change-level) every cadence interval instead of once per press; suppress repeat during option-selection mode.
- Console client: re-issue a held movement key at the character's cadence until released.

## Impact
- Affected specs: `engine-core` (ADDED: Action Cadence), `client` (ADDED: Held-Input Repeat (Unity)), `console-view` (ADDED: Held-Key Repeat (Console))
- Affected code:
  - `Aetherium.Server/Components/ActionCadence.cs` (new component)
  - `Aetherium.Server/Agents/Tools/Movement/MoveTool.cs`, the rotate/changelevel tools, and `GameSession.MoveView` (rate-limit / coalesce)
  - `Aetherium.Server/Simulation/WorldClock.cs` (game-time stamps) and the flight-plan follower
  - Perception / HUD payload (surface cadence to the client)
  - `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs` (held-input repeat)
  - Console client input loop (held-key repeat)
- Build impact: additive and backward compatible — a sane default cadence applies when a world or entity does not specify one.
