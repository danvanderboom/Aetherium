# Audit: Tooling, Testing & Developer Experience

*Audit date: 2026-07-03 · Scope: `Aetherctl`, `Aetherctl.Test`, `WorldGenCLI`, `scripts/`, `Aetherium.Test` suite architecture, CI, repo hygiene. Findings marked **Verified** or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03 (updated post-Phase-1, commit `cd6cf67`).** **Big win on coverage:** the persistence, multiplayer, and worldgen workstreams added ~130 tests — `Aetherium.Test` now runs **865 passed / 0 skipped** (both previously-skipped tests were un-skipped in Phase 1: `PromptRegistryGrainTests` passes cleanly, and the commented-out `LightingRenderingTests` corpse was deleted) — including the kinds of cross-grain integration and determinism tests the audit flagged as missing (new `Aetherium.Test/Persistence/*`, `Aetherium.Test/MultiWorld/*` grain-boundary suites, and `Aetherium.Test/WorldGen/*` determinism/regression suites). The **Aetherctl Orleans package pin is FIXED** (now 9.2.1, matching the server). **FIXED IN PHASE 1:** Aetherctl is now in `Aetherium.sln`; the whole-solution build now succeeds (0 errors, Dashboard fixed separately — see [unity-and-dashboard.md](unity-and-dashboard.md)); the runtime-mismatch finding is **moot** — `develop` migrated all 8 projects to **net10.0**, matching the .NET 10 SDK already on the audit machine, so no `global.json`/`RollForward` is needed. **STILL STANDS:** there is still no functioning CI (the one workflow targets a nonexistent `master` and runs no tests — deliberately deferred per a project cost decision, not an oversight); the CLI tests are still parse-only (no handler invocation); the 16 un-shared TestClusters still drive a multi-minute run. Net: build, boot, and test-coverage gaps from this audit are now closed; CI remains the one open foundational item. Detail in the Reconciliation section at the end.

## Summary

The CLI is well-structured (uniform try/catch handlers, broad `--json` coverage, textbook MSAL secret handling) and repo hygiene is solid (no bin/obj tracked, Unity artifacts ignored, no plaintext secrets). But the developer-experience layer has a load-bearing gap: **there is no functioning CI** — the one workflow triggers on a branch that doesn't exist and runs no tests — which is exactly how 11 Dashboard compile errors and a dev-machine runtime mismatch went unnoticed. The test suite is large and real but structurally expensive (16 un-shared Orleans clusters drive a 6-minute run), split across three test frameworks, and hollow on the CLI side (31 tests, none of which invoke a command handler).

| Severity | Count | Headline |
|---|---|---|
| High | 2 | No functioning CI (dead workflow on a nonexistent branch); every dev script breaks on the .NET-10-only runtime |
| Medium | 5 | Aetherctl missing from the .sln; parse-only CLI tests; 16 un-shared TestClusters; helper hard-exits the process; broken Ctrl+C cleanup in launch scripts |
| Low | ~8 | monitor-lite decode bug; stale script docs; PID files not ignored; hardcoded endpoints; password on CLI; stale packages; flaky timing tests; … |

## High

**No functioning CI; the one workflow is dead-on-arrival.** *Verified.* `.github/workflows/deploy-server.yml:5` triggers on `push: branches: [master]`, but the remote has no `master` (branches are `main`/`develop`; `origin/HEAD` stale-points at the nonexistent `origin/master`). No build/test workflow exists anywhere. Nothing gates merges — the Dashboard's 11 build errors sat unnoticed, the skipped test's "CI environment" rationale references a fiction, and deploys silently stopped if they ever ran. A minimal `dotnet build && dotnet test` on `main` would have caught both the build break and the runtime mismatch.

**Every dev script breaks on this machine's .NET-10-only runtime.** *Verified.* All projects target `net9.0`; the installed toolchain is SDK 10.0.301 / runtime 10.0.9 only. `DOTNET_ROLL_FORWARD` appears nowhere in the repo and cross-major roll-forward isn't automatic, so `dotnet run` in `start-game-test.ps1`, `run-client-ui-tests.ps1`, `start-llm-agents.ps1`, and `repro-move-down.ps1` fails at launch with framework-not-found. Tests only ran because this audit manually set `DOTNET_ROLL_FORWARD=Major`. No `global.json` pins an SDK. *(Fix options: install the .NET 9 runtime, add a `global.json`, or set `RollForward` in the project files / a `runtimeconfig` template.)*

## Medium (verified)

- **Aetherctl is missing from `Aetherium.sln`** — the solution lists 7 projects but not Aetherctl itself; it builds only transitively via its test project and is invisible in IDE solution views.
- **`Aetherctl.Test` is parse-only, with tautological tests** — `WorldGenRenderTests` never invokes a command: one test parses argv and asserts nothing about the PNG; another writes its own PNG magic bytes then asserts them; another re-implements `Directory.CreateDirectory`. Names promise behavior the tests don't check — false confidence.
- **16 active fixtures each deploy a private Orleans TestCluster** — the dominant share of the 6m23s run; there is no `SetUpFixture`/`CollectionDefinition`/`ICollectionFixture` anywhere, so no cluster sharing. Consolidating same-config fixtures onto a shared cluster is the highest-leverage speedup.
- **`Common.WriteOutput` hard-exits the process from a helper** — `Environment.Exit(1)` bypasses System.CommandLine's exit-code plumbing and skips pending `await using` disposals (e.g., the Orleans host in every handler).
- **Ctrl+C cleanup handlers in launch scripts are broken** — *Suspected.* `start-game-test.ps1` and `start-llm-agents.ps1` build a `[ConsoleCancelEventHandler]` from a scriptblock using `$using:` variables, which is only valid in remoting/job contexts; the tracked PIDs are the wrapper PowerShell PIDs, not the game processes, so Ctrl+C orphans the `dotnet run` tree (only the name-based sweep catches the app executables).

