## ADDED Requirements

### Requirement: Dashboard Depends Only On Shared Contracts
The dashboard SHALL obtain the agent telemetry/analysis, training (curriculum/benchmark), and quest-definition contracts it consumes from the shared `Aetherium.Model` assembly, and SHALL NOT declare a direct project reference to `Aetherium.Server`. Grain implementations and server-side logic remain in `Aetherium.Server`; only the shared contract types (grain interfaces + DTOs) are shared.

#### Scenario: Dashboard builds without a direct Server reference
- **WHEN** the dashboard project is built
- **THEN** it compiles against `Aetherium.Model` for its telemetry, analysis, training, and quest-definition contracts, with no direct `Aetherium.Server` project reference

#### Scenario: Contracts are shared, logic is not
- **WHEN** a contract type (a grain interface or DTO) is used by both the dashboard and the server
- **THEN** the type is defined in `Aetherium.Model`, while its producing logic (analyzers, grain implementations) stays in `Aetherium.Server`
