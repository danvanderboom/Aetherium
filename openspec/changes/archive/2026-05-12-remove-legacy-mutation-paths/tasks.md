# Implementation Tasks

## 1. Hub surface cleanup
- [x] 1.1 Removed `GameHub.MovePlayer`, `RotatePlayer`, `RotatePlayerDegrees`
- [x] 1.2 Removed `GameHub.ToggleDirectionalVision`, `ChangeLevel`, `JumpToRandomLocation`
- [x] 1.3 Removed `GameHub.Pickup`, `Drop`, `Use`, `Open`, `Close`
- [x] 1.4 Removed `GameHub.SetLightingMode`, `SetVisionMode`
- [x] 1.5 Confirmed no internal callers — only the obsolete methods called each other. The narrative-consequence emission for door/item events that used to live in those methods is preserved in `ExecuteTool`'s post-dispatch branch

## 2. InteractionSystem cleanup
- [ ] 2.1 **Deferred to a separate follow-up change.** Refactoring the 14-method `InteractionSystem` from session-taking signatures to stateless `(World, Character, WorldLocation, ...)` overloads is a substantial scope of its own; doing it here would inflate phase 2d well beyond "cleanup." The cleanup goals (delete obsolete hub methods, remove `ToolExecutionContext.InteractionSystem` field) ship cleanly without it
- [ ] 2.2 — see 2.1. `LocalMutationGateway` continues to use the session-taking `InteractionSystem` API. The grain methods reimplement the gameplay-critical verbs natively (per phase 2c). Real consolidation is the next change's job
- [ ] 2.3 `InteractionResult` and `UseOption` types — unchanged, still live in `Aetherium.Server/InteractionSystem.cs`

## 3. GameSession surface tightening
- [ ] 3.1 **Skipped.** Marking `GameSession.MoveView`/`RotateView`/`ChangeLevel`/`JumpToRandomLocation` `internal` would break the ~70 tests that construct a session directly and exercise those methods. Tightening the access is gold-plating that doesn't pay for itself without the broader test migration. Phase 3 (perception-from-grain) eliminates these methods entirely when the session's local mutation surface goes away
- [ ] 3.2 — see 3.1
- [x] 3.3 `GetPerception` remains public — perception read path unchanged
- [x] 3.4 `ReplaceWorld` remains public — `GameHub.JoinWorld` needs it
- [x] 3.5 Property setters for `CurrentLightingMode`, `CurrentVisionMode`, `DirectionalVisionMode` remain public — vision mode is a session-local concern

## 4. ToolExecutionContext cleanup
- [x] 4.1 Removed `InteractionSystem InteractionSystem { get; init; }` field from `ToolExecutionContext`
- [x] 4.2 `GameHub.ExecuteTool` no longer sets `InteractionSystem` on the context; the `MutationGateway` auto-fallback from `Session` is the single mutation entry point
- [x] 4.3 Audited every tool under `Agents/Tools/Movement/` and `Agents/Tools/Interaction/`: none reference `context.InteractionSystem` after phase 2a's refactor

## 5. Test updates
- [x] 5.1 Removed 16 `InteractionSystem = ...` lines from `Aetherium.Test/Agents/Tools/E2E/AgentToolE2ETests.cs` and `Aetherium.Test/Agents/Tools/Integration/ToolSystemIntegrationTests.cs` via PowerShell pattern replace. All affected tests now construct contexts with only `Session = ...` and rely on the gateway auto-fallback. The same auto-fallback covers `Aetherium.Test/Agents/Tools/PickupToolTests.cs` (a linter cleaned the file alongside the change)
- [x] 5.2 No `new InteractionSystem()` in tests was removed — tests that instantiate `InteractionSystem` directly for unit-testing it continue to work; the class itself stays (only the `ToolExecutionContext` field is gone)
- [x] 5.3 Full suite passes: **740 passed, 0 failed, 2 skipped**. No test logic changed; only mechanical removal of the now-defunct field

## 6. Documentation
- [x] 6.1 `CLIENT_SERVER_README.md` updated:
   - Removed the phase-1 "independent mutation per session" caveat (already replaced in phase 2c)
   - Updated the deferred-items note to reflect that `InteractionSystem` refactor is a follow-up rather than "phase 2d's job"
   - Added a "Wire protocol notes (post phase 2d)" section with a tool ID reference table mapping legacy hub method names to `ExecuteTool` invocations
- [ ] 6.2 `docs/agents/TOOLS.md` — not updated. Spot-checked: no references to the legacy hub methods exist there

## 7. Validation
- [x] 7.1 All 740 tests pass after the mechanical replacements
- [x] 7.2 `dotnet build` clean. Warnings reduced from baseline (`[Obsolete]` markers gone)
- [x] 7.3 `GameHub.cs` size dropped by ~280 lines
- [x] 7.4 `InteractionSystem.cs` stays for now (deferred to follow-up); the `ToolExecutionContext.InteractionSystem` field is gone — the type isn't reachable from tool execution paths anymore
- [x] 7.5 Confirmed `ToolExecutionContext` carries `MutationGateway` but not `InteractionSystem`

## Notes on the reduced scope vs. original proposal

The original proposal targeted a deeper consolidation (InteractionSystem refactor → delete the type → eliminate the duplication between `LocalMutationGateway` (uses InteractionSystem) and grain methods (reimplements verbs natively)). During implementation it became clear that:

- The 14-method `InteractionSystem` refactor is itself a meaningful scope, comparable in size to phase 2a or phase 2d's cleanup proper
- Bundling it into "phase 2d cleanup" obscures both changes' intent
- Phase 2c's grain methods only natively reimplement the gameplay-critical verbs (Move, Rotate, Pickup, Drop, Open, Close, key-on-door Use). Complex `Use` modes (consume, place, lockpick, climb) still require the legacy session-bound path. The InteractionSystem refactor is the next natural step to unlock full grain-mode parity

A future change (suggested name: `refactor-interaction-system-stateless`) will:
1. Add `(World, Character, WorldLocation, ...)` overloads to every `InteractionSystem.Try*` method
2. Switch `LocalMutationGateway` to use the new API (session overloads can stay as forwarders for backward compat, or be removed)
3. Switch grain methods to call `InteractionSystem` instead of reimplementing logic
4. Optionally: delete the session-taking overloads once `LocalMutationGateway` and the grain both use the stateless API
