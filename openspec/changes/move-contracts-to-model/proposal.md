## Why
The Dashboard (`Aetherium.Dashboard`) referenced the entire `Aetherium.Server` project just to reach a handful of shared contracts — the agent telemetry/analysis DTOs, the curriculum/benchmark training contracts, and a quest-definition DTO — even though it never uses any Server *logic* (it calls grains via the Orleans client and REST endpoints via HTTP). This is the second half of Phase 5 item **P3-10** (`docs/audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md`: "move shared contracts to `Aetherium.Model` so it stops referencing all of `Aetherium.Server`").

## What Changes
Relocate the shared contract types the Dashboard consumes from `Aetherium.Server` into `Aetherium.Model`, **preserving their namespaces** so the ~70 in-solution consumers (Server grains/analyzers, CLI, tests) need no `using` changes — the decoupling is an assembly move, not a rename:

- **Telemetry** (`Aetherium.Server.Agents.Telemetry`): `IAgentTelemetryGrain`, `PerformanceSnapshot`, and `PerformanceAnalysis`/`ActionTypeStats` (split out of `PerformanceAnalyzer.cs`, whose analyzer logic stays in Server).
- **Analysis** (`Aetherium.Server.Agents.Analysis`): `BehaviorAnalysis` + its pattern DTOs (split out of `BehaviorAnalyzer.cs`, logic stays in Server).
- **Training** (`Aetherium.WorldGen.Training`): `ICurriculumProgressionGrain` (+ `CurriculumProgress`/`CurriculumStageProgressInfo`), `CurriculumStage` (+ nested), `BenchmarkScenario` (+ nested), `BenchmarkLibrary`, and the internal `JsonStringToIntConverter` (`CurriculumStage`'s serialization dependency).
- **Narrative** (`Aetherium.Server.Narrative`): `QuestDefinition` + `QuestObjective` (split out of `NarrativeDefinition.cs`; the rest of the narrative-definition cluster stays in Server), for the AdaptationMonitor page's adaptive-quest payloads.
- Remove the `Aetherium.Server` **project reference** from `Aetherium.Dashboard.csproj`.

Grain interfaces move cleanly: `Aetherium.Model` already carries `Microsoft.Orleans.Sdk`, so it generates the client proxies; the grain *implementations* stay in `Aetherium.Server` (which references `Model`).

## Impact
- Affected specs: `training-dashboard` (ADDED: a "Dashboard depends only on shared contracts" requirement).
- Affected code: contract files relocated `Aetherium.Server/** → Aetherium.Model/Contracts/**` (telemetry, analysis, training, narrative), two analyzer files split (`PerformanceAnalyzer.cs`, `BehaviorAnalyzer.cs`), `NarrativeDefinition.cs` split, `Aetherium.Dashboard.csproj` (drop Server ref). No `using`/namespace churn elsewhere (namespaces retained).
- Build/behavior: additive/relocation only — no behavior change. Full solution build 0 errors; full suite green (1026 passed / 1 skip). Orleans serialization + grain-proxy codegen verified across the assembly boundary by the passing telemetry/curriculum/benchmark/narrative tests.

## Known limitation (follow-up)
The Dashboard still pulls `Aetherium.Server` **transitively** through `WorldGenCLI` (whose `WorldGenCLI.Models` PCG DTOs the PCG page uses; `WorldGenCLI` itself references `Aetherium.Server`). The Dashboard's *own* source no longer references any Server-only type, and its direct project reference is gone — but fully removing Server from the Dashboard's transitive closure requires a separate change: move `WorldGenCLI.Models` (10 self-contained DTO files, no Server deps) into `Aetherium.Model` and drop the Dashboard→`WorldGenCLI` reference, or otherwise decouple `WorldGenCLI` from `Aetherium.Server`. Tracked as a follow-up.

## Status
Implemented on `feat/phase5-contracts` (branched from `develop`). Namespaces retained to keep the move safe and reviewable; a clean re-namespacing (`Aetherium.Model.*`) is a separate optional pass.
