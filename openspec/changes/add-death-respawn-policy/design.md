## Context

`Dying`/`Corpse`/`DeathSystem` (shipped in `deepen-combat-model`) transition a lethally-hit entity to `Dying` for a fixed 3-tick countdown, then to `Corpse` — permanently, since nothing removes a `Corpse`. §4.11 specs a per-world policy governing these choices. This change ships that policy as data plus one new opt-in expiry system; see [proposal.md](proposal.md) for why live wiring (a persisted per-world policy, an actual respawn flow) is a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - `DeathPolicy` fields matching §4.11's design sketch exactly, with a `Default` that reproduces today's shipped behavior byte-for-byte (so adopting the policy later is behavior-neutral until a world opts into a non-default policy).
  - `ResolveDyingTicks()` as the one behavior `DeathPolicy` exposes today — a pure function, safely callable without any live wiring.
  - `CorpseExpirySystem` as strictly additive: entities without `CorpseAge` are provably unaffected (tested explicitly).
- Non-Goals (Phase 2 / later):
  - Persisting `DeathPolicy` per world (`MapState` extension).
  - Attaching `CorpseAge` from the live attack path.
  - `DropOnDeath`/`RespawnPoint`/`XpLossPolicy` having any behavior at all yet — they're declared schema fields with no consumer. Loot-drop timing already has an open question from `deepen-combat-model` Phase 2 (task 2.2); respawn needs a session/grain design; XP loss needs `add-character-progression` to exist first (a pool to lose XP from). Wiring any of these before their dependency exists would be schema theater.
  - `WorldPersistencePolicy` (generator-version migration) — see proposal.md's scope note.

## Decisions

- **`CorpseAge` is a new component, not a field added to the existing `Corpse`.** `Corpse` shipped in `deepen-combat-model` as an empty marker with an already-published requirement ("Death State Transition") whose scenarios don't mention aging. Keeping `Corpse` untouched means `deepen-combat-model`'s existing tests and spec stay exactly correct with zero risk of a silent behavior change; `CorpseExpirySystem` is opt-in per entity (has `CorpseAge` or doesn't), which is also how a future "this corpse is important, never expire it" quest object would naturally stay exempt without a special-case flag.
- **`DeathPolicy` is a plain data class, not an ECS `Component`.** It's per-world config, not per-entity state — same category as `SimulationOptions`, but deliberately *not* implemented as a global `IOptions<T>` binding like `SimulationOptions` is, because the design doc is explicit this must be per-world (a hardcore-mode world and a casual world coexisting in one cluster). Phase 2's persistence decision (likely a `MapState` field) is called out rather than guessed at now, since `MapState`'s owning grain/pattern may evolve before that phase starts.
- **`ResolveDyingTicks()` lives on `DeathPolicy` itself**, not a separate resolver class, because it's a one-line pure function of the policy's own fields (`DownStateEnabled ? ReviveWindowTicks : 0`) — a whole class would be premature structure for one method.

## Risks / Trade-offs

- **`DeathPolicy`'s non-dying-ticks fields (`DropOnDeath`, `RespawnPoint`, `XpLossPolicy`) are unused schema today.** Accepted deliberately (see Non-Goals) — each has a real dependency that doesn't exist yet (a loot system decision, a respawn flow, a progression system), and guessing their consumption shape now risks a wrong abstraction that has to be redone. The schema itself is cheap and low-risk to ship ahead of its consumers, matching this project's established Phase-1-schema-first pattern.
- **Two corpse-lifetime behaviors coexist**: policy-less (forever, today's shipped default) and policy-driven (expires via `CorpseAge`). This is intentional, not accidental complexity — it's the explicit backward-compatibility mechanism, verified by a dedicated test.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) persists `DeathPolicy` per world and wires the live attack/death path to consult it.

## Open Questions

- Should `CorpseRetentionTicks` support "never expire" as a sentinel (e.g. a negative value) distinct from "the world's `DeathPolicy.Default` doesn't attach `CorpseAge` at all"? Two ways to express the same outcome. Deferred to Phase 2, where the actual attach-site decides which is more ergonomic for callers.
