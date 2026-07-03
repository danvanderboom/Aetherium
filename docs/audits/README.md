# Aetherium Subsystem Audits

*Audit date: 2026-07-03. Conducted per the quality-improvement effort; see [RECOMMENDATIONS.md](RECOMMENDATIONS.md) for the consolidated findings register, [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md) for the system-design assessment, and [IMPROVEMENT_PLAN.md](IMPROVEMENT_PLAN.md) for the roadmap.*

Findings in each audit are marked **Verified** (confirmed against code, build output, or test runs with `file:line` evidence) or **Suspected** (plausible from reading, not independently confirmed).

> **Reconciliation note (`develop` @ 2026-07-03).** The ten audits below were written against baseline `5b7e267`. Afterward, 23 commits landed on `develop` (worldgen remediation, grain-authoritative multiplayer, durable persistence, Unity 6 migration). Each audit now carries a **Reconciliation** banner at its top marking findings **FIXED / PARTIAL / STANDS / NEW** against current `develop` code, and the ground-truth table and scorecard below have a "develop" column. **The two headline Critical findings both still stand** (verified on `develop`): the server still hangs at boot with Orleans enabled, and the whole-solution build still fails (Dashboard). But substantial progress landed elsewhere — multi-world travel, co-op perception, and durable persistence now work; the Unity project imports; worldgen is far more robust; the tool-auth bypass and reconnect soft-lock are fixed; and coverage grew to 864 tests.

## Ground truth

| Check | Baseline `5b7e267` (2026-07-03) | `develop` @ 2026-07-03 |
|---|---|---|
| `dotnet build Aetherium.sln` | **FAILS** — 11 errors, all in `Aetherium.Dashboard`: (a) `IClusterClient.Connect/Close` removed in Orleans 7+ (`Services/OrleansClientConnectionService.cs:29,59`); (b) a Razor **type-shadowing collision** — `Pages/BehaviorAnalysis.razor` binds the unqualified name `BehaviorAnalysis` to the component class rather than the server model (which *does* have those properties), affecting it and `AdaptationMonitor.razor`. 235 warnings | **STILL FAILS — identical 11 Dashboard errors** (no commit touched the Dashboard). 283 warnings |
| `dotnet test Aetherium.Test` | **703 passed, 0 failed, 2 skipped** (6m23s) | **864 passed, 0 failed, 2 skipped** (3m28s) — ~130 new persistence/multiworld/worldgen tests |
| `dotnet test Aetherctl.Test` | **31 passed, 0 failed** | unchanged (31 passed) |
| Runtime caveat | Machine has only the .NET 10 runtime; net9.0 tests run with `DOTNET_ROLL_FORWARD=Major` | same (no `global.json`/`RollForward` added) |
| The 2 skipped tests | `Add_And_List_Prompts` (PromptRegistryGrainTests) + 1 lighting-rendering | still 2 skipped |
| OpenSpec inventory | 20 capability specs, 7 active changes, 15 archived (`openspec` CLI not installed) | unchanged |
| Server boot (Orleans **enabled**) | **HANGS at startup** (no banner after 90s — self-referential co-hosting DI bridge, `Program.cs:276-348`) | **STILL HANGS** (boot-tested; only the `IGrainFactory` self-ref was removed — `IClusterClient`/`IWorldHost` at `Program.cs:362-376` still self-resolve). `DISABLE_ORLEANS=1` boots in seconds on both |

## Audit reports & scorecard

Maturity is a subjective roll-up: 🟢 solid, 🟡 works but has real gaps, 🟠 partially wired / significant issues, 🔴 broken or non-functional as shipped.

| Audit | Area | Maturity | Top concerns |
|---|---|---|---|
| [orleans-and-hosting.md](orleans-and-hosting.md) | Silo config, grains, persistence, DI bridge | 🔴 | **Server cannot boot with Orleans enabled** (self-referential DI bridge, boot-tested); `metaStore` unregistered; tick pipeline undriven; world never persisted; `ORLEANS_STORAGE≠memory` silently storage-less |
The **Baseline** column is the audit as written (`5b7e267`); the **develop** column is the reconciled maturity at `develop` @ 2026-07-03.

| Audit | Area | Baseline | develop | Reconciled top concerns |
|---|---|---|---|---|
| [orleans-and-hosting.md](orleans-and-hosting.md) | Silo, grains, persistence, DI | 🔴 | 🔴 | **Server still can't boot with Orleans** (DI bridge); `metaStore` still unregistered. *Fixed:* world persistence, driven tick pipeline, SQLite storage |
| [client-server-protocol.md](client-server-protocol.md) | Hubs, DTOs, auth, session lifecycle | 🟡 | 🟡 | Movement validation still absent (**now across 3 paths**); map-wide door/use; anonymous REST. *Fixed:* reconnect, JoinWorld, ExecuteTool auth |
| [worldgen-and-pcg.md](worldgen-and-pcg.md) | Generator pipeline, prefabs, validation | 🟡 | 🟡 | Server ignores requested generator; placement stubs; params inert (worlds still empty in-game). *Fixed:* determinism, algorithm correctness, error handling |
| [simulation-core.md](simulation-core.md) | ECS, interactions, clock/weather | 🟠 | 🟠 | Movement bypasses rules (both paths); `Memory` discards; NPCs static; adjacency unchecked. *Fixed:* West/East axis (dead code); tick pipeline driven |
| [perception-fov-lighting.md](perception-fov-lighting.md) | FOV, lighting, vision modes | 🟢 | 🟢 | Infrared still black; NPCs not drawn. *Fixed:* co-op observer perception (grain maps); FOV rotation still fixed-by-design |
| [agents-and-tools.md](agents-and-tools.md) | Tool registry, profiles, agent grains | 🟡 | 🟡 | Unbounded telemetry; runner off-scheduler; prompts never loaded. *Fixed:* **hub auth bypass**; tool-context Session |
| [narrative-and-multiworld.md](narrative-and-multiworld.md) | Narrative, portals, instances, meta | 🔴 | 🟡 | Quests can't complete (`ActiveQuestIds` unpopulated); ACL unenforced; instances/parties unreachable; `metaStore` unregistered. *Fixed:* **travel, co-op, persistence** |
| [console-client.md](console-client.md) | Rendering, audio, input, monitoring | 🟡 | 🟡 | Dead input block; torn-frame rendering; ~6k lines dead code. *Fixed:* **reconnect soft-lock** |
| [unity-and-dashboard.md](unity-and-dashboard.md) | Unity client, Blazor dashboard | 🔴 | 🟠 | Unity **now imports/tests** (🟡) but live-mode stub + tiles invisible; **Dashboard still uncompilable** (🔴) |
| [tooling-testing-devex.md](tooling-testing-devex.md) | aetherctl, WorldGenCLI, tests, CI | 🟡 | 🟡 | Still no CI; runtime mismatch; Aetherctl missing from .sln. *Fixed:* **+130 tests** (864), Orleans pin |

**The most important structural finding still holds** across all ten: subsystems are built and unit-tested as *islands*. The post-baseline work is genuine and important — it started **building the bridges** (grain-authoritative multiplayer, durable persistence, working travel and co-op perception) — but the two gates that would let a player actually reach any of it remain shut: the server can't boot with Orleans, and the movement/interaction layer under all of it is still unvalidated. See [DESIGN_ANALYSIS.md](DESIGN_ANALYSIS.md).
