## ADDED Requirements

### Requirement: Resource Pools
The system SHALL provide a `ResourcePool` covering both normal pools (spending drains `Current`, regen fills it toward `Max`) and inverse pools (spending fills `Current` toward an `OverheatThreshold`, regen vents it toward zero), each with a selectable `ResourceRegenPolicy` (`Continuous`, `OutOfCombat`, `OnHit`). Spending SHALL be refused, with `Current` left unchanged, whenever it is not affordable.

**Verified by:** `Aetherium.Test.Abilities.ResourcePoolTests.NormalPool_TrySpend_DrainsCurrent_WhenAffordable`, `.NormalPool_TrySpend_Fails_WhenNotAffordable`, `.NormalPool_Regen_Continuous_FillsTowardMax`, `.NormalPool_Regen_OutOfCombat_DoesNothingWhileInCombat`, `.NormalPool_Regen_OnHit_DoesNotAutoRegen_UseGainOnHitInstead`, `.InversePool_TrySpend_FillsTowardOverheat_InsteadOfDraining`, `.InversePool_TrySpend_Fails_WhenWouldExceedOverheatThreshold`, `.InversePool_Regen_Vents_TowardZero`, `.ResourcePools_TryGet_ReturnsAddedPool_ByTag`

#### Scenario: A normal pool drains on spend and refuses when unaffordable
- **WHEN** a pool with `Current = 10` is asked to spend `30`
- **THEN** the spend is refused and `Current` remains `10`

#### Scenario: An inverse pool fills on spend and refuses at the overheat threshold
- **WHEN** an inverse pool with `Current = 70` and `OverheatThreshold = 80` is asked to spend `20`
- **THEN** the spend is refused (it would exceed the threshold) and `Current` remains `70`

#### Scenario: Regen direction depends on whether the pool is inverse
- **WHEN** a normal pool regens under `Continuous` policy
- **THEN** `Current` moves toward `Max`; when an inverse pool regens under the same policy, `Current` moves toward zero instead

### Requirement: Ability Data Asset
The system SHALL provide an `Ability` data asset (`ResourcePoolTag`/`ResourceCost`, `ChargeTime`/`CastTime`/`RecoverTime`/`Cooldown`, `Range`, `TargetShape`, `Effects`, `Tags`) and an `AbilityCatalog` registry that rejects a duplicate `Id`.

**Verified by:** `Aetherium.Test.Abilities.AbilityCatalogTests.Add_ThenTryGet_ReturnsTheSameAbility`, `.Add_DuplicateId_IsRejected`, `.TryGet_UnknownId_ReturnsFalse`

#### Scenario: A registered ability is retrievable by id
- **WHEN** an `Ability` with `Id = "fireball"` is added to a `AbilityCatalog`
- **THEN** `TryGet("fireball", ...)` returns it with its fields intact

#### Scenario: A duplicate id is rejected
- **WHEN** an `Ability` with an already-registered `Id` is added
- **THEN** the add is rejected and the original registration is unchanged

### Requirement: Composable Ability Effects
The system SHALL provide `IAbilityEffect` implementations that reuse existing engine systems rather than duplicating them: `DealDamageEffect` SHALL resolve through the combat model's `DamagePipeline`; `ApplyStatusEffect` SHALL apply to the target's `StatusEffects` component; `ModifyResourceEffect` SHALL adjust a named `ResourcePool` on either the caster or the target. Each effect SHALL be a no-op — not throw — when its target or the target's required component is absent.

**Verified by:** `Aetherium.Test.Abilities.AbilityEffectTests.DealDamageEffect_RoutesThrough_ExistingDamagePipeline`, `.DealDamageEffect_NoTarget_IsNoOp`, `.ApplyStatusEffect_AddsStatusToTarget`, `.ApplyStatusEffect_TargetWithoutStatusEffectsComponent_IsNoOp`, `.ModifyResourceEffect_OnCaster_AdjustsCastersOwnPool`, `.ModifyResourceEffect_OnTarget_AdjustsTargetsPool_NotCasters`

#### Scenario: DealDamageEffect reduces the target's health via the existing damage pipeline
- **WHEN** a `DealDamageEffect` with a damage packet is applied against a target with `Health`
- **THEN** the target's `Health` is reduced by the pipeline's resolved (mitigated) amount

#### Scenario: An effect with a missing target is a no-op, not an error
- **WHEN** any effect is applied with no target present
- **THEN** it returns without throwing and without mutating any state

#### Scenario: ModifyResourceEffect targets the caster or the target based on its own flag
- **WHEN** a `ModifyResourceEffect` is constructed with `onCaster: true`
- **THEN** it adjusts the caster's own `ResourcePool`, not the target's, even when a target is present
