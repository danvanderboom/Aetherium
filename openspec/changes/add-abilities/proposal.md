## Why

The [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §4.3 (Wave 1) confirms there is no ability/spell system at all: no ability registry, no cooldown tracker, no resource-pool component, no cast/channel/instant taxonomy. §4.3 is explicitly framed as the genre-agnostic replacement for "magic/spells" — a swing, a spell, a hack, and a prayer must all be expressible as the same data shape.

## What Changes

- Add `ResourcePool`/`ResourcePools`: a data-driven resource pool (mana, stamina, focus, battery, oxygen, hack-charges are all instances) with pluggable regen policy (`Continuous`, `OutOfCombat`, `OnHit`) and support for **inverse** pools ("heat" that fills with use and vents via regen, rather than draining with use).
- Add `Ability`/`AbilityCatalog`: a data asset (`ResourcePoolTag`/`ResourceCost`, `ChargeTime`/`CastTime`/`RecoverTime`/`Cooldown`, `Range`, `TargetShape`, `Effects`, `Tags`) and its registry.
- Add `IAbilityEffect` + three concrete, composable effects that **reuse already-shipped systems** rather than inventing parallel ones: `DealDamageEffect` (routes through `deepen-combat-model`'s `DamagePipeline`), `ApplyStatusEffect` (routes through its `StatusEffects`), `ModifyResourceEffect` (adjusts a `ResourcePool` on caster or target).
- **Phase 1 (this change): resource pools, the ability data asset, and three effect kinds, fully unit-tested (35 tests) in isolation.** No entity carries a default `ResourcePools`, no ability's `ChargeTime`/`CastTime`/`RecoverTime` consumes the `ActionSpeed` budget from `add-continuous-action-pipeline`, and no command path can actually cast an ability.
- Phase 2 (follow-up change): consume the continuous action pipeline's `ActionQueue`/`ActionSpeed` for charge/cast/recover phases; add the remaining effect kinds the design doc names (`Teleport`, `Spawn`, `Summon`, `TriggerNarrativeEvent`) once their target subsystems (world mutation, entity factory, narrative consequence engine) have a clear integration point; wire `SkillDefinition.UnlocksAbilityId` (from `add-character-progression`) to actually grant an ability; attach default `ResourcePools` to `Character` construction.

## Impact

- Affected specs: new capability `abilities` (resource pools, ability data asset, composable effects)
- Affected code: new `Aetherium.Server/Abilities/*.cs`, new tests under `Aetherium.Test/Abilities/`. No changes to `Combat/DamagePipeline.cs`, `Combat/StatusEffect.cs`, `add-continuous-action-pipeline`'s `ActionSystem`, or any grain/command path in this change.
