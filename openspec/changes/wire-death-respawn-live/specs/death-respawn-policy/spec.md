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

### Requirement: Player Death Outcomes
A player reduced to `0` HP by a monster's attack SHALL have the active `DeathPolicy`'s outcome applied, resolved as `Permadeath × DownStateEnabled`: `(false, false)` respawns the player immediately; `(true, false)` transitions the player to a permanent `Corpse` state immediately; either combination with `DownStateEnabled = true` first places the player in a `Downed` state for `ReviveWindowTicks`, after which the same `Permadeath` flag decides between respawning and becoming a `Corpse`. A respawn SHALL restore the player's `Health` to its maximum, reuse the player's existing entity id, resolve the destination per `RespawnLocation`, and — when `RespawnInvulnerabilityTicks` is greater than zero — leave the player untargetable by monsters for that many ticks.

**Verified by:** `Aetherium.Test.Combat.PlayerDeathResolverTests` (pure outcome-table coverage), `Aetherium.Test.Combat.PlayerDeathAndRespawnTests.InstantRespawn_NoDownState_NoPermadeath_PlayerRespawnsImmediately_AndCanActRightAway`, `.DownThenRespawn_Default_PlayerIsFrozenDuringTheDownWindow_ThenRespawns`, `.InstantPermadeath_NoDownState_Permadeath_PlayerNeverRespawns`, `.DownThenPermadeath_PlayerFrozenDuringDownWindow_ThenStaysFrozenAsCorpse`, `.RespawnInvulnerability_ProtectsAFreshRespawn_FromImmediateRedowning`, `.RespawnLocation_FixedCoordinates_TeleportsToTheConfiguredLocation`

#### Scenario: No down state, no permadeath — instant respawn
- **WHEN** a player's Health reaches `0` under a policy with `Permadeath = false` and `DownStateEnabled = false`
- **THEN** the same tick restores the player's Health to maximum and resolves their respawn location — no `Downed` state is ever observed

#### Scenario: No down state, permadeath — instant Corpse
- **WHEN** a player's Health reaches `0` under a policy with `Permadeath = true` and `DownStateEnabled = false`
- **THEN** the player immediately becomes a `Corpse` and never respawns

#### Scenario: Down state enabled — frozen for the revive window, then resolved by Permadeath
- **WHEN** a player's Health reaches `0` under a policy with `DownStateEnabled = true`
- **THEN** the player enters `Downed` for `ReviveWindowTicks`, rejecting all commands throughout; once the countdown elapses, the player respawns if `Permadeath = false` or becomes a `Corpse` if `Permadeath = true`

#### Scenario: Respawn invulnerability protects against immediate re-downing
- **WHEN** a player respawns under a policy with `RespawnInvulnerabilityTicks` greater than zero, and a monster remains adjacent
- **THEN** the player's Health is not reduced by that monster until the invulnerability window elapses

### Requirement: Downed Action Gating
Every player command (`MoveAsync`, `AttackAsync`, `RotateAsync`, `ChangeLevelAsync`, `PickupAsync`, `DropAsync`, `UseAsync`, `OpenAsync`, `CloseAsync`) SHALL reject a player carrying `Downed` or `Corpse` with a clear failure reason, without mutating world state.

**Verified by:** `Aetherium.Test.Combat.PlayerDeathAndRespawnTests.DownThenRespawn_Default_PlayerIsFrozenDuringTheDownWindow_ThenRespawns`, `.InstantPermadeath_NoDownState_Permadeath_PlayerNeverRespawns`, `.DownThenPermadeath_PlayerFrozenDuringDownWindow_ThenStaysFrozenAsCorpse`

#### Scenario: A downed player's move is rejected
- **WHEN** `MoveAsync` is called for a player carrying `Downed`
- **THEN** the call fails with a "you are downed" reason and the player's location is unchanged

### Requirement: Player Vitals Wire Surface
The engine SHALL provide a player-scoped signal path, independent of `PerceptionDto`, that notifies a player's own session of `Downed`/respawn/permadeath transitions via a `PlayerVitalsDto` payload (`Health`, `MaxHealth`, `IsDowned`, `DownedTicksRemaining`, `IsInvulnerable`) delivered through named hub events (`ReceiveDowned`, `ReceiveRespawn`, `ReceiveDied`).

**Verified by:** `Aetherium.Test.Combat.PlayerVitalsWireSurfaceTests.DownedTransition_DispatchesReceiveDowned_WithMatchingVitals_ToOnlyThatPlayersConnection`, `.RespawnTransition_DispatchesReceiveRespawn_WithFullHealthVitals`

#### Scenario: A death/respawn transition notifies only the affected player
- **WHEN** a player's lethal hit or Downed-countdown expiry resolves
- **THEN** exactly that player's session receives the corresponding `ReceiveDowned`/`ReceiveRespawn`/`ReceiveDied` event with a `PlayerVitalsDto` payload — no other session is notified, and no `MapDelta`/perception push is triggered by this signal itself
