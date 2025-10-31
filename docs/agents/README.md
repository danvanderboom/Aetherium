# Agent System Documentation

This guide covers the AI agent system for Console Game, including LLM-driven agents powered by LM Studio (phi-4) and heuristic-based fallback agents.

## Overview

The agent system allows NPCs (non-player characters) to autonomously explore the game world, pick up items, open doors, and complete objectives. Agents can use either:
- **LLM-driven decisions**: Powered by local LLMs via LM Studio (phi-4 model)
- **Heuristic fallback**: Simple rule-based behavior (always works, no LLM required)

## Quick Start

### 1. Start the Game Server

```powershell
cd Aetherium.Server
dotnet run
```

### 2. Enable LLM Agents (Optional)

To use LLM-driven agents, you need LM Studio running with phi-4 model:

1. Install and start [LM Studio](https://lmstudio.ai/)
2. Download and load the `phi-4` model
3. Start the local server (default: `http://localhost:1234`)
4. Enable OpenAI-compatible API in LM Studio settings

### 3. Set Environment Variables

For LLM agents, set these environment variables before starting the server:

```powershell
$env:AGENT_LLM_ENABLED="1"
$env:OPENAI_API_BASE="http://localhost:1234/v1"
$env:OPENAI_API_KEY="lm-studio"
$env:AGENT_MODEL="phi-4"
```

### 4. Use AgentCLI to Control Agents

```powershell
# List active game sessions
agentcli mgmt sessions

# Attach an agent to a session
agentcli agent attach <sessionId> --agent agent-1 --runner runner-1

# Run the agent (with max steps and delay)
agentcli agent run runner-1 --max-steps 50 --delay 200

# Check agent status
agentcli agent status runner-1

# Stop the agent
agentcli agent stop runner-1
```

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `AGENT_LLM_ENABLED` | `0` | Enable LLM-driven agents (`1`) or use heuristic (`0`) |
| `OPENAI_API_BASE` | `http://localhost:1234/v1` | LM Studio API endpoint |
| `OPENAI_API_KEY` | `lm-studio` | API key (LM Studio accepts any value) |
| `AGENT_MODEL` | `phi-4` | Model name to use |
| `AGENT_DEBUG` | `0` | Enable debug logging (`1`) |
| `AGENT_LLM_MAX_CONCURRENT` | `2` | Maximum concurrent LLM requests |

### Policy Switching

You can switch between LLM and heuristic policies at runtime using the CLI:

```powershell
# Use LLM policy
agentcli agent policy set llm

# Use heuristic policy
agentcli agent policy set heuristic

# Check current policy
agentcli agent policy get

# Enable debug output
agentcli agent debug on

# Disable debug output
agentcli agent debug off
```

**Note**: Policy changes affect new agent steps. Restart agents to apply immediately.

## LM Studio Setup

### 1. Install LM Studio

Download from [lmstudio.ai](https://lmstudio.ai/) and install.

### 2. Download phi-4 Model

1. Open LM Studio
2. Go to "Search" tab
3. Search for "phi-4" or "microsoft/phi-4"
4. Download the model (recommended: 4-bit quantized for lower memory)

### 3. Configure Local Server

1. Go to "Local Server" tab in LM Studio
2. Select the phi-4 model
3. Click "Start Server"
4. Ensure "OpenAI-compatible API" is enabled
5. Default endpoint: `http://localhost:1234/v1`

### 4. Verify Setup

Test the endpoint:

```powershell
$body = @{
    model = "phi-4"
    messages = @(
        @{ role = "user"; content = "say ok" }
    )
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post `
    -Uri "http://localhost:1234/v1/chat/completions" `
    -ContentType "application/json" `
    -Body $body `
    -Headers @{ Authorization = "Bearer lm-studio" }
```

## AgentCLI Commands

### Session Management

```powershell
# List all active game sessions
agentcli mgmt sessions
```

### Agent Runner Commands

```powershell
# Attach agent to a game session
agentcli agent attach <sessionId> [--agent <agentId>] [--runner <runnerId>]

# Execute a single step
agentcli agent step <runnerId>

# Run continuously (with optional limits)
agentcli agent run <runnerId> [--max-steps <N>] [--delay <ms>]

# Stop continuous execution
agentcli agent stop <runnerId>

# Get agent status
agentcli agent status <runnerId>

# Set agent policy (llm|heuristic)
agentcli agent policy set <policy>

# Get current policy
agentcli agent policy get

# Enable/disable debug output
agentcli agent debug <on|off>
```

## Agent Behavior

### LLM-Driven Agents

When `AGENT_LLM_ENABLED=1`, agents:
1. Receive perception data (JSON) containing:
   - Player location and heading
   - Visible entities and items
   - Available affordances (actions)
2. Send perception to LLM with system prompt
3. Receive JSON decision: `{"action": "move", "args": {"direction": "F"}}`
4. Execute the action via game management APIs
5. Rate limited to 10 requests/second
6. Fall back to "move forward" on errors

**Supported Actions:**
- `move {direction}`: Move in direction (F/L/R/B/N/E/S/W)
- `pickup {targetEntityId}`: Pick up an item
- `drop {itemEntityId}`: Drop an item from inventory
- `open {targetEntityId}`: Open a door
- `close {targetEntityId}`: Close a door
- `use {itemEntityId, onEntityId}`: Use an item on another entity (e.g., key on door)

### Heuristic Agents

When `AGENT_LLM_ENABLED=0`, agents use simple heuristic:
1. Try to move forward
2. If blocked, try to move right (turn right)
3. Repeat

## Architecture

### Components

- **`MicrosoftAgentAdapter`**: Handles LLM API communication
  - OpenAI-compatible chat completions
  - Rate limiting (10 req/sec, configurable concurrency)
  - Error handling with fallback actions
  - Timeout protection (10 seconds)

- **`AgentRunnerGrain`**: Orleans grain for agent orchestration
  - Attaches to game sessions
  - Executes steps (LLM or heuristic)
  - Tracks status and error counts
  - Supports continuous execution with delay

- **`GameManagementGrain`**: Provides gameplay control APIs
  - `MoveAsync`, `PickupAsync`, `DropAsync`, `OpenAsync`, `CloseAsync`, `UseAsync`
  - `GetPerceptionAsync`: Returns JSON perception data

### Flow

```
AgentRunnerGrain
  ↓
GetPerceptionAsync (GameManagementGrain)
  ↓
[If LLM enabled]
  ↓
MicrosoftAgentAdapter.DecideAsync
  ↓
LLM API (LM Studio)
  ↓
Parse JSON decision
  ↓
Execute action (GameManagementGrain)
  ↓
Update status & log
```

## Rate Limiting & Telemetry

### Rate Limiting

- **Minimum request interval**: 100ms (10 requests/second max)
- **Concurrent requests**: Configurable via `AGENT_LLM_MAX_CONCURRENT` (default: 2)
- **HTTP timeout**: 10 seconds

### Logging

- **Debug mode** (`AGENT_DEBUG=1`): Logs all requests and responses
- **Error logging**: Always logs HTTP errors, timeouts, and parsing failures
- **Agent runner logs**: Logs step execution, policy used, and action results

## Troubleshooting

### LLM Agents Not Working

1. **Check LM Studio is running**
   ```powershell
   Invoke-WebRequest -UseBasicParsing http://localhost:1234/v1/models
   ```

2. **Verify environment variables**
   ```powershell
   $env:AGENT_LLM_ENABLED
   $env:OPENAI_API_BASE
   $env:AGENT_MODEL
   ```

3. **Check agent status**
   ```powershell
   agentcli agent status runner-1
   ```

4. **Enable debug mode**
   ```powershell
   agentcli agent debug on
   ```

### Agent Not Moving

1. **Check session exists**
   ```powershell
   agentcli mgmt sessions
   ```

2. **Verify agent is attached**
   ```powershell
   agentcli agent status runner-1
   ```

3. **Check perception data** (if debug enabled)

### High Error Count

- LLM may be slow or timing out
- Try reducing `AGENT_LLM_MAX_CONCURRENT`
- Check LM Studio server logs
- Consider using heuristic policy for testing

## Examples

### Example 1: Run Two LLM Agents

```powershell
# Start server with LLM enabled
$env:AGENT_LLM_ENABLED="1"
$env:OPENAI_API_BASE="http://localhost:1234/v1"
cd Aetherium.Server
dotnet run
```

In another terminal:

```powershell
# List sessions to get session ID
agentcli mgmt sessions

# Attach two agents
agentcli agent attach <sessionId> --agent agent-1 --runner runner-1
agentcli agent attach <sessionId> --agent agent-2 --runner runner-2

# Run both agents
agentcli agent run runner-1 --max-steps 100 --delay 200
agentcli agent run runner-2 --max-steps 100 --delay 200

# Monitor status
agentcli agent status runner-1
agentcli agent status runner-2
```

### Example 2: Switch Policies at Runtime

```powershell
# Start with heuristic
agentcli agent policy set heuristic
agentcli agent run runner-1 --max-steps 20

# Switch to LLM
agentcli agent policy set llm
agentcli agent stop runner-1
agentcli agent run runner-1 --max-steps 20

# Switch back to heuristic
agentcli agent policy set heuristic
agentcli agent stop runner-1
agentcli agent run runner-1 --max-steps 20
```

## Prompts

The system prompt is defined in `Aetherium.Server/Prompts/agent_explorer.md`. It instructs the LLM to:
- Output strict JSON only
- Use specific action formats
- Explore systematically
- Find keys and open doors

You can customize this prompt file to change agent behavior.

## Future Enhancements

- [ ] Support for OpenAI API directly (not just LM Studio)
- [ ] Multi-turn conversation context for agents
- [ ] Agent memory/state persistence
- [ ] More sophisticated heuristic policies
- [ ] Agent-to-agent communication
- [ ] Configurable agent goals and objectives

## See Also

- [Client-Server README](../CLIENT_SERVER_README.md) - Architecture overview
- [OpenSpec Agents Guide](../../openspec/AGENTS.md) - Development workflow
- [Agent Prompt Template](../../Aetherium.Server/Prompts/agent_explorer.md) - LLM prompt definition


