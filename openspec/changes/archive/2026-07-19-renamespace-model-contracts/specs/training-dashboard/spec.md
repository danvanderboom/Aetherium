## MODIFIED Requirements

### Requirement: Dashboard References Only Shared Contracts
The dashboard project SHALL reference only `Aetherium.Model` (plus framework/NuGet packages) among solution projects — neither `Aetherium.Server` nor `WorldGenCLI`, directly or transitively. All contracts the dashboard consumes (agent telemetry/analysis, interest profiles, replay payloads, training curriculum/benchmark, quest definitions, and PCG models) SHALL live in `Aetherium.Model` under `Aetherium.Model.*` namespaces (`Aetherium.Model.Telemetry`, `.Analysis`, `.Training`, `.Narrative`, `.Pcg`) — no contract retains an `Aetherium.Server.*` or `WorldGenCLI.*` namespace.

#### Scenario: Dashboard builds against only the shared-contracts assembly
- **WHEN** the dashboard project is built
- **THEN** it compiles with a single solution project reference (`Aetherium.Model`), and no `Aetherium.Server` or `WorldGenCLI` reference is present on its dependency graph

#### Scenario: Shared contract namespaces match their assembly
- **WHEN** a contract type lives in `Aetherium.Model` for clients to consume
- **THEN** its namespace is under `Aetherium.Model.*`, while the producing logic stays in `Aetherium.Server`/`WorldGen` under its own namespace

#### Scenario: A shared contract carries no engine coupling
- **WHEN** a contract type is placed in `Aetherium.Model` for the dashboard to consume
- **THEN** it does not reference engine types from `Aetherium.Core`/`Aetherium.Server` (e.g. the replay payload omits the live `World` object), so the shared-contracts assembly stays free of engine dependencies
