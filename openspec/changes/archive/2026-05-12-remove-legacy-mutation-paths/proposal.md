# Remove Legacy Mutation Paths (Phase 2d)

## Why

After `add-grain-authoritative-mutation` (phase 2b+c) ships, grain-bound sessions route every mutation through `IMapMutationGateway` → `GrainMutationGateway` → `IGameMapGrain.*Async`. The legacy `[Obsolete]` hub methods (`MovePlayer`, `RotatePlayer`, `Pickup`, `Drop`, `Use`, `Open`, `Close`, `ChangeLevel`, `JumpToRandomLocation`, `SetLightingMode`, `SetVisionMode`, `ToggleDirectionalVision`) on `GameHub` still exist, marked `[Obsolete("Use ExecuteTool(...)")]`. They're dead surface area in production but cost maintenance — every behavior change has to be made twice.

This change removes the legacy hub methods and consolidates ancillary cleanups that depend on phase 2b+c having landed: deleting the stateful `InteractionSystem` (replaced by the stateless API + grain delegation), removing the session's mutation entry points that tools shouldn't call, and trimming related doc surfaces.

The legacy `OnConnectedAsync` private-world path (the `FovDiagnosticWorldBuilder` fallback when no `?worldId=` is supplied) is **preserved** — it's still load-bearing for the 700+ existing tests. That path remains until those tests migrate to a TestingHost-backed pattern in a separate later change.

## What Changes

- Remove all `[Obsolete]`-marked methods on `GameHub`: `MovePlayer`, `RotatePlayer`, `RotatePlayerDegrees`, `ToggleDirectionalVision`, `ChangeLevel`, `JumpToRandomLocation`, `Pickup`, `Drop`, `Use`, `Open`, `Close`, `SetLightingMode`, `SetVisionMode`.
- Delete the stateful `Aetherium.Server.InteractionSystem` class. Its operations now live as static methods (or a stateless instance) callable from both `LocalMutationGateway` and `GameMapGrain`. The phase 2b+c refactor moved the implementations; this phase removes the original.
- Remove `GameSession.MoveView`/`RotateView`/`RotateView(int)`/`ChangeLevel`/`JumpToRandomLocation` from the public surface. Gameplay code SHALL use `IMapMutationGateway` exclusively. The internal `_stateLock`-protected logic moves into `LocalMutationGateway` (which already calls it through the gateway abstraction since phase 2a).
- Remove `ToolExecutionContext.InteractionSystem` field. The gateway is the only mutation entry point.
- Update `CLIENT_SERVER_README.md` controls section: list `ExecuteTool` invocations rather than the obsolete hub methods.
- **Preserved**: the `FovDiagnosticWorldBuilder` fallback in `OnConnectedAsync`. Tests still rely on it.
- **Preserved**: `LocalMutationGateway`. Tests and legacy/dev mode still need a non-grain path.
- **Preserved**: `GameSession`'s `_stateLock`. It guards reconciliation in `ApplyDelta` and any session-local property access.

## Impact

- Affected specs:
  - `client-server-communication` —
    - REMOVED `Identical Gameplay Experience` (legacy reference to "arrow keys, Z/X rotation, U/D level change" via direct hub methods is no longer accurate; the same controls work via `ExecuteTool` and are covered by `Command Processing` after phase 2a)
    - MODIFIED `Interaction Commands` (drop references to deprecated `Pickup`/`Drop`/`Use`/`Open`/`Close` hub methods; describe in terms of `ExecuteTool` only)
- Affected code:
  - `Aetherium.Server/GameHub.cs` — delete ~13 obsolete methods (~250 lines)
  - `Aetherium.Server/InteractionSystem.cs` — delete (logic now lives in stateless helpers used by both gateways)
  - `Aetherium.Server/GameSession.cs` — make `MoveView`/`RotateView`/`ChangeLevel`/`JumpToRandomLocation` internal or move into `LocalMutationGateway`
  - `Aetherium.Server/Agents/Tools/ToolExecutionContext.cs` — remove `InteractionSystem` field
  - `CLIENT_SERVER_README.md` — refresh control-list section
- Tests:
  - `InteractionSystemTests` — already refactored in phase 2b+c to use the stateless API; this change confirms nothing reaches the old class shape
  - Existing tests that constructed sessions directly and called `session.MoveView` need to switch to `gateway.MoveAsync`. Estimated 20-40 test methods to update — mechanical, not semantic
  - All updates are mechanical search-and-replace; no test logic changes
- Sequencing: must ship *after* `add-grain-authoritative-mutation`. Optional: bundle into the same PR if review capacity allows.
