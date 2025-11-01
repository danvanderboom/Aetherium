# Dashboard Guide

This guide explains how to use the Blazor dashboard for monitoring agent training.

## Accessing the Dashboard

1. Start the dashboard:

```powershell
cd Aetherium.Dashboard
dotnet run
```

2. Navigate to http://localhost:5001 in your browser.

## Pages

### Overview

The home page shows:
- Active agents count
- Active sessions count
- Active worlds count
- Total worlds count

Stats auto-refresh every 5 seconds.

### Agent Monitor

Real-time monitoring of a specific agent:

1. Enter the agent ID
2. Click "Subscribe" to start receiving updates
3. View:
   - Success rate
   - Total steps
   - Average decision latency
   - Perception complexity
   - Identified weaknesses
   - Recommendations

The page updates automatically via SignalR.

### Performance Analytics

Detailed performance analysis:

1. Enter the agent ID
2. Click "Load Analytics"
3. View:
   - Overall performance metrics
   - Action type statistics
   - Success/failure rates per action
   - Average latency per action type

### Curriculum Progress

Track agent progression through training curricula:

- Current stage
- Completion status
- Prerequisites met
- Performance per stage

### Benchmark Comparison

Compare agent performance across benchmarks:

- Success rates per benchmark
- Average steps
- Performance trends
- Regression detection

### Replay Viewer

View failed agent runs:

1. Enter the agent ID
2. Click "Load Replays"
3. Select a replay ID to view
4. Step through the replay sequence

### Worlds & Sessions

Manage game worlds, sessions, and agent attachments:

#### Worlds Tab
- View all worlds with status, player count, and creation time
- Create new worlds (requires API key)
- Shutdown worlds (requires API key)

#### Sessions Tab
- View all active sessions with connection info and attached agents
- Stop sessions (requires API key)
- Attach agents to sessions (requires API key)

#### Agents Tab
- View all agents with their current status
- See agent runner assignments and session attachments
- Monitor agent activity (steps, last action, running status)

**Note:** Control actions (create, stop, attach/detach) require an API key to be configured. See Configuration section below.

## Real-Time Updates

The dashboard uses SignalR for real-time telemetry updates. When you subscribe to an agent:

- Performance snapshots are sent as they occur
- Charts update automatically
- Metrics refresh in real-time

## API Integration

The dashboard uses REST API endpoints:

- `/api/agenttelemetry/{agentId}/analysis` - Performance analysis
- `/api/agenttelemetry/{agentId}/snapshots` - Historical snapshots
- `/api/benchmark` - Benchmark scenarios
- `/api/curriculum` - Training curricula
- `/api/management/worlds` - World management
- `/api/management/sessions` - Session management
- `/api/management/agents` - Agent management
- `/api/management/stats` - Summary statistics

## Configuration

### API Key Authentication

To enable control actions (create worlds, stop sessions, attach agents), configure an API key:

**Server Configuration** (`appsettings.json`):
```json
{
  "Dashboard": {
    "ApiKey": "your-secret-api-key-here"
  }
}
```

**Dashboard Configuration** (`appsettings.json`):
```json
{
  "ManagementApi": {
    "ApiKey": "your-secret-api-key-here",
    "BaseUrl": "api/management"
  }
}
```

**Note:** 
- In development mode, control actions are allowed without an API key
- In production, control actions require a valid API key
- Read-only operations (viewing worlds, sessions, agents) work without an API key

## Troubleshooting

### No Data Showing

- Verify the agent is running
- Check that telemetry collection is enabled
- Ensure Orleans grains are accessible

### SignalR Connection Issues

- Verify the dashboard is running
- Check firewall settings
- Ensure SignalR hub is mapped correctly

### Performance Issues

- Limit the number of subscribed agents
- Use snapshot limits when loading history
- Consider pagination for large datasets

### Control Actions Disabled

If you see "Control Actions Disabled" warnings:

- Verify API key is configured in `appsettings.json`
- Ensure the API key matches between server and dashboard configurations
- In development mode, control actions work without an API key
- Check that the API key header (`X-Dashboard-ApiKey`) is being sent correctly

