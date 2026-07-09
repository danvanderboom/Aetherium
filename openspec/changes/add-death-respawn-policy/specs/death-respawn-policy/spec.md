## ADDED Requirements

### Requirement: Death Policy Schema
The system SHALL provide a `DeathPolicy` data class covering, at minimum, `Permadeath`, `CorpseRetentionTicks`, `DropOnDeath`, `RespawnPoint`, `XpLossPolicy`/`XpLossAmount`, `DownStateEnabled`, and `ReviveWindowTicks`, plus a `Default` preset that reproduces the engine's pre-policy shipped behavior (a down state, a 3-tick revive window, corpses retained forever).

**Verified by:** `Aetherium.Test.Combat.DeathPolicyTests.Default_ReproducesShippedBehavior`

#### Scenario: Default preset matches pre-policy shipped behavior
- **WHEN** `DeathPolicy.Default` is constructed
- **THEN** `DownStateEnabled` is `true`, `ReviveWindowTicks` is `3`, and `CorpseRetentionTicks` is unbounded (corpses are never expired by policy alone)

### Requirement: Down-State Duration Resolution
`DeathPolicy.ResolveDyingTicks()` SHALL return `ReviveWindowTicks` when `DownStateEnabled` is `true`, and `0` (an instant transition, no down state) when `DownStateEnabled` is `false`, regardless of `ReviveWindowTicks`'s value.

**Verified by:** `Aetherium.Test.Combat.DeathPolicyTests.ResolveDyingTicks_DownStateEnabled_ReturnsReviveWindow`, `.ResolveDyingTicks_DownStateDisabled_ReturnsZero`

#### Scenario: Down state enabled returns the revive window
- **WHEN** `DownStateEnabled = true` and `ReviveWindowTicks = 5`
- **THEN** `ResolveDyingTicks()` returns `5`

#### Scenario: Down state disabled returns zero regardless of revive window
- **WHEN** `DownStateEnabled = false` and `ReviveWindowTicks = 5`
- **THEN** `ResolveDyingTicks()` returns `0`

### Requirement: Corpse Expiry
An entity carrying both `Corpse` and an opt-in `CorpseAge` component SHALL be removed from the world once `CorpseAge.Ticks` reaches a policy's `CorpseRetentionTicks` threshold. A `Corpse` entity with no `CorpseAge` attached SHALL NOT be removed by this system, regardless of policy — preserving the engine's pre-policy behavior of retaining every corpse forever as the backward-compatible default.

**Verified by:** `Aetherium.Test.Combat.CorpseExpirySystemTests.Tick_CorpseWithAge_BelowThreshold_AgesButIsNotRemoved`, `.Tick_CorpseWithAge_ReachesThreshold_IsRemoved`, `.Tick_CorpseWithoutAge_IsNeverRemoved_RegardlessOfPolicy`, `.Tick_EntityWithoutCorpse_IsIgnored`

#### Scenario: Corpse ages but is not yet removed below threshold
- **WHEN** a `Corpse` entity with `CorpseAge.Ticks = 0` ticks once under a policy with `CorpseRetentionTicks = 3`
- **THEN** the entity remains in the world with `CorpseAge.Ticks = 1`

#### Scenario: Corpse is removed once age reaches the retention threshold
- **WHEN** a `Corpse` entity's `CorpseAge.Ticks` reaches a policy's `CorpseRetentionTicks`
- **THEN** the entity is removed from the world

#### Scenario: A corpse with no CorpseAge is never removed
- **WHEN** a `Corpse` entity has no `CorpseAge` component, and the system ticks repeatedly under any policy
- **THEN** the entity is never removed
