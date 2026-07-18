# Design: Movement Cadence & Held-Key Repeat

**Status:** Draft design · **OpenSpec change:** `add-held-key-repeat-movement`

## Summary

Holding a movement key/stick should make the character keep moving — repeatedly — **as fast as that
character is configured to move**, not once per keypress. This has two halves that must agree:

1. **Server-authoritative action cadence** — each character has a maximum action rate ("turn-taking speed").
   The server is the authority on how often a character may act.
2. **Client held-input repeat** — while an input is held, the client re-issues the action on an interval
   matched to that cadence.

The cadence is the *same clock* that advances autonomous [flight plans](flying-entities.md), so "how fast a
bird flies its route" and "how fast the player walks when holding ↑" are one concept.

## Current state (grounding)

- **Holding a key moves you exactly once.** In the Unity client, `PlayerController.OnMove` fires only on
  `context.performed` and has no repeat/timer logic (`Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs:93-110`).
  `OnRotate`/`OnChangeLevel` are likewise single-shot on `performed` (`:112-148`). One actuation → one tool call.
- The move goes `ExecuteTool("move", …)` → server `MoveTool` → `GameSession.MoveView(RelativeDirection,
  distance)` (`Aetherium.Server/Agents/Tools/Movement/MoveTool.cs:106`).
- **There is no per-entity action-rate / cadence model server-side.** Moves are applied as fast as they
  arrive; nothing rate-limits a character. (Real-time loop targets 60+ FPS per `project.md`, but there is no
  "this character may step every N ms.")
- The console client issues one move per key event as well.

So today: no auto-repeat, and no authoritative cap on move rate. Feature 2 adds both.

## Model

### 1. `ActionCadence` (server, per entity)

```csharp
// Aetherium.Server/Components/ActionCadence.cs (new)
public class ActionCadence : Component
{
    public double MovesPerSecond { get; set; } = 6.0;   // max step rate ("turn-taking speed")
    public double LastActionGameTime { get; set; }      // stamp of last accepted action
    // Interval = 1 / MovesPerSecond
}
```

- The **authority**: when a `move`/`rotate`/`changelevel` arrives, the server accepts it only if at least
  `Interval` has elapsed since `LastActionGameTime`; otherwise it **coalesces/queues** (applies at the next
  eligible tick) or **rejects** (client will retry on its own repeat). Coalescing avoids input loss on jitter.
- Cadence is **data**: default per world, overridable per character (a scout moves faster than a golem).
  This is the same number a `FlightPlan` follower uses to pace an autonomous entity.
- The current cadence (or interval) is **surfaced to the client** — added to the perception/HUD payload — so
  the client can pace its repeats correctly instead of guessing.

### 2. Held-input repeat (client)

While a movement input is **held**, the client re-issues the action every `Interval`:

- **Unity:** stop treating movement as one-shot `performed`. Track pressed state (`started`→held→`canceled`)
  and, in `Update()`, when an input is held and `now - lastSent >= Interval`, send the move. Read the
  interval from the latest perception (fallback to a sane default until the first frame arrives). Equivalent
  to a manual auto-repeat; alternatively use an Input System `Hold`/repeat interaction, but a timer keyed on
  the server cadence keeps client and server aligned.
- **Console:** the console reads discrete key events; add a repeat loop that, while the OS reports the key
  still down (or within a key-up grace window), re-issues the move at `Interval`.
- **Optional client-side prediction:** to feel responsive, the client may optimistically advance the player
  marker and reconcile on the next authoritative perception. MVP can skip prediction and simply pace repeats
  to the cadence — at 6/s the round-trip is usually invisible on localhost; document the trade-off.

### Why both halves
- Client repeat *alone* would let a fast client outrun a slow character or desync with other players.
- Server cadence *alone* wouldn't give the "hold to keep going" feel.
- Together: the client streams held-input at the character's rate; the server enforces that rate as truth.

## Interaction with other features
- **Flight plans** — the `FlightPlan` follower advances one leg-step per `Interval`, so autonomous flyers and
  held-key players share one pacing model.
- **Gamepad** — a held stick is "held input" exactly like a held key; the same repeat logic drives
  [dual-stick](gamepad-dual-stick.md) movement, including strafing and (while piloting) climb/descend.
- **Diagonal/turn cadence** — rotation and level-change also honor cadence, so holding "turn left" rotates
  repeatedly at the configured rate (matching the user's "hold to turn" requirement), not instantly.

## Phasing
- **Phase 1 — `ActionCadence` + server enforcement.** Component, coalescing/rate-limit in the move/rotate/
  changelevel path, cadence surfaced in perception. Per-world default + per-entity override.
- **Phase 2 — Unity held-repeat.** Held-state tracking + `Update()` repeat paced to cadence for move/rotate/
  changelevel; disabled during option-selection (existing `isChoosingOption` guard).
- **Phase 3 — Console held-repeat.** Key-down repeat loop at cadence.
- **Phase 4 — (optional) client prediction & reconciliation.**

## Risks & trade-offs
- **Input flooding.** Without server coalescing, fast repeats could spam the hub; coalesce/queue on the
  server and cap client send-rate at the cadence.
- **Key-up detection (console).** Terminals report key *events*, not held-state cleanly; use a short
  key-repeat grace window and stop on any other key / timeout.
- **Multiplayer fairness.** Cadence must be enforced server-side so a modified client can't move faster.
- **Feel vs. authority.** Strict server gating can feel laggy on high latency; prediction (Phase 4) mitigates.

## Key source references
- `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs:93-148` (single-shot input today)
- `Aetherium.Server/Agents/Tools/Movement/MoveTool.cs` (move path), `GameSession.MoveView`
- Tick chain / `WorldClock` (`Aetherium.Server/Simulation/WorldClock.cs`) for game-time stamps
- `demo-game` and `console-view` specs (Controls); `client` capability (Unity input)
