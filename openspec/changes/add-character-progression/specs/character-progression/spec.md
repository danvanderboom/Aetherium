## ADDED Requirements

### Requirement: Per-Campaign Attributes
The system SHALL provide an `Attributes` component storing a string-keyed vector of named values, so a campaign can define arbitrary attributes without an engine code change. The engine SHALL ship `Vitality` and `Speed` as named constants for its own use; querying an unset attribute SHALL return a caller-specified default (zero if unspecified).

**Verified by:** `Aetherium.Test.Progression.AttributesAndRoleAffinityTests.Attributes_UnsetName_ReturnsDefaultValue`, `.Attributes_SetAndGet_ArbitraryCampaignDefinedName`, `.Attributes_EngineDefaults_AreNamedConstants`

#### Scenario: Unset attribute returns the caller's default
- **WHEN** `Attributes.Get("strength", defaultValue: 10)` is called and `strength` was never set
- **THEN** it returns `10`

#### Scenario: A campaign-defined attribute not known to the engine works identically
- **WHEN** a campaign sets an attribute named `hacking` (not one of the engine's own `Vitality`/`Speed` constants)
- **THEN** `Get`/`Has` behave identically to any engine-known attribute name

### Requirement: Generic Progress Pools
The system SHALL provide `ProgressPools`, holding any number of independently-tracked named `ProgressPool`s (each with cumulative `Xp` and a derived `Level`). Level derivation SHALL be delegated to an injected `ILevelCurve`, not hardcoded by the engine.

**Verified by:** `Aetherium.Test.Progression.ProgressPoolsTests.AddXp_CreatesPool_AndAccumulates`, `.AddXp_RecomputesLevel_ViaInjectedCurve`, `.MultiplePools_AreIndependent`, `.LinearLevelCurve_ZeroXp_IsLevelOne`, `.CustomLevelCurve_IsHonored_NotHardcoded`

#### Scenario: XP accumulates across multiple awards to the same pool
- **WHEN** `30` then `40` XP is added to a pool named `combat_xp`
- **THEN** the pool's cumulative `Xp` is `70`

#### Scenario: Level is derived via the injected curve, not a hardcoded formula
- **WHEN** a pool's XP is set via a campaign-supplied `ILevelCurve` that always returns level `42`
- **THEN** the pool's `Level` is `42`, regardless of the engine's own default `LinearLevelCurve`

#### Scenario: Independent pools do not affect each other
- **WHEN** `combat_xp` receives far more XP than `exploration_xp`
- **THEN** `combat_xp`'s `Level` exceeds `exploration_xp`'s `Level`, and each pool's `Xp` reflects only what was added to it

### Requirement: Skill Prerequisite Gating
The system SHALL provide `SkillUnlockService.TryUnlock`, which unlocks a skill against a `SkillCatalog` only when the skill exists, is not already unlocked, and every id in its `Prerequisites` list is already present in the actor's `UnlockedSkills`. This SHALL support flat point-buy (no prerequisites), a linear tree (one prerequisite), or a web (multiple prerequisites) without any engine-level distinction between those shapes.

**Verified by:** `Aetherium.Test.Progression.SkillUnlockServiceTests.TryUnlock_RootSkill_NoPrerequisites_Succeeds`, `.TryUnlock_UnknownSkill_Fails`, `.TryUnlock_AlreadyUnlocked_Fails`, `.TryUnlock_MissingPrerequisite_Fails`, `.TryUnlock_PrerequisiteMet_Succeeds`, `.TryUnlock_MultiplePrerequisites_AllMustBeMet`

#### Scenario: A root skill with no prerequisites unlocks unconditionally
- **WHEN** `TryUnlock` is called for a skill with an empty `Prerequisites` list, not yet unlocked
- **THEN** it unlocks and returns `Unlocked`

#### Scenario: A skill with an unmet prerequisite is blocked
- **WHEN** `TryUnlock` is called for a skill whose `Prerequisites` include an id not yet in `UnlockedSkills`
- **THEN** it returns `PrerequisitesNotMet` and does not unlock the skill

#### Scenario: A skill requiring multiple prerequisites needs all of them
- **WHEN** a skill's `Prerequisites` list has two ids and only one is unlocked
- **THEN** `TryUnlock` returns `PrerequisitesNotMet`; once both are unlocked, it returns `Unlocked`

### Requirement: Role Affinity
The system SHALL provide an optional `RoleAffinity` component mapping role tags to weights, defaulting to zero for any unset tag, supporting both a freeform build (no weights set) and a fixed archetype (a dominant weight on one tag).

**Verified by:** `Aetherium.Test.Progression.AttributesAndRoleAffinityTests.RoleAffinity_UnsetTag_ReturnsDefaultValue`, `.RoleAffinity_FreeformBuild_HasNoWeights`, `.RoleAffinity_FixedArchetype_HasDominantWeight`

#### Scenario: An unset role tag defaults to zero weight
- **WHEN** `RoleAffinity.Get("tank")` is called and `tank` was never set
- **THEN** it returns `0`

#### Scenario: A fixed archetype has a dominant weight
- **WHEN** `tank` is set to `0.9` and `healer` to `0.1`
- **THEN** `Get("tank")` exceeds `Get("healer")`
