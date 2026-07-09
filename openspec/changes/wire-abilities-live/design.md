## Context

`add-abilities` deliberately stopped at data primitives because two dependencies weren't live: the continuous action pipeline and a live caster. The action pipeline is now partially live (`ActionSpeed.TrySpend` gates NPC cadence, per `wire-npc-action-budget-live`) and effect routing is live (`DamagePipeline`, per `wire-combat-pipeline-live`). This change makes abilities per-world content and builds the cast path.

The vision constraint driving every decision: **Aetherium is an engine, not a game.** The engine owns the ability *machinery*; each world declares its abilities as *data*. A fantasy campaign's spells and a sci-fi campaign's tech powers are the same machinery with different `AbilityDefinition` rows.

## Goals / Non-Goals

- Goals:
  - Abilities and their resource pools are **per-world data**, threaded through world creation and persisted, exactly like `DeathPolicy`.
  - A player can cast a damaging and a non-damaging ability end-to-end: RPC → gating → resource/AP spend → effect resolution → delta.
  - Reuse every already-live mechanism (`IsActionable`, `ActionSpeed.TrySpend`, `DamagePipeline`, the Manhattan reach check) rather than parallel machinery.
  - The data model and RPC contract **anticipate phased casting** so it slots in as pure addition later — no retrofit of schema or contract.
- Non-Goals (deferred; see tasks.md "Later slices"):
  - **Phased charge/cast/recover execution.** The timing fields exist on `AbilityDefinition` but this slice executes instantly (effects apply the tick the RPC arrives). The `CastInProgress` component + tick system is the next slice; instant is the degenerate all-zero-duration case, so nothing built here changes when phased lands.
  - **Per-ability AP cost.** This slice spends a single flat `AbilityActionCost` constant (mirroring `NpcActionCost`). Per-ability cost co-designs with phased timing.
  - NPC/monster ability use, AOE/shape targeting, `SkillDefinition.UnlocksAbilityId` grants, the four unshipped effect kinds, and a client push signal.

## Decisions

- **Data/behavior split: `AbilityDefinition` (data) → `AbilityCompiler` → `Ability` (runtime).** This is the load-bearing decision. The primitive `Ability` holds effect *instances* bound to services and so is unserializable; per-world content is impossible without a pure-data descriptor tier. `AbilityDefinition` + `AbilityEffectDescriptor` are that tier; `AbilityCompiler` binds the map's `DamagePipeline`/`IHitResolver` at load time. Mirrors `ContentAtlas` (data schema in `Model`, seeding/consumption in `Server`) and `DeathPolicy` (data) vs `DeathSystem` (behavior). The existing `Ability`/effect classes are unchanged — they simply become the compiled tier.
- **`AbilityEffectDescriptor` is one class with a `Kind` enum + per-kind optional fields**, not a polymorphic type hierarchy — exactly the shape `RespawnLocationPolicy` used for its modes. Avoids Orleans polymorphic-serialization setup for a closed, small effect set, and keeps the descriptor trivially inspectable.
- **Instant execution, flat AP cost, this slice.** Consuming `ChargeTime`/`CastTime`/`RecoverTime` correctly means a multi-tick, interruptible cast state machine — the same shape `add-continuous-action-pipeline`'s own Phase 2 defers. Shipping instant-with-flat-cost keeps this slice's scope to "make abilities live as data," and instant is a strict special case of phased, so the follow-on is additive.
- **`ThreatTable` presence as the in-combat proxy for `ResourceRegenPolicy.OutOfCombat`.** The engine has no dedicated combat-state signal. `ThreatTable` already exists per defender and is populated by `DamagePipeline.Resolve`, so `entity.Has<ThreatTable>() && …ThreatByAttacker.Count > 0` is a zero-new-infrastructure proxy. Accepted as an approximation.
- **`AbilityCooldowns` is a new per-caster component (`Dictionary<abilityId, ticksRemaining>`)**, resolving `add-abilities`' own open question — a shared `Ability` template can't hold per-caster cooldown state. Created lazily on first cast, ticked down in `TickAsync`.
- **Resource pools come only from world config, stamped at `JoinPlayerAsync`.** `Character`'s constructor gains `ActionSpeed` (needed to gate any cast) but **not** `ResourcePools` — a character in a world that declares no pools simply has none, and an ability referencing an absent pool fails closed (`CanAfford` can't be reached). This keeps `Character` genre-neutral: the engine never invents a "mana"/"stamina" pool, the same way `DeathPolicy.Default` never baked in a genre.
- **Post-effect deltas are derived generically, not reported by effects.** Effects stay `void`/opaque. The grain snapshots the target's `Health`/`Dying` state before applying effects and diffs after, emitting a `Health` delta on change and reusing the same monster-defeat analytics/loot block `AttackAsync` uses (extracted to a shared helper so ability-kills and melee-kills never diverge).

## Risks / Trade-offs

- `Character` gaining `ActionSpeed` also touches `Monster` (subclass), which already sets its own `ActionSpeed` in its constructor and thus overwrites the base default — no monster behavior change, but it's a base-class edit on both construction paths.
- Flat AP cost + instant cast are known temporary simplifications, captured as Non-Goals so they aren't mistaken for the final design.
- `ResourcePools` gains a pool-enumeration accessor (needed for per-tick regen) — a minor additive change to an `add-abilities` primitive, not a behavior change.

## Migration Plan

Additive only. `AbilityConfig` is nullable everywhere it's threaded (null → no abilities, no pools — the pre-change state). `Character`'s new `ActionSpeed` defaults to full budget. No existing test or behavior depends on their absence.

## Open Questions

Resolved during scoping (recorded for traceability):

- **Ability content ownership:** per-world data (`AbilityConfig` threaded through world creation), never engine-hardcoded. Engine ships zero abilities.
- **Casting model this slice:** instant execution, flat AP cost. Phased charge/cast/recover deferred to the next slice, with the schema carrying timing fields now so it's a pure addition.
- **In-combat regen signal:** `ThreatTable` presence.
- **Resource pool origin:** world config, stamped at join; `Character` carries no pools by default.
- **Targeting scope this slice:** self-target and single-entity-target (reach-checked) only; no AOE/shape.
