## Why
Three dashboard pages shipped as placeholder stubs — `BenchmarkComparison.razor`, `CurriculumProgress.razor`, and `ReplayViewer.razor` — each an `alert-info` "this page will show …" with a `// TODO` and no data. This is the first half of Phase 5 item **P3-10** (`docs/audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md`: "finish the stub pages"). The data sources already exist and were simply never wired:
- `IAgentTelemetryGrain.GetReplayAsync(replayId)` — the Replay Viewer's "View" button was a `// TODO`.
- `ICurriculumProgressionGrain.GetProgressAsync()` (keyed by agent id) — curriculum stage/run progress.
- `BenchmarkLibrary.GetAllBenchmarks()` — the built-in benchmark catalog.

## What Changes
- **Replay Viewer** — the per-run **View** action now fetches the stored replay JSON (`AgentTelemetryService.GetReplayAsync`) and shows it in a scrollable detail pane beside the failed-run list, with loading/empty states.
- **Curriculum Progress** — new `CurriculumProgressService` reads `ICurriculumProgressionGrain` for an agent; the page shows the curriculum id, current stage (with name), a stages-completed progress bar, run totals, current success rate, and a per-stage table.
- **Benchmark Comparison** — new `BenchmarkCatalogService` exposes the benchmark catalog; the page lists scenarios (name, description, categories, difficulty, success-criteria) with a category filter, plus an optional agent-id lookup that shows that agent's overall telemetry (success rate / steps / action counts) to compare against.
- DI: both new services registered in `Program.cs` (curriculum via the Orleans client like `AgentTelemetryService`; benchmark catalog as a plain singleton — the library is in-process). `_Imports.razor` gains `Aetherium.WorldGen.Training`.

## Impact
- Affected specs: `training-dashboard` (ADDED: Benchmark Catalog Page, Curriculum Progress Page, Replay Viewer Detail).
- Affected code: `Aetherium.Dashboard/Pages/{BenchmarkComparison,CurriculumProgress,ReplayViewer}.razor`, new `Aetherium.Dashboard/Services/{CurriculumProgressService,BenchmarkCatalogService}.cs`, `Aetherium.Dashboard/Services/AgentTelemetryService.cs` (add `GetReplayAsync`), `Aetherium.Dashboard/Program.cs`, `Aetherium.Dashboard/_Imports.razor`.
- Build impact: additive; no breaking changes. New services are null-safe (return null/empty when the Orleans client is absent), mirroring the existing `AgentTelemetryService` so pages render an empty state rather than throw.
- **Not in this change:** the second half of P3-10 — moving shared contracts to `Aetherium.Model` so the Dashboard drops its `Aetherium.Server` project reference. That is a larger refactor than the audit implies: the Dashboard calls grains directly, so it needs the grain *interfaces* (`IAgentTelemetryGrain`, `ICurriculumProgressionGrain`) and their DTOs, which currently live in `Aetherium.Server`. Relocating those (and updating every Server consumer) is a cross-cutting move tracked as a follow-up.

## Status
Implemented on `feat/phase5-dashboard` (branched from `develop`). `Aetherium.Dashboard` builds 0 errors; full solution build + suite green. The Dashboard has no unit-test harness (Blazor UI; no bUnit), consistent with the project's current state — verification is via build + the null-safe service contract mirroring `AgentTelemetryService`.
