# Agent Training System

The Agent Training System provides comprehensive infrastructure for training and evaluating AI agents in procedurally generated game environments.

## Overview

The training system includes:

- **Telemetry**: Real-time performance tracking and analysis
- **Curriculum**: Progressive training curricula with automatic difficulty adjustment
- **Benchmarks**: Standardized test scenarios for evaluating agent performance
- **Dashboard**: Blazor web interface for monitoring training progress
- **Replays**: Storage and visualization of failed agent runs

## Components

### 1. Telemetry System

Tracks agent performance at each step:

- Action success/failure rates
- Decision latency
- Perception complexity
- Action type statistics
- Failure pattern analysis

**Location**: `Aetherium.Server/Agents/Telemetry/`

### 2. Curriculum System

Provides structured training paths:

- Manual curricula defined in JSON
- Automatic curriculum generation based on performance
- Progressive difficulty adjustment
- Prerequisites and completion criteria

**Location**: `Aetherium.Server/WorldGen/Training/`

**Example Curricula**: `Data/Curricula/`

### 3. Benchmark Scenarios

Standard test scenarios for agent evaluation:

- Navigation tests
- Combat scenarios
- Puzzle challenges
- Custom benchmarks

**Location**: `Aetherium.Server/WorldGen/Training/`

**Example Benchmarks**: `Data/Benchmarks/`

### 4. Dashboard

Blazor Server web interface for:

- Real-time agent monitoring
- Performance analytics
- Curriculum progress tracking
- Benchmark comparison
- Replay visualization

**Location**: `Aetherium.Dashboard/`

**Access**: http://localhost:5001 (when running)

## Quick Start

### 1. Start the Dashboard

```powershell
cd Aetherium.Dashboard
dotnet run
```

Navigate to http://localhost:5001 to view the dashboard.

### 2. Start Training an Agent

```powershell
# Attach agent to a session
agentcli agent attach <sessionId> --agent <agentId> --runner <runnerId>

# Start a training curriculum
agentcli train start <agentId> beginner-dungeon

# Monitor progress
agentcli train status <agentId>
```

### 3. Run a Benchmark

```powershell
# Generate benchmark world
worldgencli --benchmark navigation-basic --output benchmark.json

# Run agent on benchmark
agentcli train benchmark navigation-basic <agentId>
```

### 4. View Telemetry

Access the dashboard or use the REST API:

```powershell
# Get agent telemetry
curl http://localhost:5000/api/agenttelemetry/{agentId}/analysis
```

## API Endpoints

### Agent Telemetry

- `GET /api/agenttelemetry/{agentId}/analysis` - Get performance analysis
- `GET /api/agenttelemetry/{agentId}/snapshots` - Get performance snapshots
- `GET /api/agenttelemetry/{agentId}/failed-runs` - Get failed run IDs

### Benchmarks

- `GET /api/benchmark` - List all benchmarks
- `GET /api/benchmark/{benchmarkId}` - Get benchmark details
- `POST /api/benchmark` - Create new benchmark
- `POST /api/benchmark/{benchmarkId}/variations` - Generate variations

### Curricula

- `GET /api/curriculum` - List all curricula
- `GET /api/curriculum/{curriculumId}` - Get curriculum details
- `POST /api/curriculum` - Create/update curriculum

## See Also

- [Curriculum Guide](curriculum-guide.md) - Creating custom curricula
- [Benchmark Format](benchmark-format.md) - Benchmark JSON schema
- [Dashboard Guide](dashboard-guide.md) - Using the Blazor dashboard

