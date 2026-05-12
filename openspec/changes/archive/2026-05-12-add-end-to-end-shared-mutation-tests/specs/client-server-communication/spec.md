## ADDED Requirements

### Requirement: End-to-End Multiplayer Validation
The test suite SHALL include at least one end-to-end test that exercises the full grain mutation → host-side broker → per-session perception dispatch chain against a real Orleans cluster, using either an Orleans `TestCluster` with a captured `IHubContext` substitute, or `WebApplicationFactory<Aetherium.Server.Program>` with co-hosted Orleans. The test SHALL verify that a gameplay mutation invoked on one session's behalf produces a `ReceivePerceptionUpdate` dispatch for every other session bound to the same map.

#### Scenario: Two sessions in the same world see each other join
- **WHEN** two sessions join the same map via `IGameMapGrain.JoinPlayerAsync`
- **THEN** each session's hydrated `World` SHALL contain the other's `Character` entity
- **AND** the host-side delta broker SHALL have been invoked with an `EntityAddedDelta` for each joiner

#### Scenario: Mutation propagates to host-side dispatch
- **WHEN** one session invokes a gameplay verb through `IMapMutationGateway` (or directly through `IGameMapGrain` in a test)
- **THEN** the grain SHALL apply the mutation to `_world`
- **AND** the host-side `GameSessionManager.NotifyMapMutationAsync` SHALL invoke `IHubContext.Clients.Client(connectionId).SendAsync("ReceivePerceptionUpdate", ...)` for every joined session in the map

#### Scenario: Leave removes player visibility from other sessions
- **WHEN** a session's `IGameMapGrain.LeavePlayerAsync` is invoked
- **THEN** the grain SHALL remove the player Character from `_world`
- **AND** SHALL emit an `EntityRemovedDelta` for that player
- **AND** the remaining session(s) SHALL receive a fresh `ReceivePerceptionUpdate` whose perception no longer references the departed Character
