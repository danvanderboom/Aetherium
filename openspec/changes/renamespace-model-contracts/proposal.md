## Why
The previous contract-move changes (`move-contracts-to-model`, `finish-dashboard-decoupling`) relocated the shared contracts into `Aetherium.Model` but **retained their original namespaces** (`Aetherium.Server.Agents.Telemetry`, `Aetherium.Server.Agents.Analysis`, `Aetherium.WorldGen.Training`, `Aetherium.Server.Narrative`, `WorldGenCLI.Models`) to avoid churn. That left `Aetherium.Server.*`- and `WorldGenCLI.*`-named namespaces physically living in `Aetherium.Model.dll` — a smell (the namespace advertises an assembly the type no longer lives in). This change completes the cleanup by re-namespacing the moved contracts to `Aetherium.Model.*`.

## What Changes
Rename the moved contract types' namespaces (definitions in `Aetherium.Model/Contracts/**`) and update every consumer:
- `Aetherium.Server.Agents.Telemetry` → `Aetherium.Model.Telemetry` (IAgentTelemetryGrain, PerformanceSnapshot, PerformanceAnalysis, ActionTypeStats, ReplayData, ReplayStep)
- `Aetherium.Server.Agents.Analysis` → `Aetherium.Model.Analysis` (BehaviorAnalysis + pattern DTOs, InterestProfile + ActionPreference/AreaPreference)
- `Aetherium.WorldGen.Training` → `Aetherium.Model.Training` (ICurriculumProgressionGrain + CurriculumProgress, CurriculumStage + nested, BenchmarkScenario + nested, BenchmarkLibrary, JsonStringToIntConverter)
- `Aetherium.Server.Narrative` → `Aetherium.Model.Narrative` (QuestDefinition, QuestObjective)
- `WorldGenCLI.Models` → `Aetherium.Model.Pcg` (the 10 PCG DTOs)

The **producing logic** (analyzers, grain implementations, generators) stays in its original `Aetherium.Server.*` / `WorldGen.Training` namespaces — so those namespaces are now *only* server-side, and each contract's namespace matches the assembly it lives in. Consumer usings were updated across Server/CLI/Console/Test (add the new `using` alongside the retained server one, since those projects use both contracts and logic) and the Dashboard (replace, since it references only `Aetherium.Model`).

## Impact
- Affected specs: `training-dashboard` (MODIFIED: the shared contracts now live under `Aetherium.Model.*`).
- Affected code: namespace declarations in the ~23 moved `Aetherium.Model/Contracts/**` files; `using`/`@using` directives and a few fully-qualified references across ~40 consumer files in `Aetherium.Server`, `Aetherctl`, `WorldGenCLI`, `Aetherium.Console`, `Aetherium.Test`, `Aetherium.Dashboard`. No type/member renames, no behavior change.
- Build/behavior: relocation of namespaces only. Full solution build 0 errors; full suite green (1027 passed / 0 skip). Orleans grain-proxy identity and serialization are unaffected — both client and silo use the same (renamed) contract types from `Aetherium.Model`, verified by the passing telemetry/curriculum/benchmark/narrative tests.

## Status
Implemented on `feat/phase5-renamespace` (branched from `develop`). This closes the last cosmetic follow-up from the contract move: every relocated contract's namespace now matches `Aetherium.Model`, and the `Aetherium.Server.*` / `WorldGenCLI.Models` namespaces are gone from `Aetherium.Model.dll`.
