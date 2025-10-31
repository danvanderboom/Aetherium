# Agent System Documentation

This guide covers the AI agent system for Console Game, including the extensible tool system, LLM-driven agents powered by LM Studio (phi-4), and heuristic-based fallback agents.

## Overview

The agent system allows NPCs (non-player characters) to autonomously explore the game world, pick up items, open doors, and complete objectives using a comprehensive extensible tool system. Agents can use either:
- **LLM-driven decisions**: Powered by local LLMs via LM Studio (phi-4 model) with OpenAI function calling support
- **Heuristic fallback**: Simple rule-based behavior (always works, no LLM required)

### New: Extensible Tool System

The agent system now features a fully extensible tool architecture with:
- **26+ discoverable tools** organized into categories (movement, interaction, vision, world-building)
- **Profile-based access control** with predefined profiles (Explorer, Player, WorldBuilder, Admin, etc.)
- **OpenAI function calling** support for advanced LLM integration
- **Dual API support** for both human players (SignalR) and AI agents (Orleans)
- **Dynamic tool discovery** via reflection - new tools are automatically available
- **CLI tool management** commands for inspection and debugging

**📖 For detailed architecture and usage, see: [Tool System Documentation](TOOLS.md)**

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

# List available tools
agentcli tools list
agentcli tools list --profile explorer
agentcli tools list --category movement

# Get tool details
agentcli tools describe move

# View tool categories
agentcli tools categories

# Test a tool execution
agentcli tools test move --session-id <sessionId> --args '{"direction":"forward"}'

# List agent profiles
agentcli tools profile list
agentcli tools profile show worldbuilder

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

### Tool Management Commands

```powershell
# List all available tools
agentcli tools list

# Filter tools by profile
agentcli tools list --profile explorer
agentcli tools list --profile worldbuilder

# Filter tools by category
agentcli tools list --category movement
agentcli tools list --category interaction

# Get detailed information about a specific tool
agentcli tools describe move
agentcli tools describe pickup

# List all tool categories
agentcli tools categories

# Test tool execution (requires active session)
agentcli tools test move --session-id <sessionId> --args '{"direction":"forward"}'
agentcli tools test pickup --session-id <sessionId> --args '{"targetEntityId":"entity-123"}'
```

### Profile Management Commands

```powershell
# List all predefined agent profiles
agentcli tools profile list

# Show details of a specific profile
agentcli tools profile show explorer
agentcli tools profile show worldbuilder
agentcli tools profile show admin
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
2. **NEW:** Receive dynamic list of available tools based on agent profile
3. Send perception + tool definitions to LLM (supports OpenAI function calling format)
4. Receive decision (either OpenAI tool_calls or simple JSON: `{"action": "move", "args": {"direction": "forward"}}`)
5. Execute the tool via the extensible tool system
6. Rate limited to 10 requests/second
7. Fall back to "move forward" on errors

**Available Tool Categories:**
- **Movement** (4 tools): move, rotate, changelevel, jumptolocation
- **Interaction** (5 tools): pickup, drop, use, open, close
- **Vision** (4 tools): toggledirectionalvision, setfov, setlightingmode, setvisionmode
- **World-Building** (13 tools): Entity/terrain/map/narrative management (for WorldBuilder agents)

**Note:** Tool availability depends on the agent's profile. Use `agentcli tools list --profile <profile>` to see which tools are available for each profile.

### Heuristic Agents

When `AGENT_LLM_ENABLED=0`, agents use simple heuristic:
1. Try to move forward
2. If blocked, try to move right (turn right)
3. Repeat

## Architecture

### Components

- **`AgentToolRegistry`**: Central registry for all agent tools
  - Automatic tool discovery via reflection
  - DI-based tool instantiation
  - Category and capability filtering
  - Profile-based access control

- **`IAgentTool`**: Interface for all agent tools
  - `ExecuteAsync`: Tool execution logic
  - `GetParameterSchema`: OpenAI-compatible parameter definitions
  - `Categories`: Tool categorization
  - `RequiredCapabilities`: Security capabilities

- **`AgentToolProfile`**: Profile-based access control
  - Predefined profiles: Explorer, Player, WorldBuilder, NarrativeDesigner, Admin
  - Category-based and capability-based filtering
  - Runtime profile selection

- **`MicrosoftAgentAdapter`**: Handles LLM API communication
  - OpenAI-compatible chat completions with function calling support
  - Dynamic tool schema generation
  - Dual format parsing (OpenAI tool_calls + simple JSON)
  - Rate limiting (10 req/sec, configurable concurrency)
  - Error handling with fallback actions
  - Timeout protection (10 seconds)

- **`AgentRunnerGrain`**: Orleans grain for agent orchestration
  - Attaches to game sessions
  - Executes steps using tool system
  - Profile-aware tool filtering
  - Tracks status and error counts
  - Supports continuous execution with delay

- **`GameManagementGrain`**: Provides gameplay control APIs
  - `ListAvailableToolsAsync`: Query available tools for a profile
  - `ExecuteToolAsync`: Execute a tool with provided arguments
  - Legacy action methods (deprecated, use tool system)
  - `GetPerceptionAsync`: Returns JSON perception data

- **`GameHub`**: SignalR hub for human players
  - `ExecuteTool`: Unified tool execution API
  - `ListAvailableTools`: Query tools for current player
  - Legacy methods (marked `[Obsolete]`)

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

- **[Tool System Documentation](TOOLS.md)** - Complete architecture and usage guide for the extensible tool system
- **[Tool Implementation Summary](FINAL_SUMMARY.md)** - Implementation status and metrics
- [OpenSpec Agents Guide](../../openspec/AGENTS.md) - Development workflow
- [Agent Prompt Template](../../Aetherium.Server/Prompts/agent_explorer.md) - LLM prompt definition

## Adding New Tools

To add a new tool to the system:

1. **Create a new class** implementing `IAgentTool`:
```csharp
[AgentTool("mytool", "Description of what this tool does")]
public class MyTool : IAgentTool
{
    public string ToolId => "mytool";
    public string Description => "Description of what this tool does";
    public IEnumerable<string> Categories => new[] { "movement", "custom" };
    public IEnumerable<string> RequiredCapabilities => new[] { "basic_movement" };
    
    public ToolParameterSchema GetParameterSchema()
    {
        return new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["param1"] = new() { Type = "string", Description = "Parameter description" }
            },
            Required = new List<string> { "param1" }
        };
    }
    
    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
    {
        // Tool implementation
        return ToolExecutionResult.Ok("Success!");
    }
}
```

2. **That's it!** The tool is automatically:
   - Discovered at startup via reflection
   - Available to appropriate agent profiles
   - Exposed to LLMs with auto-generated schemas
   - Accessible via CLI commands

3. **Test it**:
```powershell
# Verify tool was discovered
agentcli tools list | Select-String mytool

# Get tool details
agentcli tools describe mytool

# Test tool execution (requires active session)
agentcli tools test mytool --session-id <sessionId> --args '{"param1":"value1"}'
```

For more details, see [TOOLS.md](TOOLS.md).


