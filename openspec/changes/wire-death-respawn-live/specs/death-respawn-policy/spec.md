## MODIFIED Requirements

### Requirement: Death Policy Schema
The system SHALL provide a `DeathPolicy` data class covering, at minimum, `Permadeath`, `CorpseRetentionTicks`, `DropOnDeath`, `RespawnLocation` (a `RespawnLocationPolicy` selecting among `DeathLocation`, `EntryLocation`, `WorldSpawn`, `NamedLocation`, `FixedCoordinates`, `OffsetFromCoordinates`, `OffsetFromNamedLocation`, `LastSafeLocation`, and `PartyLeader` modes, with coordinate/offset/tag parameters), `XpLossPolicy`/`XpLossAmount`, `DownStateEnabled`, `ReviveWindowTicks`, `RespawnInvulnerabilityTicks`, and `PermadeathBehavior` (a `PermadeathSessionPolicy` of `Spectate` or `Disconnect`), plus a `Default` preset that reproduces the engine's pre-policy shipped behavior (a down state, a 3-tick revive window, corpses retained forever, world-spawn respawn, a 3-tick respawn-invulnerability window, and a spectate permadeath default). The schema SHALL live in `Aetherium.Model` (not `Aetherium.Server`) so it is reachable from both a world's runtime configuration and its creation contract.

**Verified by:** `Aetherium.Test.Combat.DeathPolicyTests.Default_ReproducesShippedBehavior`, `.RespawnLocationPolicy_WorldSpawnDefault_HasNoStrayCoordinates`

#### Scenario: Default preset matches pre-policy shipped behavior
- **WHEN** `DeathPolicy.Default` is constructed
- **THEN** `DownStateEnabled` is `true`, `ReviveWindowTicks` is `3`, `CorpseRetentionTicks` is unbounded, `RespawnLocation.Mode` is `WorldSpawn`, `RespawnInvulnerabilityTicks` is `3`, and `PermadeathBehavior` is `Spectate`

#### Scenario: A respawn-location mode carries only the parameters it needs
- **WHEN** a `RespawnLocationPolicy` is constructed with `Mode = WorldSpawn`
- **THEN** its `LocationTag`, coordinate, and offset fields are unset/zero — modes that don't consume a parameter simply ignore it, they aren't required to be absent

### Requirement: Down-State Duration Resolution
`DeathPolicy.ResolveDyingTicks()` SHALL return `ReviveWindowTicks` when `DownStateEnabled` is `true`, and `0` (an instant transition, no down state) when `DownStateEnabled` is `false`, regardless of `ReviveWindowTicks`'s value.

**Verified by:** `Aetherium.Test.Combat.DeathPolicyTests.ResolveDyingTicks_DownStateEnabled_ReturnsReviveWindow`, `.ResolveDyingTicks_DownStateDisabled_ReturnsZero`

#### Scenario: Down state enabled returns the revive window
- **WHEN** `DownStateEnabled = true` and `ReviveWindowTicks = 5`
- **THEN** `ResolveDyingTicks()` returns `5`

#### Scenario: Down state disabled returns zero regardless of revive window
- **WHEN** `DownStateEnabled = false` and `ReviveWindowTicks = 5`
- **THEN** `ResolveDyingTicks()` returns `0`

## ADDED Requirements

### Requirement: Per-World Death Policy
A world's `DeathPolicy` SHALL be specifiable at world-creation time (via `WorldConfig.DeathPolicy` or `WorldTemplate.DeathPolicy`/`CreateWorldRequest.DeathPolicy`), persisted per-world, and applied to every map the world creates — both the initial map created during world initialization and any map added later. A map created on a world with no specified policy SHALL use `DeathPolicy.Default`. A map's active policy SHALL survive grain reactivation without re-running its initialization.

**Verified by:** `Aetherium.Test.WorldGrainTests.WorldGrain_DeathPolicy_PropagatesToEveryMapItCreates`, `.WorldGrain_NoDeathPolicySpecified_MapFallsBackToDefault`, `Aetherium.Test.GameManagementGrainTests.GameManagement_CreateWorldRequest_DeathPolicy_ReachesTheCreatedMap`

#### Scenario: A world's death policy reaches every map it creates
- **WHEN** a world is initialized with a custom `DeathPolicy` and creates both an initial map and a later map via `AddMapAsync`
- **THEN** both maps' active `DeathPolicy` (queryable via `IGameMapGrain.GetDeathPolicyAsync`) matches the world's configured policy

#### Scenario: No policy specified falls back to Default
- **WHEN** a world is initialized with `WorldConfig.DeathPolicy` left null
- **THEN** every map it creates reports `DeathPolicy.Default` as its active policy

#### Scenario: A CreateWorldRequest's death policy reaches the created map
- **WHEN** `GameManagementGrain.CreateWorldAsync` is called with a `CreateWorldRequest.DeathPolicy` set
- **THEN** the created world's map(s) report that policy as active
