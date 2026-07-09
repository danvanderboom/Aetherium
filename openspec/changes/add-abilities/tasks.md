## 1. Resource pools, ability data asset, composable effects (Phase 1 — this change)

- [x] 1.1 `ResourcePool` (normal + inverse/overheat) with `TrySpend`/`CanAfford`/`Regen`/`GainOnHit`, `ResourceRegenPolicy` (`Continuous`/`OutOfCombat`/`OnHit`) + `ResourcePools` component
- [x] 1.2 `Ability` data asset + `AbilityCatalog`
- [x] 1.3 `IAbilityEffect`/`AbilityEffectContext` + `DealDamageEffect` (via `DamagePipeline`), `ApplyStatusEffect` (via `StatusEffects`), `ModifyResourceEffect`
- [x] 1.4 Unit tests (35 tests): resource pool spend/regen for both normal and inverse pools across all three regen policies, ability catalog add/get/duplicate-rejection, each effect's happy path and no-op-on-missing-target/component path
- [x] 1.5 `openspec/specs/abilities/spec.md` delta: ADDED requirements, each with a `**Verified by:**` line

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Consume `ActionSpeed`/`ActionQueue` (`add-continuous-action-pipeline`) for an ability's charge/cast/recover phases
- [ ] 2.2 Add `Teleport`, `Spawn`, `Summon`, `TriggerNarrativeEvent` effect kinds once their target subsystems have a clear integration point
- [ ] 2.3 Attach default `ResourcePools` to `Character` construction
- [ ] 2.4 Resolve `SkillDefinition.UnlocksAbilityId` (`add-character-progression`) against `AbilityCatalog` to actually grant abilities
- [ ] 2.5 Design per-caster `Cooldown` tracking (a component, not a field on the shared `Ability` template)
- [ ] 2.6 Build a real cast command path (client input → ability lookup → effect resolution → perception event)
