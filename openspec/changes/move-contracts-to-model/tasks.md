## 1. Relocate wholesale contract files to Aetherium.Model (namespaces retained)
- [x] 1.1 Telemetry: `IAgentTelemetryGrain.cs`, `PerformanceSnapshot.cs`
- [x] 1.2 Training: `ICurriculumProgressionGrain.cs`, `CurriculumStage.cs`, `BenchmarkScenario.cs`, `BenchmarkLibrary.cs`, `JsonConverters.cs`

## 2. Split DTOs out of logic files into Aetherium.Model
- [x] 2.1 `PerformanceAnalysis` + `ActionTypeStats` ← `PerformanceAnalyzer.cs` (analyzer logic stays in Server)
- [x] 2.2 `BehaviorAnalysis` + pattern DTOs ← `BehaviorAnalyzer.cs` (analyzer logic stays in Server)
- [x] 2.3 `QuestDefinition` + `QuestObjective` ← `NarrativeDefinition.cs` (rest of cluster stays in Server)

## 3. Drop the Dashboard→Server reference
- [x] 3.1 Remove `Aetherium.Server` `ProjectReference` from `Aetherium.Dashboard.csproj`
- [x] 3.2 Verify: full solution builds with no `using`/namespace churn elsewhere

## 4. Verify
- [x] 4.1 Full solution build 0 errors; full suite green (1026 passed / 1 skip)
- [x] 4.2 Orleans serialization + grain-proxy codegen confirmed by passing telemetry/curriculum/benchmark/narrative tests

## Deferred (follow-up)
- [ ] Move `WorldGenCLI.Models` to `Aetherium.Model` (or decouple `WorldGenCLI` from Server) and drop the Dashboard→`WorldGenCLI` reference, to remove the last transitive Server dependency
- [ ] Optional: re-namespace the relocated contracts to `Aetherium.Model.*` (cosmetic; currently retain original namespaces to avoid ~70-file churn)
