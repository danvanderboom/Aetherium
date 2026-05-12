# Add End-to-End Shared Mutation Tests

## Why

Phase 1 and phase 2c both deferred an end-to-end test that boots the real `Aetherium.Server` host (`WebApplicationFactory<Program>`) with Orleans co-hosted, opens two `HubConnection`s against the same world, and verifies that a mutation through one client surfaces as a `ReceivePerceptionUpdate` on the other. The proof points (snapshot consistency, delta application, gateway routing) are individually covered by `GameMapGrainJoinTests`, `GameMapGrainMutationTests`, `DeltaApplicationTests`, and `SessionHeatMirrorTests` — but no test exercises the full wire path: grain mutation → `GameSessionManager.NotifyMapMutationAsync` → `session.ApplyDelta` → `Clients.Client(connectionId).SendAsync("ReceivePerceptionUpdate", ...)` → second client's handler.

This change adds that test. It's the highest-leverage validation we can write — the only way to catch regressions in the layered protocol (Orleans serialization of grain method arguments, SignalR JSON of `PerceptionDto`, host-side broker correctness, session-Gateway binding) as a single assertion. It also pays down the deferred-test debt referenced from `add-hub-grain-snapshot-bridge` (phase 1) and `add-grain-authoritative-mutation` (phase 2c).

## What Changes

- New test fixture `EndToEndSharedMutationTests` in `Aetherium.Test/MultiWorld/`. Uses `WebApplicationFactory<Aetherium.Server.Program>` with Orleans enabled (the env var `DISABLE_ORLEANS` is intentionally NOT set; the test goes through the real co-hosted silo path).
- Per-test setup creates a world via the management grain (`IGameManagementGrain.CreateWorldAsync`), then opens two `HubConnection`s with `?worldId=<created-id>` and waits for both to receive their initial perception.
- Test cases:
  - `Two_Clients_See_Each_Other_After_Join` — both clients receive a perception that includes the other player's `Character` entity
  - `Player_Move_Propagates_To_Other_Client` — client A invokes `ExecuteTool("move", ...)`; client B receives a `ReceivePerceptionUpdate` reflecting A's new position within the test timeout
  - `Client_Disconnect_Removes_Player_From_Other_Clients_View` — client A disconnects; client B receives a perception update without A's Character
- Test infrastructure helpers: a small `WaitForPerceptionAsync(predicate, timeout)` helper that subscribes to `ReceivePerceptionUpdate` on a `HubConnection` and resolves when a perception matches the predicate. Bounded wait with assertion on timeout.

## Impact

- Affected specs:
  - `client-server-communication` — ADDED `End-to-End Multiplayer Validation` requirement. Captures the contract that "two clients in the same map see each other's actions" is a tested invariant, not just an aspiration
- Affected code:
  - New file: `Aetherium.Test/MultiWorld/EndToEndSharedMutationTests.cs`
  - Possibly: a small `Aetherium.Test/MultiWorld/SignalRTestHelpers.cs` for the wait-for-perception helper
- Affected docs:
  - `CLIENT_SERVER_README.md` — the existing "Wire protocol notes" section can reference the new test as the canonical "this is what the wire actually does" example
- Test counts:
  - +3 NUnit test cases. Each runs in ~1-3 seconds (Orleans cold-start + SignalR handshake dominate). Total ~5-10 seconds added to the suite

## Honest scope notes

- The test boots a full Orleans silo per fixture (`OneTimeSetUp`). That's ~1 second of overhead, amortized across the fixture's tests. Single fixture, sequential tests
- The test uses `factory.Server.CreateHandler()` for the SignalR transport (no real network) — same pattern as the existing `GameHubSmokeTests`
- World creation goes through `IGameManagementGrain.CreateWorldAsync` (the management grain creates both the world grain and its first map grain). This validates the management → world → map grain chain as a side effect
- The test verifies perception-based propagation; it does NOT inspect raw deltas on the wire (because clients shouldn't see them — perception-pure principle). The presence of the perception update IS the signal that the delta was applied server-side
