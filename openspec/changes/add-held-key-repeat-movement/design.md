## Context
Holding a movement key or stick should keep the character moving repeatedly, at the rate that character is configured to move — not once per press. The authoritative design is `docs/design/movement-cadence-and-held-input.md`; this document captures the load-bearing decision.

Current state (grounding):
- **Holding a key moves you exactly once.** In the Unity client, `PlayerController.OnMove` fires only on `context.performed` and has no repeat/timer logic (`Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs:93-110`); `OnRotate`/`OnChangeLevel` are likewise single-shot on `performed` (`:112-148`). One actuation produces exactly one tool call.
- **There is no per-entity action-rate / cadence model server-side.** Moves are applied as fast as they arrive; nothing rate-limits a character.
- The console client issues one move per key event as well.

## Goals / Non-Goals
- Goals: a server-authoritative per-character action rate; "hold to keep moving" for move, rotate, and change-level on both the Unity and console clients; one shared cadence clock for player-issued actions and autonomous flyers.
- Non-Goals: client-side prediction/reconciliation is optional (Phase 4) and not required for the MVP; gamepad binding details are owned by the gamepad change, not this one.

## Decisions
The feature has **two halves that must agree**:

1. **Server-authoritative action cadence.** Each character has a maximum action rate ("turn-taking speed"). When a `move`/`rotate`/`changelevel` arrives, the server accepts it only if at least `Interval = 1 / MovesPerSecond` has elapsed since the last accepted action; otherwise it **coalesces/defers** to the next eligible tick (avoiding input loss on jitter) rather than applying the action twice. Cadence is **data**: a per-world default with a per-entity override (a scout moves faster than a golem), and the current cadence is **surfaced to the client** in the perception/HUD payload.
2. **Client held-input repeat.** While a movement input is held, the client re-issues the action every `Interval` — for move, rotate, and change-level — paced to the server cadence read from the latest perception (with a sane fallback until the first frame arrives).

**Why both halves.** Client repeat alone would let a fast client outrun a slow character or desync with other players; server cadence alone would not give the "hold to keep going" feel. Together, the client streams held input at the character's rate and the server enforces that rate as truth.

**One clock.** The `FlightPlan` follower advances one leg-step per `Interval`, so autonomous flyers and held-key players share a single pacing model.

**Alternative considered.** On the Unity side, an Input System `Hold`/repeat interaction was considered; a manual timer keyed on the server-surfaced cadence is preferred so client and server stay aligned.

## Risks / Trade-offs
- **Input flooding** → coalesce/queue on the server and cap the client send-rate at the cadence.
- **Console key-up detection** is unreliable (terminals report key events, not held-state) → use a short key-repeat grace window and stop on any other key or on timeout.
- **Multiplayer fairness** → cadence is enforced server-side so a modified client cannot move faster.
- **Feel vs. authority** on high latency → the optional Phase 4 prediction/reconciliation mitigates.

## References
- `docs/design/movement-cadence-and-held-input.md` (authoritative design)
- `Aetherium.Server/Agents/Tools/Movement/MoveTool.cs`, `GameSession.MoveView`
- `Aetherium.Server/Simulation/WorldClock.cs` (game-time stamps)
- `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs:93-148` (single-shot input today)
