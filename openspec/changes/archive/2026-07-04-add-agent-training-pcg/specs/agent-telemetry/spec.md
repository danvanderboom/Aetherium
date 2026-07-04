## ADDED Requirements

### Requirement: Performance Snapshot Collection
The system SHALL collect performance snapshots for each agent step, including action type, success status, decision latency, and perception complexity.

#### Scenario: Snapshot recorded on each step
- **WHEN** AgentRunnerGrain executes StepAsync
- **THEN** a PerformanceSnapshot MUST be created with timestamp, step number, action type, success status, latency, and perception complexity
- **AND** the snapshot MUST be recorded via AgentTelemetryGrain.RecordSnapshotAsync

#### Scenario: Snapshot includes all metrics
- **WHEN** a performance snapshot is recorded
- **THEN** it MUST include: Timestamp, StepNumber, AgentId, SessionId, ActionType, ActionSummary, ActionSucceeded, DecisionLatencyMs, PerceptionComplexity, and Metadata

### Requirement: Telemetry Grain Storage
The system SHALL provide an Orleans grain for storing and retrieving agent performance telemetry data.

#### Scenario: Telemetry grain stores snapshots
- **WHEN** AgentTelemetryGrain.RecordSnapshotAsync is called
- **THEN** the snapshot MUST be stored in the grain's state
- **AND** snapshots MUST be retrievable via GetSnapshotsAsync

#### Scenario: Telemetry grain provides analysis
- **WHEN** AgentTelemetryGrain.GetAnalysisAsync is called
- **THEN** it MUST return a PerformanceAnalysis calculated from stored snapshots
- **AND** the analysis MUST include success rate, average latency, action type statistics, identified weaknesses, and recommendations

#### Scenario: Telemetry grain retrieves snapshots by range
- **WHEN** GetSnapshotsInRangeAsync is called with start and end times
- **THEN** it MUST return all snapshots within the time range
- **AND** snapshots MUST be ordered by timestamp

### Requirement: Replay Storage
The system SHALL store failed agent runs with world state and action sequences to enable replay analysis.

#### Scenario: Failed run stored automatically
- **WHEN** an agent run fails (3+ consecutive failed actions)
- **THEN** a ReplayData object MUST be created with agent ID, session ID, failure reason, initial world state, and action sequence
- **AND** the replay MUST be stored via AgentTelemetryGrain.RecordFailedRunAsync
- **AND** a replay ID MUST be returned for later retrieval

#### Scenario: Replay includes full context
- **WHEN** a replay is stored
- **THEN** it MUST include: ReplayId, AgentId, SessionId, CreatedAt, BenchmarkName, FailureReason, TotalSteps, InitialWorldState, Steps (with action type, perception JSON, timestamps), and Metadata

#### Scenario: Replays retrievable by agent
- **WHEN** GetFailedRunIdsAsync is called with an agent ID
- **THEN** it MUST return list of replay IDs for that agent's failed runs
- **AND** results MAY be limited by count parameter

### Requirement: Performance Analysis
The system SHALL analyze performance snapshots to identify weaknesses and generate recommendations.

#### Scenario: Analysis calculates metrics
- **WHEN** PerformanceAnalyzer.Analyze is called with snapshots
- **THEN** it MUST calculate: total steps, successful/failed actions, success rate, average decision latency, average perception complexity
- **AND** it MUST group statistics by action type

#### Scenario: Analysis identifies weaknesses
- **WHEN** performance analysis identifies low success rate (<50% with >10 steps)
- **THEN** it MUST add weakness to IdentifiedWeaknesses list
- **AND** weakness description MUST indicate the issue (e.g., "Low overall success rate (45%)")

#### Scenario: Analysis generates recommendations
- **WHEN** weaknesses are identified
- **THEN** corresponding recommendations MUST be added to Recommendations list
- **AND** recommendations MUST suggest actionable improvements (e.g., "Consider reducing difficulty or providing simpler training scenarios")

#### Scenario: Analysis calculates trends
- **WHEN** snapshots contain >=10 entries
- **THEN** it MUST compare first half to second half
- **AND** it MUST calculate trend metrics (success rate trend, latency trend)
- **AND** negative trends MUST trigger recommendations

### Requirement: AgentRunnerGrain Integration
The system SHALL automatically collect telemetry during agent execution.

#### Scenario: Telemetry collected on each step
- **WHEN** AgentRunnerGrain.StepAsync executes an action
- **THEN** it MUST create a PerformanceSnapshot with action details
- **AND** it MUST call AgentTelemetryGrain.RecordSnapshotAsync
- **AND** it MUST estimate perception complexity from perception JSON

#### Scenario: Failed actions trigger replay tracking
- **WHEN** an action fails
- **THEN** the failure MUST be added to current replay data
- **AND** if 3+ consecutive failures occur, the replay MUST be stored via RecordFailedRunAsync

#### Scenario: Telemetry available via GetTelemetryAsync
- **WHEN** AgentRunnerGrain.GetTelemetryAsync is called
- **THEN** it MUST return PerformanceAnalysis from AgentTelemetryGrain
- **AND** analysis MUST reflect current agent performance state

