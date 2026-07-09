## ADDED Requirements

### Requirement: Per-World Ability Config
Abilities and their resource pools SHALL be per-world data, not an engine-hardcoded set. A world's `AbilityConfig` (a list of `AbilityDefinition`s plus the `ResourcePoolDefinition`s its characters start with) SHALL be specifiable at world-creation time (via `WorldConfig.AbilityConfig` or `WorldTemplate.AbilityConfig`/`CreateWorldRequest.AbilityConfig`), persisted per-world, and applied to every map the world creates — both the initial map and any map added later. A map created on a world with no specified `AbilityConfig` SHALL have no abilities available and stamp no resource pools onto joining characters. A map's active ability catalog SHALL survive grain reactivation without re-running initialization. The engine SHALL ship zero built-in abilities; the ability set is entirely campaign-supplied data.

`AbilityDefinition` SHALL be pure, serializable data (no bound services or effect instances); a server-side `AbilityCompiler` SHALL compile it into the runtime `AbilityCatalog`, binding the map's `DamagePipeline`/`IHitResolver` into the resulting effects. `AbilityEffectDescriptor` SHALL carry an effect `Kind` plus that kind's parameters in a single serializable shape (no polymorphic type hierarchy).

**Verified by:** `Aetherium.Test.Abilities.AbilityCompilerTests.CompileCatalog_DealDamageDescriptor_ProducesDamageDealingAbility`, `.CompileCatalog_ModifyResourceDescriptor_ProducesResourceModifyingAbility`, `.BuildResourcePools_ProducesWorkingPoolsFromDefinitions`, `Aetherium.Test.Abilities.PerWorldAbilityConfigTests.WorldAbilityConfig_ReachesEveryMapItCreates`, `.CreateWorldRequest_AbilityConfig_ReachesTheCreatedMap`, `Aetherium.Test.Abilities.AbilityCastTests.NoAbilityConfig_EveryAbilityIsUnknown_AndNoPoolsStamped`

#### Scenario: A world's ability config reaches every map it creates
- **WHEN** a world is initialized with an `AbilityConfig` and creates both an initial map and a later map via `AddMapAsync`
- **THEN** a player on either map can cast the config's abilities, and joining characters carry the config's resource pools

#### Scenario: No ability config means no abilities
- **WHEN** a world is initialized with `AbilityConfig` left null
- **THEN** `UseAbilityAsync` on its maps rejects every ability id as unknown, and joining characters carry no resource pools

#### Scenario: A definition is pure data compiled to a runtime ability
- **WHEN** an `AbilityDefinition` with a `DealDamage` effect descriptor is compiled by `AbilityCompiler`
- **THEN** the resulting `Ability` deals damage through the map's `DamagePipeline` when cast, without the definition itself holding any pipeline reference

### Requirement: Live Ability Cast Path
The engine SHALL provide `IGameMapGrain.UseAbilityAsync(sessionId, abilityId, targetEntityId?)` that resolves a player's ability against the map's compiled `AbilityCatalog`. On success it SHALL apply the ability's effects in order via an `AbilityEffectContext` (caster = the player, target = the resolved entity when supplied) and fan out any resulting world delta — in particular a `Health` change on a damaged target, handled identically to a melee attack (the target enters `Dying` rather than being removed, and a defeated monster's loot/analytics match `AttackAsync`). A cast SHALL restore nothing and mutate no world state when any gate rejects it.

**Verified by:** `Aetherium.Test.Abilities.AbilityCastTests.DamagingCast_ReducesTargetHealth_ThroughDamagePipeline`, `.DamagingCast_DefeatingMonster_EntersDying_AndDropsLoot_LikeMelee`, `.ResourceModifyCast_ChangesCastersOwnPool`, `.Cast_UnknownAbility_IsRejected`

#### Scenario: A damaging cast reduces the target's health
- **WHEN** a player casts a `DealDamage` ability at an adjacent monster
- **THEN** the monster's `Health` is reduced by the pipeline-resolved amount and a `Health` delta is fanned out

#### Scenario: A cast that defeats a monster behaves like a melee kill
- **WHEN** a damaging cast reduces a monster to zero health
- **THEN** the monster enters `Dying` (not removed), drops loot, and increments the map's monsters-defeated analytics — identical to a killing melee attack

### Requirement: Ability Resource & Cooldown Gating
`UseAbilityAsync` SHALL reject a cast, without mutating any state, when: the caster carries `Downed` or `Corpse`; the ability id is unknown to the map's catalog; the ability is on cooldown for that caster (tracked per-caster in an `AbilityCooldowns` component); the ability's resource pool is absent or cannot afford its `ResourceCost`; a targeted ability's target is missing, has no location, or is beyond the ability's range; or the caster's `ActionSpeed` budget cannot afford the cast. Resource and action-budget SHALL be committed only after every gate passes. A successful cast SHALL place the ability on cooldown for its `Cooldown` duration.

**Verified by:** `Aetherium.Test.Abilities.AbilityCastTests.Cast_WhileDowned_IsRejected`, `.Cast_OnCooldown_IsRejected`, `.Cast_InsufficientResource_IsRejected_AndPoolUnchanged`, `.Cast_TargetOutOfReach_IsRejected`, `.Cast_Success_PutsAbilityOnCooldown`

#### Scenario: A cast on cooldown is rejected
- **WHEN** a player casts an ability with a nonzero cooldown, then casts it again before the cooldown elapses
- **THEN** the second cast is rejected and no resource is spent

#### Scenario: An unaffordable cast leaves the pool untouched
- **WHEN** a player casts an ability whose `ResourceCost` exceeds the caster's pool `Current`
- **THEN** the cast is rejected and the pool's `Current` is unchanged

### Requirement: Ability Tick Upkeep
On each `TickAsync`, the map SHALL count down every actor's `AbilityCooldowns` and regenerate every actor's `ResourcePools` per each pool's `ResourceRegenPolicy`, using the presence of a non-empty `ThreatTable` as the in-combat signal that gates `OutOfCombat`-policy regen.

**Verified by:** `Aetherium.Test.Abilities.AbilityCastTests.Cooldown_TicksDown_OverTicks_ThenAbilityCastableAgain`, `.ResourcePool_Regenerates_OverTicks`

#### Scenario: A cooldown elapses over successive ticks
- **WHEN** a player casts an ability with an N-tick cooldown, then the map ticks N times
- **THEN** the ability is castable again

#### Scenario: A resource pool regenerates over ticks
- **WHEN** a player spends part of a `Continuous`-regen pool, then the map ticks
- **THEN** the pool's `Current` has moved back toward `Max`
