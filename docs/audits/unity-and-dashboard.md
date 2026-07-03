# Audit: Unity Client & Dashboard

*Audit date: 2026-07-03 · Scope: `Aetherium.Unity` (Assets scripts, tests, input, asmdefs) and `Aetherium.Dashboard` (Blazor Server). Findings marked **Verified** or **Suspected**. Note: `origin/develop` has since fixed much of the Unity project setup (see each finding); this audit reflects the branch under review.*

> **Reconciliation — `develop` @ 2026-07-03. Unity maturity 🔴 → 🟡; Dashboard 🔴 unchanged.** The audit anticipated much of this: the Unity 6000.4 migration landed. **UNITY FIXED:** the project now imports and compiles (`Packages/manifest.json` with InputSystem/TestFramework/Newtonsoft.Json, asmdefs moved to their own subfolders, `ProjectSettings.asset` present); mock frame loading works (`PerceptionMockProvider` now uses `JsonConvert.DeserializeObject`, so `Visuals`/dictionaries populate); the `SetMode(false)` early-return bug is fixed; `Main.unity` exists; PlayMode tests now compile. **UNITY STILL PARTIAL/STANDS:** live mode (`PerceptionSignalRClient`) is still a stub (no deserializer, `ExecuteToolAsync` returns failure); tiles still render invisible (`CreateDefaultTile` makes a spriteless `Tile` — the tile-type→sprite mapping is still unimplemented). Net: Unity went from "can't even import" to a working mock client with a clear path to live mode. **DASHBOARD STANDS (unchanged):** the whole-solution build still fails with the identical 11 errors — none of the 23 commits touched the Dashboard. The break is still the Razor type-shadowing collision plus the removed Orleans 7+ `IClusterClient.Connect/Close`; the server-side `BehaviorAnalysis` model still has all four properties (confirming the break is Razor-side only). Detail in the Reconciliation section at the end.

## Summary

Both clients are, on this branch, **non-functional as committed**. The Unity project cannot even import (three asmdefs in one folder, no package manifest, no `ProjectSettings.asset`), its live SignalR client is a stub, and its mock path produces empty perceptions because `JsonUtility` can't deserialize the property-based, dictionary-bearing DTOs it's given. The Dashboard has not compiled since it was first committed (~8 months) — 11 errors from a Razor type-shadowing collision and removed Orleans 7+ client APIs. Both have effectively zero behavioral test coverage, which is why the Dashboard's breakage went undetected. The underlying designs (perception-provider abstraction, option-selection state machine, YARP hub proxy, modern Orleans client bootstrap) are sound; the wiring and setup are not.

## Dashboard: root cause of the build break

The 11 errors are **not** a server-model drift. The server model `BehaviorAnalysis` (`BehaviorAnalyzer.cs:521-543`) has all four referenced properties (`ActionPatterns`, `ExplorationPatterns`, `StrugglePatterns`, `SuccessPatterns`). The real causes:

1. **C# type-shadowing collision** — `Pages/BehaviorAnalysis.razor` generates a class named `BehaviorAnalysis`; inside it, the unqualified identifier `BehaviorAnalysis` (`:205,220`) binds to the component class (which has none of those properties), not the server model. `AdaptationMonitor.razor:141` hits the same collision because namespace members beat `@using` imports in name resolution.
2. **Removed Orleans APIs** — `OrleansClientConnectionService.cs:29,59` call `IClusterClient.Connect`/`.Close`, removed in Orleans 7's hosted-client model (the project is on 9.2.1).

**Git evidence:** the Razor pages were added in `94a94f0` (2025-10-31, "test: green suite" — ironically) and the Orleans service in `6062f72` (2025-11-02); both have never compiled. `origin/develop` has not fixed them either. **Minimal fix:** rename `BehaviorAnalysis.razor` → `BehaviorAnalysisPage.razor` (kills the collision for both pages) or add a `using`-alias to the server type; delete `OrleansClientConnectionService` and rely on the already-present `builder.Host.UseOrleansClient(...)` (`Program.cs:49`), using a connection-retry filter for resilience.

## Unity findings

