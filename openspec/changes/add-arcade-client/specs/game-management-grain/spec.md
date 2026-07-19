## ADDED Requirements

### Requirement: Player-Initiated Instance Lifecycle
The game-management grain SHALL resolve a player's `PlayGame` request into a concrete world instance by
applying the game definition's instancing policy, and instance creation on the player's behalf SHALL be
governed entirely by that per-bundle data — not by client input or hard-coded server behavior. The
policy fields are `playerEntry` (`shared` | `private` | `disabled`), `allowPlayerInstances`,
`maxInstances`, and `idleShutdownMinutes`. When a bundle omits the `instancing:` section, the defaults
are `shared` / `allowPlayerInstances: false` / `maxInstances: 1` — i.e. players may join operator-created
instances but never cause creation, preserving today's behavior.

**Verified by:** `Aetherium.Test.Games.PlayGameLifecycleTests.SharedPolicy_ReusesInstanceBelowCapacity`, `Aetherium.Test.Games.PlayGameLifecycleTests.SharedPolicy_CreatesUpToMaxInstancesThenReportsFull`, `Aetherium.Test.Games.PlayGameLifecycleTests.PrivatePolicy_AlwaysCreates`, `Aetherium.Test.Games.PlayGameLifecycleTests.DisabledPolicy_NeverCreatesForPlayers`

#### Scenario: Shared policy reuses an instance below capacity
- **WHEN** the grain resolves `PlayGame` for a `shared` game and an `Active` instance exists with
  joined players fewer than the definition's `maxPlayers`
- **THEN** the grain returns that instance for the caller to join, creating nothing

#### Scenario: Shared policy creates up to the instance cap
- **WHEN** the grain resolves `PlayGame` for a `shared` game whose only instances are all at capacity,
  `allowPlayerInstances` is true, and fewer than `maxInstances` instances exist
- **THEN** the grain creates a new instance from the definition and returns it
- **AND** once `maxInstances` instances exist and all are full, the grain reports "full" rather than
  creating another

#### Scenario: Private policy creates a fresh instance per entry
- **WHEN** the grain resolves `PlayGame` for a `private` game (which requires `allowPlayerInstances`)
- **THEN** the grain creates a new instance for the caller regardless of existing instances

#### Scenario: Disabled policy refuses player creation
- **WHEN** the grain resolves `PlayGame` for a `disabled` game
- **THEN** the grain neither reuses nor creates an instance for the player, and reports entry refused
  (operator-created instances remain joinable only through the explicit `ListWorlds`/`JoinWorld` path)

### Requirement: Idle Instance Reaping
Game-instance worlds created on a player's behalf SHALL be reclaimable: an instance with no joined
sessions for longer than its game's `idleShutdownMinutes` SHALL be shut down through the existing world
shutdown path so it stops being ticked and stored, while persisted-state worlds remain recreatable. An
`idleShutdownMinutes` of 0 means never reap. This closes the "worlds ticked forever" exposure that
on-demand creation would otherwise introduce.

**Verified by:** `Aetherium.Test.Games.InstanceReapingTests.IdleInstance_IsReapedAndStopsTicking`, `Aetherium.Test.Games.InstanceReapingTests.OccupiedInstance_IsNotReaped`, `Aetherium.Test.Games.InstanceReapingTests.ZeroIdleMinutes_NeverReaped`, `Aetherium.Test.Games.InstanceReapingTests.ReapedInstance_RecreatableViaPlayGame`

#### Scenario: An idle instance is reaped
- **WHEN** the idle sweep runs and a game-instance world has had zero joined sessions for longer than
  its `idleShutdownMinutes`
- **THEN** that world is shut down via the existing shutdown path and no longer ticked
- **AND** the number of instances reaped is reported

#### Scenario: An occupied or recently-active instance is left alone
- **WHEN** the idle sweep runs and an instance either has joined sessions or last saw player activity
  within its `idleShutdownMinutes` (or its `idleShutdownMinutes` is 0)
- **THEN** that instance is not shut down

#### Scenario: A reaped game remains re-enterable
- **WHEN** every instance of a game has been reaped and a player calls `PlayGame` for it again
- **THEN** the join-or-create resolution creates a fresh instance (subject to the game's policy), so the
  game stays on the shelf and playable after reaping
