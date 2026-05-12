# Add Map Mutation Gateway Abstraction (Phase 2a)

## Why

Gameplay mutations currently happen in two unrelated places: `GameSession.MoveView`/`RotateView`/`ChangeLevel` for movement and `InteractionSystem.TryPickup`/`TryDrop`/`TryUse`/`TryOpen`/`TryClose` for entity interactions. Both reach directly into `session.World` and mutate it. Tools call these directly via `context.Session.MoveView(...)` or `context.InteractionSystem.TryPickup(...)`.

Phase 2's goal is to make mutation grain-authoritative — but routing every tool call through a grain method requires the call sites to be unaware of *whether* they're talking to a local session or a remote grain. This change introduces the `IMapMutationGateway` interface as that abstraction. It carries no behavior change: in this phase the only implementation is `LocalMutationGateway`, which wraps the existing `InteractionSystem` and `GameSession` movement methods.

The point is to establish the boundary so phase 2b can swap in a `GrainMutationGateway` without touching every tool.

## What Changes

- New `IMapMutationGateway` interface with one method per current gameplay verb (`MoveAsync`, `RotateAsync`, `ChangeLevelAsync`, `PickupAsync`, `DropAsync`, `UseAsync`, `OpenAsync`, `CloseAsync`).
- New `LocalMutationGateway` implementation that delegates to today's `GameSession` and `InteractionSystem` methods — behavior identical to the status quo.
- `ToolExecutionContext` carries an `IMapMutationGateway` instance. Tools call `context.MutationGateway.MoveAsync(...)` instead of `context.Session.MoveView(...)` directly.
- All gameplay tools under `Aetherium.Server/Agents/Tools/Movement/` and `Aetherium.Server/Agents/Tools/Interaction/` refactor to use the gateway.
- `GameHub.ExecuteTool` and `GameMapGrain` (when phase 2b lands) become the two places that construct gateway instances.
- **Not breaking**: all existing tests pass unchanged. The `GameSession.MoveView`/`InteractionSystem.TryPickup` etc. methods remain callable for any code that hasn't migrated; only tool code is refactored in this phase.

## Impact

- Affected specs:
  - `client-server-communication` — ADDED `Map Mutation Gateway Abstraction`; MODIFIED `Command Processing` (commands now flow through the gateway rather than touching session state directly)
- Affected code:
  - New: `Aetherium.Server/MultiWorld/IMapMutationGateway.cs`, `Aetherium.Server/MultiWorld/LocalMutationGateway.cs`
  - `Aetherium.Server/Agents/Tools/ToolExecutionContext.cs` — add `MutationGateway` property
  - `Aetherium.Server/GameHub.cs` — construct a `LocalMutationGateway` per `ExecuteTool` call
  - `Aetherium.Server/Agents/Tools/Movement/*.cs` (~4 tools) — route through gateway
  - `Aetherium.Server/Agents/Tools/Interaction/*.cs` (~5 tools) — route through gateway
- **Non-breaking**: zero test changes expected. 714 existing tests continue to pass.
- Sets up phase 2b/2c (`add-grain-authoritative-mutation`) by establishing the interface tools depend on.
