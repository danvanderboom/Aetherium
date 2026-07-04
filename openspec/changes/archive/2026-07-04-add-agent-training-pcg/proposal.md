## Why
Agents need structured training environments with comprehensive telemetry tracking, curriculum progression, and benchmark evaluation. Currently, agent performance is not systematically tracked, there's no framework for progressive training, and no standard benchmarks exist for evaluating agent capabilities. This makes it difficult to train agents effectively, identify weaknesses, and compare performance across different scenarios.

## What Changes
- Add agent telemetry system: performance snapshot collection per step, replay storage for failed runs, performance analysis with weakness identification
- Add curriculum generation system: manual curriculum definitions (JSON), automatic curriculum generation based on performance, curriculum progression tracking
- Add benchmark scenario library: benchmark definitions (JSON), benchmark loading/generation, success criteria types, edge case generation from failure patterns
- Add Blazor dashboard: real-time monitoring, SignalR telemetry broadcasting, REST API controllers for data access
- Enhance PCG for training: difficulty profiles, training mode flags, heatmap collection support
- Update CLI tools: add --benchmark flag to WorldGenCLI, add train subcommand to AgentCLI

## Impact
- Affected specs: NEW capabilities - agent-telemetry, training-curriculum, training-benchmarks, training-dashboard; MODIFIED - pcg-core (difficulty profiles, training mode)
- Affected code:
  - `Aetherium.Server/Agents/` - Telemetry collection, AgentRunnerGrain integration
  - `Aetherium.Server/Agents/Telemetry/` - Telemetry grains, analysis, replay storage
  - `Aetherium.Server/WorldGen/Training/` - Curriculum and benchmark systems
  - `Aetherium.Server/WorldGen/` - Training enhancements (DifficultyProfile, WorldGenerationRequest updates)
  - `Aetherium.Dashboard/` - New Blazor Server project for monitoring
  - `Aetherium.Server/Controllers/` - REST API controllers
  - `WorldGenCLI/Program.cs` - Benchmark flag
  - `AgentCLI/Program.cs` - Train subcommand
- New files:
  - Telemetry: AgentTelemetryGrain, PerformanceSnapshot, ReplayStorage, PerformanceAnalyzer
  - Curriculum: CurriculumDefinition, CurriculumStage, AutoCurriculumGenerator, CurriculumProgressionGrain
  - Benchmarks: BenchmarkScenario, BenchmarkLibrary, BenchmarkGenerator
  - Dashboard: Blazor Server project with pages, components, SignalR hub
  - Controllers: AgentTelemetryController, BenchmarkController, CurriculumController

