# Aetherium Subsystem Audits

*Audit date: 2026-07-03. Conducted per the quality-improvement effort; see [RECOMMENDATIONS.md](RECOMMENDATIONS.md) for the consolidated findings register, [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md) for the system-design assessment, and [IMPROVEMENT_PLAN.md](IMPROVEMENT_PLAN.md) for the roadmap.*

Findings in each audit are marked **Verified** (confirmed against code, build output, or test runs with `file:line` evidence) or **Suspected** (plausible from reading, not independently confirmed).

## Ground truth (measured 2026-07-03)

| Check | Result |
|---|---|
| `dotnet build Aetherium.sln` | **FAILS** — 11 errors, all in `Aetherium.Dashboard`: (a) `IClusterClient.Connect/Close` removed in Orleans 7+ (`Services/OrleansClientConnectionService.cs:29,59`); (b) a Razor **type-shadowing collision** — `Pages/BehaviorAnalysis.razor` binds the unqualified name `BehaviorAnalysis` to the component class rather than the server model (which *does* have `StrugglePatterns`/`ActionPatterns`/`SuccessPatterns`/`ExplorationPatterns`), affecting it and `AdaptationMonitor.razor`. Never compiled since ~2025-10-31. 235 warnings (mostly CS8602/CS8618 nullability in tests). All other projects build clean |
| `dotnet test Aetherium.Test` | **703 passed, 0 failed, 2 skipped** (705 total, 6m23s). Docs claiming "597 passed" were stale |
| `dotnet test Aetherctl.Test` | **31 passed, 0 failed** |
| Runtime caveat | Machine has only the .NET 10 runtime; net9.0 tests were run with `DOTNET_ROLL_FORWARD=Major`. Behavior differences vs a true .NET 9 runtime are theoretically possible |
| The 2 skipped tests | `Add_And_List_Prompts` (PromptRegistryGrainTests — Orleans codegen unavailable) + 1 in lighting rendering (ConsoleMapView moved client-side) |
| OpenSpec inventory | 20 capability specs, 7 active changes, 15 archived changes (`openspec` CLI not installed on this machine; inventory from the filesystem) |
| Server boot test | **Orleans enabled: HANGS at startup** (no banner after 90s — self-referential co-hosting DI bridge, `Program.cs:276-348`; see [orleans-and-hosting.md](orleans-and-hosting.md)). `DISABLE_ORLEANS=1`: boots in seconds. Also: under `dotnet run`, `launchSettings.json` overrides the listen URLs to 50309/50310, not the documented 5000 |

## Audit reports & scorecard

Maturity is a subjective roll-up: 🟢 solid, 🟡 works but has real gaps, 🟠 partially wired / significant issues, 🔴 broken or non-functional as shipped.

| Audit | Area | Maturity | Top concerns |
|---|---|---|---|
| [orleans-and-hosting.md](orleans-and-hosting.md) | Silo config, grains, persistence, DI bridge | 🔴 | **Server cannot boot with Orleans enabled** (self-referential DI bridge, boot-tested); `metaStore` unregistered; tick pipeline undriven; world never persisted; `ORLEANS_STORAGE≠memory` silently storage-less |
| [client-server-protocol.md](client-server-protocol.md) | Hubs, DTOs, auth, session lifecycle | 🟡 | No movement validation (Critical); map-wide door/use; anonymous REST; no reconnect/resume; obsolete-vs-tool path drift |
| [worldgen-and-pcg.md](worldgen-and-pcg.md) | Generator pipeline, prefabs, validation, PCG API | 🟡 | Server registry never populated (ignores requested generator); placement features are stubs; generator params inert; server vs tooling pass-list divergence |
| [simulation-core.md](simulation-core.md) | ECS, interactions, clock/weather/seasons | 🟠 | West/East axis-swap; movement bypasses all rules; `Memory` discards; no NPC ticking; capacity exploit; thread-unsafe clock/weather/scheduler |
| [perception-fov-lighting.md](perception-fov-lighting.md) | FOV, lighting, vision modes | 🟢 | FOV rotation bug **fixed** by design; infrared renders black; observers get no updates; lighting modes mutually exclusive |
| [agents-and-tools.md](agents-and-tools.md) | Tool registry, profiles, agent grains, LLM adapter | 🟡 | Hub-level auth bypass; unbounded telemetry/replay; runner off-scheduler; prompts never loaded; docs claim wrong tool count (23 real) |
| [narrative-and-multiworld.md](narrative-and-multiworld.md) | Narrative, portals, clusters, instances, meta-progression | 🔴 | `metaStore` unregistered (throws); narrative/travel dead-wired; instances/parties unreachable; docs claim "complete & tested" |
| [console-client.md](console-client.md) | Rendering, audio, input, monitoring | 🟡 | Dead input block disables features; unsynchronized torn-frame rendering; reconnect soft-lock; ~6k lines dead legacy code; no audio assets |
| [unity-and-dashboard.md](unity-and-dashboard.md) | Unity client, Blazor dashboard | 🔴 | Unity can't import (asmdefs/no manifest); mock renders nothing; live mode a stub; Dashboard uncompilable ~8 months |
| [tooling-testing-devex.md](tooling-testing-devex.md) | aetherctl, WorldGenCLI, scripts, test infra | 🟡 | No functioning CI; dev scripts break on .NET-10-only runtime; Aetherctl missing from .sln; hollow CLI tests |

**The most important structural finding** cuts across all ten: subsystems are built and unit-tested as *islands*, but the *bridges* between them (and to a client the player can actually use) are missing, stubbed, or dead-wired — so much of the documented feature set does not function in real gameplay. See [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md).