- **[Critical · Verified] The project cannot import/compile as committed** — three asmdefs in the `Assets/` root folder (Unity forbids >1 per folder), test asmdefs with empty `references`, no `Packages/manifest.json` (so Input System, Test Framework, uGUI aren't declared), and no `ProjectSettings.asset`. Nothing in the Unity scope is runnable or testable on this branch. (`origin/develop` fixed this on 2026-05-13: Unity 6000.4 skeleton, manifest with inputsystem/test-framework/newtonsoft-json.)
- **[Critical · Verified] Mock frame loading produces empty perceptions** — `PerceptionMockProvider.cs:55` uses `JsonUtility.FromJson<PerceptionLite>`, but `JsonUtility` ignores auto-properties *and* can't populate `Dictionary<string,VisualLite>` — so loaded frames are all-default (empty `Visuals` → zero tiles). The advertised default (mock) mode renders nothing.
- **[High · Verified] `SetMode(false)` early-return bug** — `GameClientFacade.cs:55-57` returns because `isLiveMode` is already false when `Awake` calls `SetMode(false)`, so `currentProvider` is never assigned; the initial render never fires and display only recovers after the first input.
- **[High · Verified] Live mode is unreachable and unimplemented** — `PerceptionSignalRClient` is a stub (HubConnection commented out, `ExecuteToolAsync` always returns `Success=false`); `SetMode(true)` has no callers. There is no deserializer at all — live mode cannot work in any form.
- **[Medium · Verified]** Frame replay never advances (`NextFrame()` has zero callers, no timer, one frame file); mock/live coordinate divergence (server sends `PlayerLocation=(0,0,0)` + relative visuals, mock mutates PlayerLocation while visuals stay fixed — no anchoring strategy); mock ignores heading for forward/backward; **all tiles render invisible** (`CreateDefaultTile` makes a `Tile` with no sprite; tile-type→sprite mapping is 0% complete); PlayMode tests load a nonexistent `Main` scene and would fail on timing (moveSpeed vs assertion distances) even if they compiled.
- **[Low · Verified]** Dead `GameManager.CycleZLevel`; `[SerializeField]` on a Dictionary (no-op); `.gitignore` globally ignores `*.meta` while a later comment says to keep them (GUIDs regenerate per machine); `ProjectVersion.txt` pins the never-shipped "2023.3.0f1".

### Unity verified leads

1. **Partial drift** — most Lite shims match their DTOs, but `VisualLite.TileTypeId: string` has **no counterpart** in `VisualDto` (which has `Terrain`/`Entities`/`ThingsSeen`) — raw server JSON would never populate it, and no mapping layer exists. All Lite types use auto-properties, which `JsonUtility` ignores.
2. **Confirmed (worse)** — live SignalR is a stub with no deserializer; the dictionary problem also breaks mock mode (empty Visuals).
3. **Partial** — the controller *bindings* all check out (stick→move, LB/RB rotate, LT/RT z-level, A confirm, B cancel, D-pad options), but the tests don't assert behavior (they call the facade directly, include `Assert.IsTrue(x || !x)` tautologies), **can't compile** (they assign PlayerController's private `[SerializeField]` fields → CS0122), and keyboard `d` is double-bound to move-east *and* changelevel-down.
4. **Confirmed mock-by-default; refuted "runs out of the box"** — no scripting defines, no packages, no ProjectSettings, broken asmdefs — even mock mode won't compile on first open.

## Dashboard findings (beyond the build break)

- **[High · Verified] PCG API endpoints aren't served by the game server** — `WorldGenApi.MapEndpoints` is only called by `aetherctl worldgen serve` (default port 5000, colliding with the game server's 5000). `PcgApiClient` hard-codes `http://localhost:5000/api`, so PCG.razor and the management pages can never work simultaneously.
- **[Medium · Verified]** Hardcoded URLs/ports throughout (only `ManagementApiClient` is config-driven); adaptation pages bypass the typed clients and YARP with raw `HttpClient`; a Razor format-string bug renders literal `:P1`/`:F0` text (`@x.SuccessRate:P1` needs `@($"{x.SuccessRate:P1}")`).
- **[Low · Verified]** `BenchmarkComparison.razor` and `CurriculumProgress.razor` are `// TODO` stubs; ReplayViewer's View button is a no-op; `AllowAnyOrigin` CORS.
- **[Info · Verified]** The Dashboard project-references all of `Aetherium.Server` (dragging grains/WorldGenCLI into its build) to reach grain interfaces/models — this is what *enabled* the type collision. Shared contracts belong in `Aetherium.Model`.

## Strengths

- **Dashboard**: the modern Orleans client bootstrap is *already present* (`UseOrleansClient` + localhost clustering) — only the legacy hosted service needs deleting; the YARP hub proxy is correctly configured and `AgentMonitor` reconnects robustly (backoff, `Reconnected` resubscribe, `Closed` retry); all HTTP calls line up with real server endpoints and grain interfaces.
- **Unity**: clean `IPerceptionProvider` abstraction with facade-managed mode switching and graceful-degradation messaging; a complete, well-guarded option-selection state machine (input suppression, HUD save/restore); a well-formed `InputActions` asset whose composite axes match handler semantics.
- The server-side perception-privacy design (absolute coordinates never leave the server) is solid and deliberate.
- `docs/unity` is unusually candid about the JsonUtility/SignalR/iOS limitations, and `Scenes/README.md` is an actionable scene-construction guide.

## Spec & doc alignment

- **`add-unity-2d-client/tasks.md`: all 14 boxes unchecked** while ~10 are substantially implemented (under-reporting drift). Genuinely incomplete: packages/asmdefs, `Main.unity` never committed, live-mode receive/send, sequential frame replay, HUD connection status, Z-level cycling.
- **`add-xbox-controller-unity/tasks.md`: all 14 boxes checked** — bindings are genuinely done, but the "HUD in GameManager" item actually lives in PlayerController, and the testing items are checked-but-hollow (vacuous, non-compiling tests).
- **`docs/unity/README.md` drift:** claims `Main.unity` and `Packages/` exist (false on this branch) and presents iOS as supported (no iOS config anywhere). Should be reframed against the actual branch state (or the doc rebased onto `develop`).

## Test coverage

- **Unity (6 files): effective behavioral coverage ≈ 0.** EditMode parsing tests depend on `JsonUtility` populating the model (can't pass); the rest are trivial constructor/property checks. PlayMode tests are blocked in layers — asmdef misconfiguration, private-field compile errors, a nonexistent `Main` scene, timing asserts that contradict `moveSpeed`, and surviving `IsNotNull`/tautology assertions. Not headless-CI-runnable.
- **Dashboard: zero tests.** No bUnit component tests, no service-layer tests. This is precisely why an uncompilable project survived ~8 months in `Aetherium.sln` — nothing in the "green suite" builds or exercises it, and there is no full-solution build gate (see [tooling-testing-devex.md](tooling-testing-devex.md)).
