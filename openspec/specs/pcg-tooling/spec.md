# pcg-tooling Specification

## Purpose
TBD - created by archiving change expand-pcg-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Seed CLI and Repro Recipes
A CLI SHALL allow specifying seed, generator, and parameters to reproduce worlds and export artifacts.

#### Scenario: Repro via CLI
- WHEN running the CLI with a seed
- THEN the same map and metrics are produced and saved

### Requirement: Visual Debug Overlays
Developers SHALL be able to enable overlays for generation phases (rooms, corridors, biomes, roads, secrets, keys/locks).

#### Scenario: Phase overlays toggled
- WHEN overlays are enabled
- THEN each phase’s artifacts can be displayed independently

### Requirement: Metrics Export
The system MUST export metrics to logs and optional JSON for automated analysis.

#### Scenario: JSON metrics emitted
- WHEN generation completes
- THEN a JSON metrics file is written

### Requirement: REST API for Remote Generation
The WorldGenCLI SHALL expose a REST API for remote generation requests when started with `--serve`.

#### Scenario: Start API server
- **WHEN** WorldGenCLI is started with `--serve --port 5000`
- **THEN** an HTTP server listens on the specified port and accepts generation requests

#### Scenario: List available generators
- **WHEN** a GET request is made to `/api/generators`
- **THEN** a JSON array of generator metadata (id, name, version) is returned

#### Scenario: Get constraint schema
- **WHEN** a GET request is made to `/api/generators/{id}/constraints-schema`
- **THEN** a JSON Schema describing the generator's parameters with types, defaults, min/max, and descriptions is returned

#### Scenario: Generate world via API
- **WHEN** a POST request is made to `/api/generate` with a `WorldGenerationRequest` body
- **THEN** the world is generated and a response containing metrics, validation, errors, and `MapRenderDto` is returned

#### Scenario: A/B test generation
- **WHEN** a POST request is made to `/api/generate/abtest` with parameters including generator, base request, count, and metric selector
- **THEN** multiple candidate worlds are generated and returned with metrics sorted by the selected metric

### Requirement: Constraint Parameter Metadata
Generators SHALL support parameter metadata via attributes for automatic schema generation.

#### Scenario: Generator with parameter attributes
- **WHEN** a generator class uses `[GeneratorParam]` attributes on parameter properties
- **THEN** the constraint schema builder extracts type, min/max, default, description, and group information

#### Scenario: Schema generation
- **WHEN** a constraint schema is requested for a generator
- **THEN** a complete JSON Schema is generated including all parameter definitions and validation rules

### Requirement: Template Library
The system SHALL support saving and loading PCG configurations as templates.

#### Scenario: Save template
- **WHEN** a POST request is made to `/api/templates` with template name and configuration
- **THEN** the template is persisted to disk under `Data/PCGTemplates/`

#### Scenario: Load template
- **WHEN** a GET request is made to `/api/templates/{name}`
- **THEN** the template configuration is returned

#### Scenario: List templates
- **WHEN** a GET request is made to `/api/templates`
- **THEN** a list of all available template names is returned

#### Scenario: Delete template
- **WHEN** a DELETE request is made to `/api/templates/{name}`
- **THEN** the template file is removed

### Requirement: Enhanced Map Visualization
The system SHALL provide compact map rendering data for preview visualization.

#### Scenario: MapRenderDto generation
- **WHEN** a world is generated
- **THEN** a `MapRenderDto` can be created containing width, height, compact tile array, and overlay data (rooms, corridors, anchors, regions)

#### Scenario: Overlay toggles
- **WHEN** visualization overlays are requested
- **THEN** the `MapRenderDto` includes structured overlay data for rooms, corridors, anchors, and regions

