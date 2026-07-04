## ADDED Requirements

### Requirement: Benchmark Catalog Page
The Benchmark Comparison page SHALL list the available benchmark scenarios from the benchmark library, with a category filter, and SHALL allow looking up an agent's overall performance for comparison.

#### Scenario: Catalog lists benchmark scenarios
- **WHEN** the Benchmark Comparison page loads
- **THEN** it shows each benchmark's name, description, categories, difficulty, and success-criteria type

#### Scenario: Category filter narrows the catalog
- **WHEN** a category is selected
- **THEN** only benchmarks in that category are shown; selecting "All categories" shows every benchmark

#### Scenario: Agent performance lookup
- **WHEN** an agent id is entered and loaded
- **THEN** the agent's overall telemetry (success rate, total steps, action counts) is shown, or an empty state when no telemetry exists

### Requirement: Curriculum Progress Page
The Curriculum Progress page SHALL display an agent's progression through its curriculum, read from the curriculum progression grain keyed by agent id.

#### Scenario: Progress shown for an agent
- **WHEN** an agent id is entered and loaded
- **THEN** the page shows the curriculum id, current stage, completed/total stages with a progress bar, run totals, current success rate, and a per-stage progress table

#### Scenario: No progression for an agent
- **WHEN** the agent has no curriculum progression (or the cluster is unreachable)
- **THEN** the page shows an empty state rather than throwing

### Requirement: Replay Viewer Detail
The Replay Viewer page SHALL, in addition to listing an agent's failed-run ids, retrieve and display a selected run's stored replay data.

#### Scenario: Viewing a failed run shows its replay
- **WHEN** a failed run's "View" action is triggered
- **THEN** the stored replay JSON for that run is fetched and shown in a detail pane

#### Scenario: Missing replay shows a warning
- **WHEN** a selected run's replay cannot be retrieved
- **THEN** the page shows a warning state instead of blank or an error
