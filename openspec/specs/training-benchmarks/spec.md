# training-benchmarks Specification

## Purpose
Defines benchmark scenarios for evaluating agents: JSON scenario definitions with deterministic recipes and success criteria, a benchmark library, request/variation/edge-case generation, and WorldGenCLI integration.

## Requirements
### Requirement: Benchmark Scenario Definition
The system SHALL support benchmark scenario definitions in JSON format with recipes and success criteria.

#### Scenario: Benchmark loaded from JSON
- **WHEN** a benchmark JSON file is loaded from Data/Benchmarks/
- **THEN** it MUST be parsed into BenchmarkScenario with benchmarkId, name, description, categories, difficulty, version, recipe, and successCriteria
- **AND** benchmark MUST be registered in BenchmarkLibrary

#### Scenario: Benchmark recipe defines generation
- **WHEN** a BenchmarkRecipe is created
- **THEN** it MUST include: Generator, Template, Seed, GeneratorVersion, Width, Height, Levels, and Parameters dictionary
- **AND** recipe MUST be sufficient to generate a deterministic world

#### Scenario: Success criteria defines evaluation
- **WHEN** SuccessCriteria is defined
- **THEN** it MUST specify Type (ReachGoal, CollectItems, SurviveTurns, CompleteWithinLimits)
- **AND** Type-specific fields MUST be set (GoalLocation for ReachGoal, RequiredItems for CollectItems, etc.)
- **AND** maxSteps and maxTimeSeconds MAY be specified

### Requirement: Benchmark Library Management
The system SHALL provide a library for loading and managing benchmark scenarios.

#### Scenario: Benchmarks loaded from directory
- **WHEN** BenchmarkLibrary.LoadBenchmarks is called
- **THEN** it MUST scan Data/Benchmarks/ for JSON files
- **AND** each valid JSON file MUST be parsed and registered
- **AND** loading errors MUST be logged but MUST not stop processing

#### Scenario: Benchmark retrieved by ID
- **WHEN** BenchmarkLibrary.GetBenchmark is called with benchmark ID
- **THEN** it MUST return the benchmark scenario if found
- **OR** it MUST return null if not found

#### Scenario: Benchmarks filtered by category
- **WHEN** BenchmarkLibrary.GetBenchmarksByCategory is called with category
- **THEN** it MUST return all benchmarks with that category in their Categories list
- **AND** matching MUST be case-insensitive

### Requirement: Benchmark Generation
The system SHALL generate world generation requests from benchmark recipes and create variations.

#### Scenario: Request generated from recipe
- **WHEN** BenchmarkGenerator.GenerateRequest is called with BenchmarkRecipe
- **THEN** it MUST create a WorldGenerationRequest with recipe parameters
- **AND** request MUST have IsTrainingMode set to true
- **AND** request Template MUST match recipe template enum

#### Scenario: Variations generated from base
- **WHEN** BenchmarkGenerator.GenerateVariations is called with base benchmark and count
- **THEN** it MUST create count variations with different seeds
- **AND** each variation MUST have unique benchmarkId (baseId_var_N)
- **AND** recipe seeds MUST be offset by variation index

#### Scenario: Edge case generated from failure pattern
- **WHEN** BenchmarkGenerator.GenerateEdgeCase is called with failure pattern
- **THEN** recipe parameters MUST be adjusted based on pattern
- **AND** navigation failures MUST increase map size and reduce branching
- **AND** key-lock failures MUST increase chain depth
- **AND** trap failures MUST increase trap density
- **AND** perception failures MUST reduce map size and increase entity density

### Requirement: Benchmark CLI Integration
The system SHALL support generating benchmarks via WorldGenCLI.

#### Scenario: Benchmark loaded from library
- **WHEN** WorldGenCLI is invoked with --benchmark <benchmarkId>
- **THEN** BenchmarkLibrary MUST load the benchmark
- **AND** benchmark recipe MUST be used to generate WorldGenerationRequest
- **AND** generation MUST proceed with benchmark parameters

#### Scenario: Missing benchmark handled
- **WHEN** --benchmark is specified with non-existent ID
- **THEN** CLI MUST exit with error code 1
- **AND** error message MUST indicate benchmark not found