## Low (verified)

`monitor-lite.ps1` decodes the full 65536-byte buffer instead of the received count (frames never parse; `monitor-game.ps1` does it right); `start-llm-agents.ps1` documents a nonexistent `agentcli` and hangs on `Read-Host` in non-interactive shells; PID files (`.game-run-pids.json`, `scripts/.llm-agent-pids.json`) aren't gitignored; hardcoded `localhost` endpoints throughout the CLI and dashboard (and `WorldCommands` silently falls back to localhost Orleans on *any* SignalR exception, masking auth failures); `--password` accepted on the command line (shell history/process table exposure); stale packages (`Microsoft.AspNetCore.SignalR 1.1.0` alongside built-in SignalR; `Mvc.Testing 8.0.8` on net9.0) as likely warning sources; flaky timing tests (`WorldGrainTickingTests` fixed `Task.Delay`, `WorldClockTests` `Thread.Sleep`, an LM-Studio E2E probe in the default suite).

## Verified leads (from the brief)

1. **Partial** — Aetherctl pins `Microsoft.Orleans.Client 8.0.0`, but its ProjectReference to Aetherium.Server uplifts every real Orleans assembly to 9.2.1 (confirmed in `project.assets.json`), so the shipped binary talks 9.2.1↔9.2.1 — no live wire-protocol risk today. The pin is dead weight and misleading, and the Orleans CLI path has **zero** test coverage (and hardcodes localhost clustering; the `--gateway/--cluster-id/--service-id` options are hidden and unused).
2. **Confirmed** — both skipped tests have stale reasons: `PromptRegistryGrainTests` ("Orleans codegen in CI") would likely pass today (16 sibling TestCluster fixtures pass, and there's no CI anyway); `LightingRenderingTests` has a fully commented-out body and an obsolete "ConsoleMapView moved" reason.
3. **Partial** — the assembly-conflict problem was real but already solved *inside* `Aetherium.Test` via extern aliases (`Server`/`Console`); no `Aetherium.Client.Test` project exists, and the three "templates" in `CLIENT_TESTS_README.md` are markdown only. The README is stale and misleading.
4. **Confirmed** — `Aetherium.Test` is a dual-framework assembly: **83 NUnit files (585 `[Test]`) + 9 xUnit files (127 `[Fact]/[Theory]`)**, both adapters installed (double discovery), subsystems split across dialects; Aetherctl.Test is pure xUnit — three conventions repo-wide.

## Strengths

- Uniform CLI handler pattern (try/catch → clean one-line `✗ Failed…` exit 1, never a stack trace); broad, consistent `--json` coverage (147 usages) with camelCase `{success,error}` envelopes.
- Textbook MSAL token handling: OS-protected cache (keychain/keyring), platform-appropriate dirs, ROPC gated behind an env flag; config persists only tenant/policy/clientId — no plaintext secrets.
- Test temp-data hygiene: GUID-suffixed temp dirs with TearDown/Dispose cleanup.
- Repo hygiene: zero bin/obj tracked, Unity `Library/Temp/Logs` ignored, tracked `appsettings.json` holds only empty placeholders.
- `run-client-ui-tests.ps1` uses a real port-readiness poll (not a blind sleep) and propagates the client exit code — the best-engineered script.
- `GameHubSmokeTests` uses in-memory `WebApplicationFactory` + `DISABLE_ORLEANS` for a fast SignalR smoke path.
- Dashboard↔WorldGen API drift is structurally impossible — both sides share the `WorldGenCLI.Models` types at compile time.

## Spec alignment

**pcg-tooling** (purpose "TBD") is largely implemented: seed/repro CLI, JSON metrics export, and REST endpoints match the spec exactly; one text drift (spec says "WorldGenCLI when started with `--serve`" but WorldGenCLI is a library — the server starts via `aetherctl worldgen serve`); nothing in the REST section is covered by an automated test. **add-aetherctl-extensions/tasks.md**: all 5 boxes unchecked yet most work shipped (session shims as a root `session` group, `worldgen render --png` with no feature flag, unconditional SkiaSharp instead of conditional, scripts still referencing the dead `agentcli`, and the golden-image render tests never actually invoke rendering) — classic tasks-vs-reality drift, never archived.

## Test coverage (suite architecture)

Dual-runner assembly (NUnit + xUnit); 17 TestingHost fixtures (16 active, none shared) driving the 6-minute run; exactly 2 skips (both stale) plus 3 LM-Studio E2E tests that silently no-op. **Per-command CLI coverage is hollow**: of 31 CLI tests, `CommandStructureTests` (11) assert groups *exist*, `CommonTests` (6) cover global-flag parsing, `SessionCommandsTests` (8) cover structure, `WorldGenRenderTests` (6) cover option presence + self-referential PNG checks — **zero tests invoke any handler**, and the `Auth/`, `SignalR/`, `Orleans/`, `Config/` namespaces have no coverage at all. **CI status: none functioning.**
