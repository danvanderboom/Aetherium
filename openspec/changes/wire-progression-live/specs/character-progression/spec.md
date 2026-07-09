## ADDED Requirements

### Requirement: Per-World Progression Config
Character progression SHALL be per-world data, not an engine-hardcoded set. A world's `ProgressionConfig` (progress-pool definitions with level curves, a skill catalog, starting attributes/role-affinity, XP-award rules, attribute-derivation rules, and a `RequireSkillToCastAbilities` flag) SHALL be specifiable at world-creation time (via `WorldConfig.ProgressionConfig` or `WorldTemplate.ProgressionConfig`/`CreateWorldRequest.ProgressionConfig`), persisted per-world, and applied to every map the world creates — both the initial map and any map added later. A map created on a world with no `ProgressionConfig` SHALL stamp no progression components onto joining characters. The engine SHALL ship zero progression content; the pools, curves, attributes, and skills are entirely campaign-supplied data.

`ProgressionConfig` SHALL be pure serializable data; a server-side `ProgressionCompiler` SHALL compile it into the runtime tier (per-pool `ILevelCurve` instances, a `SkillCatalog`, and fresh per-character `ProgressPools`/`Attributes`/`UnlockedSkills`/`RoleAffinity`/`GrantedAbilities` components).

**Verified by:** `Aetherium.Test.Progression.ProgressionCompilerTests.CompilePools_ProducesWorkingPoolsAndCurves`, `.CompileSkillCatalog_ProducesCatalog`, `.BuildComponents_FromStartingDicts`, `Aetherium.Test.Progression.PerWorldProgressionConfigTests.WorldProgressionConfig_ReachesEveryMapItCreates`, `.CreateWorldRequest_ProgressionConfig_ReachesTheCreatedMap`, `.NoProgressionConfig_NoComponentsStamped`

#### Scenario: A world's progression config reaches every map it creates
- **WHEN** a world is initialized with a `ProgressionConfig` and creates both an initial map and a later map via `AddMapAsync`
- **THEN** a player joining either map carries the config's progress pools, attributes, and skills

#### Scenario: No progression config means no progression
- **WHEN** a world is initialized with `ProgressionConfig` left null
- **THEN** joining characters carry no `ProgressPools`/`Attributes`/`UnlockedSkills`, and `GetProgressionAsync` reports empty

### Requirement: XP Award On Kill
When a player defeats a monster (the target entering `Dying`), the map SHALL award XP to the killer's `ProgressPools` per the world's declarative `XpAwardRule`s, matching on event (`MonsterDefeated`) and optional enemy-type filter, and recompute each affected pool's level via its configured curve. The award SHALL apply identically whether the kill came from a melee `AttackAsync` or an ability `UseAbilityAsync`.

**Verified by:** `Aetherium.Test.Progression.ProgressionLiveTests.MonsterKill_Melee_AwardsXp_AndLevelsPool`, `.MonsterKill_Ability_AwardsXp_AndLevelsPool`

#### Scenario: A melee kill awards XP and can level a pool
- **WHEN** a player with a `combat` pool kills a monster under a rule awarding `combat` XP, enough to cross a level boundary
- **THEN** the pool's XP increases by the rule amount and its level is recomputed upward

#### Scenario: An ability kill awards the same XP as a melee kill
- **WHEN** the same lethal blow is delivered by an ability instead of a melee attack
- **THEN** the identical `XpAwardRule` fires and the pool is credited the same amount

### Requirement: Skill Unlock & Ability Grant
The engine SHALL provide `IGameMapGrain.UnlockSkillAsync(sessionId, skillId)` that unlocks a skill via `SkillUnlockService`, gated on its prerequisites and — when the skill declares one — a `RequiredPoolLevel` (the actor's named pool must be at least that level). On a successful unlock the map SHALL apply the skill's effects: `ModifiesAttributeId` adjusts the actor's `Attributes` by `ModifierAmount` (re-deriving any dependent stats), and `UnlocksAbilityId` adds the ability id to the actor's `GrantedAbilities`. A cast SHALL require the ability be in the caster's `GrantedAbilities` only when the world's `RequireSkillToCastAbilities` is true; when false (the default), catalog membership remains the sole ability gate.

**Verified by:** `Aetherium.Test.Progression.SkillUnlockServiceTests.RequiredPoolLevel_BelowIsRejected`, `.RequiredPoolLevel_AtOrAboveIsAccepted`, `Aetherium.Test.Progression.ProgressionLiveTests.UnlockSkill_RespectsPrerequisitesAndPoolLevelGate`, `.UnlockSkill_GrantsAbility_AndModifiesAttribute`, `.RequireSkillToCastAbilities_True_GatesUngrantedCast_AllowsGranted`

#### Scenario: A skill gated on pool level cannot be unlocked too early
- **WHEN** a skill declares `RequiredPoolId = "combat"`, `RequiredLevel = 3` and the actor's `combat` pool is level 1
- **THEN** `UnlockSkillAsync` fails with a pool-level-too-low reason and the skill stays locked

#### Scenario: Unlocking a skill grants its ability and modifies its attribute
- **WHEN** a skill with `UnlocksAbilityId` and `ModifiesAttributeId` is unlocked
- **THEN** the ability id is added to the actor's `GrantedAbilities` and the named attribute changes by `ModifierAmount`

#### Scenario: Skill-gated casting is enforced only when the world opts in
- **WHEN** `RequireSkillToCastAbilities` is true and a player casts an ability not in their `GrantedAbilities`
- **THEN** the cast is rejected; the same cast succeeds once the granting skill is unlocked; and with the flag false the cast is allowed regardless of grants

### Requirement: Attribute-Derived Stats
A world MAY declare `AttributeDerivation`s mapping an attribute onto a derived stat (`HealthMax` from vitality, `ActionSpeed` from speed) as `Base + PerPoint × attributeValue`. The map SHALL apply derivations when a character joins (from its starting attributes) and after any attribute change (e.g. a skill's `ModifiesAttributeId`), writing the derived value onto the corresponding component. Derivations SHALL be applied on these change events, not polled every tick. A world with no derivations SHALL leave `Health`/`ActionSpeed` at their constructed defaults.

**Verified by:** `Aetherium.Test.Progression.ProgressionLiveTests.AttributeDerivation_SetsMaxHealth_AtJoin`, `.AttributeDerivation_ReDerives_AfterSkillModifiesAttribute`

#### Scenario: Max health is derived from vitality at join
- **WHEN** a world declares `HealthMax = Base 0 + 1×vitality` and a starting `vitality` of 150
- **THEN** a joining character's `Health.MaxLevel` is 150 rather than the constructed default

#### Scenario: A stat re-derives when a skill changes its attribute
- **WHEN** a skill raises `vitality` by 20 and health is derived from vitality
- **THEN** the character's `Health.MaxLevel` increases by 20 after the unlock
