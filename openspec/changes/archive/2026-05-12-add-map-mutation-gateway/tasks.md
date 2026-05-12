# Implementation Tasks

## 1. Interface and local implementation
- [x] 1.1 Created `Aetherium.Server/MultiWorld/IMapMutationGateway.cs` with `MoveAsync`, `RotateAsync`, `ChangeLevelAsync`, `PickupAsync`, `DropAsync`, `UseAsync`, `OpenAsync`, `CloseAsync` returning typed result DTOs (`MoveResult`, `RotateResult`, `ChangeLevelResult`, `InteractionResultDto`)
- [x] 1.2 Result DTOs (`MoveResult`, `RotateResult`, `ChangeLevelResult`) live in the interface file; all `[GenerateSerializer]` with stable `[Id]` ordinals so they can later cross grain boundaries
- [x] 1.3 Created `Aetherium.Server/MultiWorld/LocalMutationGateway.cs`; methods delegate to `GameSession.MoveView`/`RotateView`/`ChangeLevel` and `InteractionSystem.Try*` exactly as before; an internal `InteractionResult → InteractionResultDto` translator preserves the reactive-disambiguation `Options` shape
- [x] 1.4 `LocalMutationGateway` constructor takes a `GameSession` and an optional `InteractionSystem` (defaults to a fresh stateless instance)

## 2. ToolExecutionContext wiring
- [x] 2.1 Added `IMapMutationGateway? MutationGateway { get; init; }` to `ToolExecutionContext`
- [x] 2.2 `GameHub.ExecuteTool` constructs `new LocalMutationGateway(session, interactionSystem)` and passes it to the context. Existing fields (`Session`, `InteractionSystem`) are preserved for backward compat
- [x] 2.3 **Auto-fallback added during implementation** (not in original plan): when `MutationGateway` isn't explicitly set but `Session` is, the property getter constructs a `LocalMutationGateway` on demand. This keeps the ~30 existing test setups that build `ToolExecutionContext` directly working unmodified — they pass `Session` (and sometimes `InteractionSystem`) and get a working gateway for free. The fallback is cached after first read
- [x] 2.4 `WorldBuildingToolContext` is unaffected (world-building tools don't mutate gameplay state)

## 3. Refactor movement tools
- [x] 3.1 `MoveTool` calls `context.MutationGateway.MoveAsync(direction, distance)` for the in-process path; `context.ManagementGrain.MoveAsync` path preserved for agent runners that operate via `IGameManagementGrain`
- [x] 3.2 `RotateTool` calls `context.MutationGateway.RotateAsync(degrees)`
- [x] 3.3 `ChangeLevelTool` calls `context.MutationGateway.ChangeLevelAsync(deltaZ)`
- [ ] 3.4 `JumpToLocationTool` left on the `context.Session.JumpToRandomLocation` path — deliberately unchanged because there's no grain equivalent in phase 2a; phase 2c will revisit when the grain owns randomness-of-placement

## 4. Refactor interaction tools
- [x] 4.1 `PickupTool` routes through `context.MutationGateway.PickupAsync`
- [x] 4.2 `DropTool` routes through `context.MutationGateway.DropAsync`
- [x] 4.3 `UseTool` routes through `context.MutationGateway.UseAsync`, preserving the reactive-disambiguation `Options` payload when present
- [x] 4.4 `OpenTool` routes through `context.MutationGateway.OpenAsync`
- [x] 4.5 `CloseTool` routes through `context.MutationGateway.CloseAsync`

## 5. Tests
- [x] 5.1 `LocalMutationGatewayTests` (xUnit, 8 tests): pickup, drop, move, rotate, change-level, use-failure-shape, move-without-view-location, plus the legacy direct-path equivalence checks. All passing
- [x] 5.2 `ToolExecutionContext` MutationGateway auto-fallback exercised implicitly by every existing tool-integration test that constructs a context with `Session` set
- [x] 5.3 Updated one previously-existing test: `PickupToolTests.ExecuteAsync_ShouldFailWithoutInteractionSystem` was asserting on a now-obsolete gating dependency (`InteractionSystem == null`). Renamed and tightened to `ExecuteAsync_ShouldFailWithoutSessionOrGateway` — the actual phase-2a gating dependency is Session-OR-explicit-gateway, not InteractionSystem

## 6. Validation
- [x] 6.1 All 714 pre-phase-2a tests pass plus 8 new gateway tests — total **722 passed, 0 failed, 2 skipped**
- [x] 6.2 New unit tests pass (8/8 in `LocalMutationGatewayTests`)
- [x] 6.3 `dotnet build` clean — no new warnings beyond pre-existing baseline
- [x] 6.4 Audit confirmed: every tool under `Aetherium.Server/Agents/Tools/Movement/` and `Aetherium.Server/Agents/Tools/Interaction/` routes mutations through `context.MutationGateway`. The legacy `context.InteractionSystem.Try*` direct-call paths have been removed from tool code. `JumpToLocationTool` retains the `context.Session.JumpToRandomLocation` path (documented in task 3.4)
