# Agent Tool Profiles

## Overview

Agent Tool Profiles define which tools an agent can access based on their role. All game characters (NPCs and human players) should use the same profile by default, while world-building agents have different profiles with elevated capabilities.

## Profile Hierarchy

### 🎮 Game Character Profiles

**Player Profile** (Default for all game characters)
- **Used for**: Human players AND NPCs in the game
- **Tools**: Movement, navigation, inventory, interaction, perception, vision
- **Capabilities**: `basic_movement`, `inventory_access`, `interaction`, `vision`
- **Principle**: All game characters get the same tools unless explicitly granted special access

**Explorer Profile** (Limited - for test/simple agents only)
- **Used for**: Simple exploration-only agents (testing, debugging)
- **Tools**: Movement, navigation, perception (NO inventory/interaction)
- **Capabilities**: `basic_movement`, `vision`
- **Note**: NOT for game NPCs - use Player profile for those

### 🛠️ World-Building Agent Profiles

**WorldBuilder Profile**
- **Used for**: Agents that create/modify the game world
- **Tools**: Movement, navigation, worldbuilding, entity_management, terrain
- **Capabilities**: `basic_movement`, `vision`, `world_edit`, `world_generate`
- **Distinct from**: Game NPCs - these agents work on the world itself, not within it

**NarrativeDesigner Profile**
- **Used for**: Agents that create quests and narrative content
- **Tools**: Movement, navigation, worldbuilding, narrative, quest
- **Capabilities**: `basic_movement`, `vision`, `narrative_edit`, `world_edit`
- **Distinct from**: Game NPCs - these agents create the story, not participate in it

### 🔧 Admin Profile

**Admin Profile**
- **Used for**: Unrestricted access (debugging, admin operations)
- **Tools**: ALL tools including admin-only
- **Capabilities**: All capabilities including `admin`
- **Use sparingly**: Only for special cases requiring unrestricted access

## Default Profile Assignment

### Game Characters (NPCs + Human Players)
- **Default**: `Player` profile
- **Rationale**: All characters in a game should have the same capabilities unless explicitly granted special access

### Agent Runners
- **Default**: `Player` profile (for game NPCs)
- **Can be set to**: `WorldBuilder`, `NarrativeDesigner`, or `Admin` for special-purpose agents

### Human Players (via SignalR)
- **Default**: `Player` profile
- **Via**: `GameHub.ListAvailableTools()` and `GameHub.ExecuteTool()`

## Profile Assignment Examples

### Example 1: Regular Game NPC
```csharp
// Default - uses Player profile automatically
var agent = await mgmt.CreateAgentAsync("npc-guard-1", "player");
// Gets: movement, inventory, interaction, vision tools
```

### Example 2: World-Building Agent
```csharp
// Explicitly set to WorldBuilder profile
var builder = await mgmt.CreateAgentAsync("world-builder-1", "worldbuilder");
// Gets: movement, worldbuilding, entity_management, terrain tools
// Does NOT get: inventory, interaction tools (not needed for world-building)
```

### Example 3: Narrative Designer Agent
```csharp
// Explicitly set to NarrativeDesigner profile
var designer = await mgmt.CreateAgentAsync("quest-designer-1", "narrativedesigner");
// Gets: movement, narrative, quest, worldbuilding tools
// Can create quests and narrative tokens
```

### Example 4: Special NPC with Admin Access
```csharp
// Explicitly grant admin access (rare case)
var adminNPC = await mgmt.CreateAgentAsync("gm-avatar", "admin");
// Gets: ALL tools including debug/admin capabilities
```

## Key Principles

1. **Equality by Default**: All game characters (NPCs + players) get the same tools unless explicitly configured otherwise
2. **Separation of Concerns**: World-building agents are separate from game NPCs - they operate on different tools
3. **Explicit Special Access**: If a game character needs special tools, explicitly assign a different profile
4. **Security**: Tools with required capabilities (e.g., "admin") are blocked even if category matches

## Profile Selection Guide

| Use Case | Profile | Why |
|----------|---------|-----|
| Human player | `Player` | Standard game character |
| NPC in game | `Player` | Same as human players |
| Quest NPC | `Player` | Still a game character (quests handled separately) |
| World builder agent | `WorldBuilder` | Creates/modifies world, needs entity spawn tools |
| Quest designer agent | `NarrativeDesigner` | Creates quests/narrative, needs quest tools |
| Test/exploration agent | `Explorer` | Limited capabilities for testing |
| Admin/debug agent | `Admin` | Unrestricted access |

## Tool Access Matrix

| Tool Category | Player | Explorer | WorldBuilder | NarrativeDesigner | Admin |
|--------------|--------|----------|--------------|-------------------|-------|
| Movement | ✅ | ✅ | ✅ | ✅ | ✅ |
| Navigation | ✅ | ✅ | ✅ | ✅ | ✅ |
| Inventory | ✅ | ❌ | ❌ | ❌ | ✅ |
| Interaction | ✅ | ❌ | ❌ | ❌ | ✅ |
| Perception | ✅ | ✅ | ❌ | ❌ | ✅ |
| Vision | ✅ | ✅ | ✅ | ✅ | ✅ |
| World Building | ❌ | ❌ | ✅ | ✅ | ✅ |
| Entity Management | ❌ | ❌ | ✅ | ❌ | ✅ |
| Terrain | ❌ | ❌ | ✅ | ❌ | ✅ |
| Narrative | ❌ | ❌ | ❌ | ✅ | ✅ |
| Quest | ❌ | ❌ | ❌ | ✅ | ✅ |
| Admin | ❌ | ❌ | ❌ | ❌ | ✅ |

## Implementation Details

### Default Behavior
- `AgentRunnerGrain` defaults to `Player` profile
- `GameHub` uses `Player` profile for human players
- `GameManagementGrain.ListAvailableToolsAsync()` defaults to `Player` profile

### Changing Profiles
Profiles can be changed per agent via:
- Agent creation API
- Runtime profile assignment (if supported)
- Configuration files

### Legacy Support
- `FullAccess` profile maps to `Player` (backward compatibility)
- Old code using `FullAccess` will continue to work

