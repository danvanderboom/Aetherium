# training-dashboard Specification

## Purpose
Defines the Blazor Server training dashboard: real-time telemetry broadcasting over SignalR, REST API controllers for training data, monitoring pages, and Orleans client integration for reading grain state.

## Requirements
### Requirement: Blazor Server Dashboard
The system SHALL provide a Blazor Server web application for monitoring agent training in real-time.

#### Scenario: Dashboard accessible via HTTP
- **WHEN** Aetherium.Dashboard is started
- **THEN** it MUST be accessible via configured port (default: 5001)
- **AND** it MUST serve Blazor Server pages with SignalR connection

#### Scenario: Dashboard pages render correctly
- **WHEN** user navigates to dashboard pages
- **THEN** pages MUST render without errors
- **AND** navigation menu MUST allow access to: Overview, Agent Monitor, Performance Analytics, Curriculum Progress, Benchmark Comparison, Replay Viewer

### Requirement: Real-Time Telemetry Broadcasting
The system SHALL broadcast agent telemetry updates in real-time via SignalR.

#### Scenario: SignalR hub receives subscriptions
- **WHEN** AgentDashboardHub.SubscribeToAgent is called with agent ID
- **THEN** connection MUST be added to agent-specific group
- **AND** current telemetry MUST be sent immediately if available

#### Scenario: Telemetry updates broadcast
- **WHEN** AgentRunnerGrain records a performance snapshot
- **THEN** AgentDashboardHub MUST broadcast TelemetryUpdate event to agent's group
- **AND** broadcast MUST include PerformanceAnalysis data

#### Scenario: Subscribers receive updates
- **WHEN** dashboard page subscribes to agent telemetry
- **THEN** it MUST receive TelemetryUpdate events via SignalR
- **AND** page MUST update UI automatically when events arrive

### Requirement: REST API Controllers
The system SHALL provide REST API endpoints for accessing training data.

#### Scenario: Telemetry analysis retrieved
- **WHEN** GET /api/agenttelemetry/{agentId}/analysis is called
- **THEN** it MUST return PerformanceAnalysis for the agent
- **OR** it MUST return 404 if no telemetry exists

#### Scenario: Snapshots retrieved with limits
- **WHEN** GET /api/agenttelemetry/{agentId}/snapshots?limit=N is called
- **THEN** it MUST return up to N most recent snapshots
- **AND** snapshots MUST be ordered by timestamp

#### Scenario: Benchmarks listed
- **WHEN** GET /api/benchmark is called
- **THEN** it MUST return list of all available benchmarks
- **AND** benchmarks MUST include all required fields

#### Scenario: Curriculum retrieved
- **WHEN** GET /api/curriculum/{curriculumId} is called
- **THEN** it MUST return CurriculumDefinition for the curriculum
- **OR** it MUST return 404 if curriculum not found

### Requirement: Dashboard Pages
The system SHALL provide specific pages for different aspects of agent training.

#### Scenario: Overview page displays summary
- **WHEN** user navigates to Overview page
- **THEN** it MUST display quick stats: active agents, running benchmarks, completed runs, failed runs
- **AND** stats MAY be placeholders until backend integration

#### Scenario: Agent Monitor page shows real-time data
- **WHEN** user enters agent ID and subscribes
- **THEN** page MUST display current telemetry: success rate, total steps, average latency, perception complexity
- **AND** page MUST display identified weaknesses and recommendations
- **AND** page MUST update automatically via SignalR

#### Scenario: Performance Analytics page shows detailed metrics
- **WHEN** user enters agent ID and loads analytics
- **THEN** page MUST display: overall performance table, action type statistics table
- **AND** tables MUST show success rates, counts, and latencies per action type

#### Scenario: Replay Viewer page lists failed runs
- **WHEN** user enters agent ID and loads replays
- **THEN** page MUST display list of failed run IDs
- **AND** each replay MUST show replay ID and have View button

### Requirement: Orleans Client Integration
The system SHALL connect Dashboard to Orleans cluster to access grain data.

#### Scenario: Orleans client configured
- **WHEN** Dashboard Program.cs is configured
- **THEN** IClusterClient MUST be registered in dependency injection
- **AND** client MUST connect to same Orleans cluster as Server
- **AND** connection failures MUST be handled gracefully

#### Scenario: Telemetry service uses client
- **WHEN** AgentTelemetryService methods are called
- **THEN** service MUST use injected IClusterClient to get grains
- **AND** grain calls MUST handle exceptions gracefully

