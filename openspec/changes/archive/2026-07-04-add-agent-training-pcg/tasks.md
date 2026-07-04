> Status (2026-07-03): verified substantially complete — telemetry, curriculum, benchmarks, dashboard (now builds after Phase 1 fixes), controllers, docs, and tests all exist in code. One gap: no `train` subcommand exists in the CLI (7.2).

## 1. Telemetry Infrastructure
- [x] 1.1 Create AgentTelemetryGrain for storing performance data
- [x] 1.2 Create PerformanceSnapshot model for per-step metrics
- [x] 1.3 Create ReplayStorage for failed run storage
- [x] 1.4 Create PerformanceAnalyzer for weakness identification
- [x] 1.5 Integrate telemetry collection into AgentRunnerGrain

## 2. Curriculum System
- [x] 2.1 Create CurriculumDefinition and CurriculumStage models
- [x] 2.2 Create AutoCurriculumGenerator for automatic difficulty adjustment
- [x] 2.3 Create CurriculumProgressionGrain for tracking progression
- [x] 2.4 Create example curriculum JSON files (beginner-dungeon, advanced-combat)
- [x] 2.5 Integrate curriculum stages into WorldGenerationRequest

## 3. Benchmark Library
- [x] 3.1 Create BenchmarkScenario model with recipe and success criteria
- [x] 3.2 Create BenchmarkLibrary for loading scenarios from JSON
- [x] 3.3 Create BenchmarkGenerator for on-demand generation and variations
- [x] 3.4 Create example benchmark JSON files (navigation-basic, combat-survival, puzzle-keys)
- [x] 3.5 Integrate benchmark generation into WorldGenCLI

## 4. PCG Training Enhancements
- [x] 4.1 Create DifficultyProfile with numeric scoring (0-100)
- [x] 4.2 Add difficulty profile calculation to GenerationMetrics
- [x] 4.3 Add training mode flags to WorldGenerationRequest
- [x] 4.4 Add predicted success rate calculation

## 5. Blazor Dashboard
- [x] 5.1 Create Aetherium.Dashboard Blazor Server project
- [x] 5.2 Add project to solution
- [x] 5.3 Create dashboard pages (Index, AgentMonitor, PerformanceAnalytics, CurriculumProgress, BenchmarkComparison, ReplayViewer)
- [x] 5.4 Create AgentDashboardHub SignalR hub
- [x] 5.5 Create reusable components (MetricsCard)
- [x] 5.6 Configure SignalR and routing

## 6. REST API Controllers
- [x] 6.1 Create AgentTelemetryController for telemetry data access
- [x] 6.2 Create BenchmarkController for benchmark management
- [x] 6.3 Create CurriculumController for curriculum management
- [x] 6.4 Register controllers in Program.cs

## 7. CLI Updates
- [x] 7.1 Add --benchmark flag to WorldGenCLI
- [ ] 7.2 Add train subcommand to AgentCLI (start, status, benchmark) (unchecked 2026-07-03: no `train` subcommand exists anywhere in Aetherctl/Commands — AgentCommands.cs has no train group)
- [x] 7.3 Update WorldGenCLI to load benchmarks from library

## 8. Documentation
- [x] 8.1 Create docs/training/README.md
- [x] 8.2 Create docs/training/curriculum-guide.md
- [x] 8.3 Create docs/training/benchmark-format.md
- [x] 8.4 Create docs/training/dashboard-guide.md

## 9. Integration & Fixes
- [x] 9.1 Fix Dashboard Orleans client integration
- [x] 9.2 Add SignalR broadcasting from AgentRunnerGrain
- [x] 9.3 Register AgentDashboardHub context in Orleans silo
- [x] 9.4 Enhance dashboard SignalR connection handling

## 10. Testing
- [x] 10.1 Create AgentTelemetryTests
- [x] 10.2 Create CurriculumGeneratorTests
- [x] 10.3 Create BenchmarkLibraryTests
- [x] 10.4 Create PerformanceAnalyzerTests
- [x] 10.5 Create AgentTrainingIntegrationTests

