# Agent Tool System Documentation

## Overview

The Agent Tool System provides a comprehensive, extensible framework for AI agents and human players to interact with the game world through a unified set of dynamically-discoverable tools.

## Key Features

### 1. **Unified Tool Interface**
- Both AI agents and human players use the same underlying tools
- Tools are discovered via reflection at startup
- Capability-based access control ensures security

### 2. **Dual API Support**
- **Legacy SignalR Methods**: Existing methods like `MovePlayer`, `Pickup`, etc. still work
- **Unified Tool API**: New `ExecuteTool(toolId, args)` method for dynamic tool execution
- Both APIs execute the same underlying tool implementations

### 3. **LLM Format Support**
- **OpenAI Function Calling**: For GPT-4 and compatible models
- **Simple JSON Format**: For models like phi-4 that don't support function calling
- Automatic format detection based on model name

### 4. **Tool Profiles**
- **Explorer**: Basic navigation tools only
- **FullAccess**: All player-level tools (movement, inventory, interaction, vision)
- **WorldBuilder**: Entity and terrain manipulation tools
- **NarrativeDesigner**: Quest and narrative tools
- **Admin**: Unrestricted access to all tools

## Available Tools

### Movement Tools (Category: `movement`, `navigation`)
- **`move`** - Move in relative (F/L/R/B) or absolute (N/E/S/W) directions
- **`rotate`** - Rotate view by degrees or clockwise/counter-clockwise
- **`changelevel`** - Move up/down Z-levels
- **`jumptolocation`** - Teleport to coordinates (admin only)

### Interaction Tools (Category: `interaction`, `inventory`)
- **`pickup`** - Pick up items by entity ID
- **`drop`** - Drop items from inventory
- **`use`** - Use item on another entity (e.g., key on door)
- **`open`** - Open doors or containers
- **`close`** - Close doors or containers

### Vision Tools (Category: `vision`, `perception`)
- **`toggledirectionalvision`** - Toggle FOV cone mode
- **`setfieldofview`** - Set FOV in degrees (1-360)
- **`setlightingmode`** - Set lighting (Torch/Sunlight/Darkness)
- **`setvisionmode`** - Set vision mode (Normal/Infrared/UltraViolet/Sonar)

### World-Building Tools (Category: `worldbuilding`, `entity_management`)
- **`spawnentity`** - Create entities at coordinates
- **`destroyentity`** - Remove entities
- **`modifyentity`** - Change entity properties
- **`moveentity`** - Relocate entities

**Note**: World-building tools currently have stub implementations and require the `world_edit` or `world_generate` capabilities.

## Quick Start

### For Players (Using New Unified API)

```csharp
// Get available tools
var tools = await gameClient.GetAvailableToolsAsync();

// Execute a tool
var result = await gameClient.ExecuteToolAsync("move", new Dictionary<string, object>
{
    ["direction"] = "F"
});

if (result.Success)
    Console.WriteLine("Moved forward successfully!");
```

### For Agents (Using Tool System)

Agents automatically use the tool system when executing actions. The AgentRunnerGrain:
1. Gets perception from the game world
2. Asks LLM to choose a tool and arguments (or uses heuristic)
3. Executes the tool through the tool registry
4. Receives feedback on success/failure

```csharp
// Agent configuration happens automatically
// Set profile for an agent (in AgentRunnerGrain.OnActivateAsync):
_toolProfile = AgentToolProfile.Explorer; // or FullAccess, WorldBuilder, etc.

// Agents then automatically use only allowed tools
```

### Environment Variables

```powershell
# Enable LLM-driven agents
$env:AGENT_LLM_ENABLED="1"

# LLM API configuration
$env:OPENAI_API_BASE="http://localhost:1234/v1"  # LM Studio default
$env:OPENAI_API_KEY="lm-studio"
$env:AGENT_MODEL="phi-4"  # or "gpt-4" for function calling support

# Debug output
$env:AGENT_DEBUG="1"

# Rate limiting
$env:AGENT_LLM_MAX_CONCURRENT="2"
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       Client Layer                           │
│  (GameClient with ExecuteToolAsync + Legacy Methods)        │
└──────────────────────┬──────────────────────────────────────┘
                       │ SignalR
┌──────────────────────┴──────────────────────────────────────┐
│                      GameHub Layer                           │
│  • ExecuteTool(toolId, args)  [NEW]                         │
│  • ListAvailableTools()        [NEW]                         │
│  • MovePlayer, Pickup, etc.    [LEGACY - delegates to tools] │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────┴──────────────────────────────────────┐
│                  Tool Registry Layer                         │
│  • Discovers tools via reflection                            │
│  • Creates tool instances with DI                            │
│  • Filters by profile/capabilities                           │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────┴──────────────────────────────────────┐
│                    Tool Implementations                       │
│  • MoveTool, PickupTool, OpenTool, etc.                     │
│  • Each implements IAgentTool interface                      │
│  • Execute via ToolExecutionContext                          │
└───────────────────────────────────────────────────────────────┘
```

## Creating Custom Tools

### 1. Implement IAgentTool

