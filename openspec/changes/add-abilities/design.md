## Context

No ability/resource-pool concept exists anywhere in the codebase (re-verified by grep). §4.3 specs a genre-agnostic data asset plus composable effects. This change ships both, deliberately wiring its effects through the already-shipped `deepen-combat-model` pipeline rather than a parallel damage/status system; see [proposal.md](proposal.md) for why action-pipeline consumption and the remaining effect kinds are a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - `ResourcePool` general enough to cover every example the design doc names, including the unusual "heat" inverse-pool case, without a special-cased type.
  - `Ability` as pure data (no behavior beyond holding its `Effects` list) — genre neutrality lives entirely in string tags (`ResourcePoolTag`, `TargetShape`), never a hardcoded enum of spell schools.
  - Effects that **compose with what's already shipped**: `DealDamageEffect`/`ApplyStatusEffect` are thin adapters onto `DamagePipeline`/`StatusEffects`, proving the ability system doesn't need its own damage/status machinery.
- Non-Goals (Phase 2 / later):
  - Any consumption of the `add-continuous-action-pipeline`'s `ActionSpeed`/`ActionQueue` for charge/cast/recover timing — that pipeline isn't wired into live grains yet either (its own Phase 2), so this would be coupling two not-yet-live systems.
  - `Teleport`/`Spawn`/`Summon`/`TriggerNarrativeEvent` effect kinds — each needs a real integration point (world mutation validation, `EntityFactory`, `NarrativeConsequenceEngine`) that doesn't have an obvious "just call this method" shape yet; inventing one now risks guessing wrong ahead of Phase 2's actual live-wiring design.
  - Attaching default `ResourcePools` to any entity.
  - Resolving `SkillDefinition.UnlocksAbilityId` (from `add-character-progression`) against this catalog — that's the natural Phase 2 bridge between the two changes, not done here since neither is live yet.

## Decisions

- **`ResourcePool` combines config and runtime state in one mutable object**, unlike `ProgressPools`' split of `ProgressPool` (state) + `ILevelCurve` (injected strategy). A resource pool's "policy" (regen rate, regen trigger, inverse-or-not) is fixed at authoring time and doesn't need runtime-injected pluggability the way XP-to-level conversion does (campaigns are expected to vary that formula; they aren't expected to vary "how mana regens" per-call). This is a deliberate asymmetry, not an inconsistency — each shape was chosen for what actually varies.
- **`IsInverse` + `OverheatThreshold` model "heat"-style pools directly on `ResourcePool`**, rather than a separate `InverseResourcePool` type, because every other field and both `TrySpend`/`Regen` operations are identical in shape — only the sign of the delta differs. A parallel type would duplicate the whole class for one sign flip.
- **`DealDamageEffect`/`ApplyStatusEffect` are no-ops on a missing target/component**, not throwing exceptions. An ability whose target moved out of range or lost its `StatusEffects` component between queuing and resolution is a normal, expected runtime condition — not a program error — matching this codebase's existing `CombatSystem.TryAttack`/`DamagePipeline.Resolve` convention of returning a failure result rather than throwing.
- **Effects only three of the seven kinds the design doc lists.** Shipping `DealDamage`/`ApplyStatus`/`ModifyResource` now (all three have an existing, shipped target system to route through) and explicitly deferring the other four (none do) keeps every shipped line of code backed by a real integration, not a stub that merely type-checks.

## Risks / Trade-offs

- **No live caster/target exists yet.** Zero risk to running gameplay — `Aetherium.Server/Abilities/` is new and unreferenced outside its own tests.
- **`Ability.ChargeTime`/`CastTime`/`RecoverTime`/`Cooldown` are unconsumed data fields today.** Accepted deliberately (see Non-Goals) — consuming them means deciding how they interact with `ActionSpeed`, which is exactly the kind of decision this project's phased approach defers until the dependency (a live action pipeline) actually exists.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) wires abilities into the action pipeline and adds the remaining effect kinds.

## Open Questions

- Should `Cooldown` tracking live on `Ability` (shared across all casters of that ability template) or per-caster (a `Dictionary<abilityId, ticksRemaining>` component)? Almost certainly per-caster, but not decided here since nothing consumes `Cooldown` yet.
