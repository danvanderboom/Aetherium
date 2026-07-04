## 1. Replay Viewer
- [x] 1.1 `AgentTelemetryService.GetReplayAsync(agentId, replayId)` → replay JSON (null-safe)
- [x] 1.2 "View" fetches + shows the replay in a scrollable detail pane (loading/empty states)

## 2. Curriculum Progress
- [x] 2.1 New `CurriculumProgressService` over `ICurriculumProgressionGrain` (GetProgress/GetCurrentStage, null-safe)
- [x] 2.2 Page: curriculum id, current stage, stages-completed bar, run totals, success rate, per-stage table

## 3. Benchmark Comparison
- [x] 3.1 New `BenchmarkCatalogService` over `BenchmarkLibrary` (all / categories / by-category)
- [x] 3.2 Page: catalog table + category filter + optional agent telemetry lookup

## 4. Wiring
- [x] 4.1 Register both services in `Program.cs`; add `Aetherium.WorldGen.Training` to `_Imports.razor`
- [x] 4.2 `Aetherium.Dashboard` builds; full solution build + suite green

## Deferred (P3-10 second half, separate change)
- [ ] Move grain interfaces + shared DTOs to `Aetherium.Model` so the Dashboard drops its `Aetherium.Server` reference
