## ADDED Requirements

### Requirement: Player-Facing Game Catalog
The game hub SHALL let a connected player enumerate the server's registered game definitions and
inspect a single game's live occupancy, without operator credentials. This is the player-facing
counterpart to the operator-only `GET /api/management/games` surface: the same registry, reachable over
`GameHub` under its adaptive auth policy, with no API key.

**Verified by:** `Aetherium.Test.Games.ArcadeCatalogTests.ListGames_ReturnsAllRegisteredDefinitions`, `Aetherium.Test.Games.ArcadeCatalogTests.GetGameDetails_ReportsLiveOccupancy`

#### Scenario: Listing games over the hub
- **WHEN** a connected client calls `GameHub.ListGames()`
- **THEN** the server returns a `GameDefinitionSummaryDto` for every registered definition (id, name,
  version, description, tags, topology, max players, presentation hints)
- **AND** no API key or operator authorization is required

#### Scenario: Game details include live occupancy
- **WHEN** a client calls `GameHub.GetGameDetails(gameId)` for a registered game
- **THEN** the result carries the game's summary plus its live occupancy — count of running instances,
  count of joinable instances (below capacity), and total players currently in-game
- **AND** an unknown `gameId` yields a not-found result rather than throwing

### Requirement: One-Call Game Entry
The game hub SHALL provide a single verb, `PlayGame(gameId, options?)`, that places the calling player
into a running instance of the named game — joining an existing joinable instance when one exists, or
creating one on demand when the game's instancing policy permits — and then binds the caller's session
to that instance through the existing `JoinWorld` flow. The player never handles world or map ids.

**Verified by:** `Aetherium.Test.Games.PlayGameLifecycleTests.PlayGame_JoinsExistingSharedInstance`, `Aetherium.Test.Games.PlayGameLifecycleTests.PlayGame_CreatesWhenPolicyPermits`, `Aetherium.Test.Games.PlayGameLifecycleTests.PlayGame_RefusedWhenEntryDisabled`, `Aetherium.Client.Tests.InProcServerIntegrationTests.PlayGame_RoundTripsThroughLiveServer`

#### Scenario: Joining an existing joinable instance
- **WHEN** a player calls `GameHub.PlayGame(gameId)` and an `Active` instance of that game exists with
  player headroom under a `shared` entry policy
- **THEN** the player is bound to that instance via the existing `JoinWorld` grain-binding flow
- **AND** the result is a `JoinWorldResult` reporting the joined world id, map id, and spawn

#### Scenario: Creating an instance on demand
- **WHEN** a player calls `PlayGame(gameId)`, no joinable instance exists, and the game's policy allows
  player instantiation
- **THEN** the server creates a new instance from the definition (the existing `CreateGameInstanceAsync`
  mapper path), waits for it to become `Active`, and binds the caller to it
- **AND** the result reports the newly created world/map

#### Scenario: Entry refused by policy
- **WHEN** a player calls `PlayGame(gameId)` for a game whose policy is `disabled`, or whose policy
  forbids player instantiation and offers no joinable instance
- **THEN** the call returns a failed `JoinWorldResult` with a human-readable reason and creates nothing

### Requirement: Server Capability Handshake
The game hub SHALL expose `GetServerInfo()` returning the server version and a list of protocol
capability strings, so a generalized client meeting an unknown server can feature-gate on advertised
capabilities rather than on version arithmetic. The capability list is append-only across releases.

**Verified by:** `Aetherium.Test.Games.ArcadeCatalogTests.GetServerInfo_AdvertisesCapabilitySet`

#### Scenario: Client discovers server capabilities
- **WHEN** a client calls `GameHub.GetServerInfo()`
- **THEN** the result carries a server version string and a set of capability tokens (for example
  `"topology"`, `"interoception"`, `"arcade"`)
- **AND** a client may enable or hide features by testing token membership, treating an absent token as
  "not supported" without failing the connection