```csharp
using Aetherium.Server.Agents.Tools;

[AgentTool("customaction", "Description of custom action",
    Categories = new[] { "custom" },
    RequiredCapabilities = new[] { "custom_capability" })]
public class CustomActionTool : IAgentTool
{
    public string ToolId => "customaction";
    public string Description => "Performs a custom action";
    public IEnumerable<string> Categories => new[] { "custom" };
    public IEnumerable<string> RequiredCapabilities => new[] { "custom_capability" };
    
    public ToolParameterSchema GetParameterSchema()
    {
        return new ToolParameterSchema
        {
            Properties = new Dictionary<string, ParameterDefinition>
            {
                ["param1"] = new()
                {
                    Type = "string",
                    Description = "Description of param1"
                }
            },
            Required = new() { "param1" }
        };
    }
    
    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        Dictionary<string, object> args)
    {
        // Check capabilities
        if (!context.HasCapability("custom_capability"))
            return ToolExecutionResult.Error("Missing capability");
        
        // Extract parameters
        if (!args.TryGetValue("param1", out var param1Obj))
            return ToolExecutionResult.Error("Missing param1");
        
        // Execute your custom logic here
        // ...
        
        return ToolExecutionResult.Ok("Action completed");
    }
}
```

### 2. Tool Will Be Auto-Discovered

The tool registry automatically discovers all classes implementing `IAgentTool` at startup. No manual registration required!

### 3. Add Capability to Profiles

```csharp
// In AgentToolProfile.cs, add to a profile:
public static AgentToolProfile CustomProfile => new()
{
    ProfileName = "custom",
    AllowedCategories = new() { "movement", "custom" },
    GrantedCapabilities = new() { "basic_movement", "custom_capability" }
};
```

## Testing

### Manual Testing with AgentCLI

```powershell
# Start server
cd Aetherium.Server
dotnet run

# In another terminal, use AgentCLI
cd AgentCLI

# List active sessions
dotnet run -- mgmt sessions

# List available tools
dotnet run -- tools list
dotnet run -- tools list --profile explorer
dotnet run -- tools describe move

# Test a tool execution (requires active session)
dotnet run -- tools test move --session-id <sessionId> --args '{"direction":"forward"}'
dotnet run -- tools test pickup --session-id <sessionId> --args '{"targetEntityId":"entity-123"}'

# Agent management
dotnet run -- agent attach <sessionId> --agent test-agent --runner runner-1
dotnet run -- agent run runner-1 --max-steps 10 --delay 500
dotnet run -- agent status runner-1
```

### Testing New Unified API

Create a simple test client:

```csharp
var client = new GameClient();
await client.ConnectAsync();

// List available tools
var tools = await client.GetAvailableToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.ToolId}: {tool.Description}");
}

// Execute a tool
var result = await client.ExecuteToolAsync("move", new Dictionary<string, object>
{
    ["direction"] = "F"
});

Console.WriteLine($"Result: {result.Message}");
```

## Future Enhancements

### Hierarchical Agent Delegation (Planned)
- Parent agents can spawn child agents with scoped contexts
- Useful for complex tasks like world generation or quest creation
- Parent validates child outputs for coherence

### Advanced World-Building (Planned)
- Full implementations of terrain modification tools
- Map section generation with validation
- Prefab placement and room creation
- Narrative token management

### Tool Metrics & Analytics (Planned)
- Track tool usage frequency
- Measure success rates per tool
- Identify problematic tools or capabilities
- Performance profiling per tool

## Migration from Legacy API

The legacy SignalR API (`MovePlayer`, `Pickup`, etc.) will continue to work but is marked for deprecation in a future release.

### Recommended Migration Path

1. **Update client code** to use `ExecuteToolAsync` instead of specific methods
2. **Test thoroughly** with both APIs to ensure compatibility
3. **Switch to new API** once confident
4. **Remove legacy method usage** before the next major version

### Example Migration

**Before (Legacy):**
```csharp
await gameClient.MovePlayerAsync(RelativeDirection.Forward, 1);
await gameClient.PickupAsync(entityId);
```

**After (New Unified API):**
```csharp
await gameClient.ExecuteToolAsync("move", new Dictionary<string, object> {
    ["direction"] = "F",
    ["distance"] = 1
});

await gameClient.ExecuteToolAsync("pickup", new Dictionary<string, object> {
    ["targetEntityId"] = entityId
});
```

## Troubleshooting

### Tool Not Found
- Ensure tool class implements `IAgentTool`
- Check that `[AgentTool]` attribute is present
- Verify tool registry discovered it at startup (check console output)

### Permission Denied
- Check tool's `RequiredCapabilities`
- Verify agent profile has those capabilities
- Use `AgentToolProfile.Admin` for testing

### LLM Not Choosing Tools
- Enable debug mode: `$env:AGENT_DEBUG="1"`
- Check perception JSON is being sent correctly
- Verify tool descriptions are clear
- Try OpenAI-compatible model for function calling

## Support

For issues, questions, or contributions, see the project documentation or reach out to the development team.

