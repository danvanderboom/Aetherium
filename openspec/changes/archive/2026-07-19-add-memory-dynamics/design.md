## Context

Layer-1 memory (add-character-memory, merged) records what a session-bound character perceives into their `Memory` component and decays it lazily at read time: `effective = strength × 0.5^(age/halfLife)` with one `DecayHalfLifeSeconds` for the whole world. `Impressions` and `LastEventTime` update on re-encounter, so re-perceiving already *resets the decay clock* — but the decay **rate** never changes, nothing becomes permanent, and nothing is ever culled (only the location cap prunes, by recency, regardless of strength).

The request: memory that follows the curve real memory follows — repeated spaced visits make a memory more durable, eventually permanently so; unvisited places genuinely fade away; and how good a character's memory is should be configurable per character (forgetful vs. eidetic), with the whole system opt-in per world.

## Goals / Non-Goals

**Goals**
- A decay model where reinforcement grows durability (spacing effect) and sufficient familiarity becomes permanence.
- Genuine forgetting: weak memories leave the component, not just approach zero.
- Per-character memory quality as data (`MemoryProfile`), per-world defaults as data (`MemoryPolicy`), all opt-in.
- Reads stay pure (no mutation on read); recording stays confined to the existing perception-time write path.
- With dynamics off (default), behavior is exactly today's — byte-identical decay math and no culling.

**Non-Goals**
- Full FSRS/SM-18-style modeling (retrievability-dependent stability increments, per-item difficulty). See Decision 1.
- Game-time decay. Layer 1 shipped on real time (`DateTime.Now` via `TimeSinceLastSeen`); switching time bases is a separate, deliberate migration for both layers at once.
- Memory persistence across grain deactivation (components are not in map snapshots; unchanged).
- NPC perception ticks. The model is character-agnostic; NPCs start writing memory when something computes perception for them (`add-identity-recognition` adds their first writer for individuals).

## Decisions

### 1. Model: per-memory stability with multiplicative spaced growth (not fixed-rate, not full FSRS)

Three options were considered:

- **A — fixed rate + per-character multiplier.** Cheapest, but cannot express "familiar places stick longer": every memory of a character decays identically no matter how often it was reinforced. Rejected as not answering the request.
- **B — per-memory stability, multiplicative growth on spaced re-exposure (chosen).** Each memory carries `StabilitySeconds` (its own half-life). On a *spaced* re-encounter, `stability ×= StabilityGrowthFactor` (default 2.0) and strength refreshes to 1.0. This is the shape spaced-repetition systems (SM-2 through FSRS) converge on: exponential retention against a stability that grows geometrically with successful spaced reviews. It reproduces the requested behavior directly: ~10 spaced revisits at defaults carries a 1-hour half-life past the 30-day permanence threshold.
- **C — full FSRS.** Adds retrievability-dependent increments (reinforcing an almost-forgotten memory grows stability more) and per-item difficulty. More faithful, but its parameters are fit to human flashcard data, are hard for a game designer to author, and the observable in-game difference from B is subtle. Rejected for this slice; B's fields (`stability`, `strength`, `LastEventTime`) are a strict subset of what C needs, so C remains a compatible future refinement.

The math lives in `MemoryPolicy` alongside the existing `EffectiveStrength` as pure static functions, so `add-identity-recognition` can reuse the identical curve for individual familiarity.

### 2. Spacing gate: reinforcement requires elapsed time

Perception frames arrive continuously; without a gate, standing still would multiply stability every frame and make a wall permanent in seconds. A re-encounter only grows stability when `now − LastEventTime ≥ MinReinforcementIntervalSeconds` (default 60). Massed re-exposure still bumps `Impressions` and `LastEventTime` (today's behavior), so the decay clock keeps resetting — it just doesn't compound durability. This is also what the memory literature says: massed exposure barely improves retention; spacing does.

### 3. Back-compat encoding: `StabilitySeconds == 0` means "use the world half-life"

Existing `SpaceTimeMemory` rows (and worlds with dynamics off) have no stability. Rather than a nullable or a migration, `0` (the default) reads as "policy half-life × profile multiplier". New reinforcements initialize stability from that same effective base before growing it. `EffectiveStrength` reads therefore work identically for old and new rows.

### 4. Culling: at write time, on touched locations plus the existing cap sweep

Reads must stay pure, so forgetting happens where writes already happen:
- When recording touches a location, entries in that location's list below `ForgetThreshold` effective strength are removed (cheap: the list is already in hand).
- The existing `MaxLocations` cap sweep additionally drops locations whose entries are all below threshold before falling back to oldest-first pruning.

This bounds cull work to O(what perception already visits) per frame instead of O(total memories), at the cost of stale below-threshold entries lingering at never-revisited locations until a cap sweep. That's acceptable: they read as ~0 effective strength, and the cap bounds total storage. `ForgetThreshold` default 0.05 (with a 1-hour half-life, an unreinforced memory culls ≈4.3 hours after last sight); `0` disables culling.

### 5. Per-character profile is a component; per-world policy is data; one switch

`MemoryProfile { HalfLifeMultiplier = 1.0, StabilityGrowthMultiplier = 1.0, MaxLocationsOverride = null }` — absent component ⇒ world defaults. Forgetful NPC: multiplier 0.2; sharp: 5.0. Settable at spawn or via the runtime `configurecharacter` worldbuilding tool (added by `add-identity-recognition`, which needs it for its own profiles too).

Everything is gated by one flag, `MemoryPolicy.DynamicsEnabled` (generator parameter `MemoryDynamicsEnabled`, default `false`), per the "opt-in config option for a game to use or not" requirement. Off ⇒ the recording path takes exactly its current branch: no stability writes, no permanence, no culls.

### 6. Permanence is a one-way latch

`Permanent = true` once stability ≥ `PermanenceThresholdSeconds` (default 30 days ≈ 10 spaced revisits from a 1-hour base at growth 2.0). Permanent memories skip decay and culling, and are exempt from strength-based pruning — but still count against `MaxLocations` (a hard resource bound outranks fidelity).

## Risks / Trade-offs

- **Real-time decay under compressed game time** feels wrong for long-lived worlds (an hour of wall clock ≠ an hour of game time). Accepted: consistent with shipped Layer 1; both migrate together later.
- **Touched-location culling leaves cold spots uncalled** until a cap sweep. Accepted per Decision 4.
- **`Strength` refresh to 1.0 on reinforcement** discards the fractional pre-reinforcement value. This matches retrieval-based refresh in the literature (successful retrieval restores retention) and keeps the model two-parameter.
- **Component state is not persisted** across grain rehydration — unchanged from Layer 1, restated so nobody assumes permanence survives a silo restart.

## Migration

None required. Default-off flag; existing worlds, rows, and tests are untouched. `MemoryEntryDto` gains additive fields only.
