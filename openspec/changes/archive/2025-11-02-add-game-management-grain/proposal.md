## Why
AgentCLI needs programmatic session control for game management and vision mode manipulation. Currently, GameSessionManager is in-memory only and accessed via SignalR connection IDs, making it inaccessible from Orleans grains. AI agents and administrators need the ability to query active sessions, control vision modes (directional/omnidirectional), adjust field of view, and modify session settings remotely without direct client interaction.

## What Changes
- Add `IGameManagementGrain` Orleans interface with session management methods
- Add `GameManagementGrain` implementation (singleton grain, stateless)
- Add session registration hooks in `GameHub.OnConnectedAsync` and `GameHub.OnDisconnectedAsync`
- Add `SessionInfo` and `OperationResult` DTOs for grain method responses
- Add `VisionStatus` DTO for vision configuration queries
- Implement vision control commands in `AgentCLI/Program.cs` (replace TODO placeholders)
- Add grain-to-GameHub bridge using `IHubContext<GameHub>` for executing hub methods from grain context
- Add `GetGameManagement()` method to `AgentClient` for accessing the singleton grain

## Impact
- Affected specs: NEW `game-management-grain`
- Affected code:
  - `Aetherium.Server/GameHub.cs` - Add grain injection and registration hooks
  - `AgentCLI/Program.cs` - Implement vision commands (lines 138-191)
  - `AgentCLI/AgentClient.cs` - Add GetGameManagement() method
- New files:
  - `Aetherium.Server/Management/IGameManagementGrain.cs`
  - `Aetherium.Server/Management/GameManagementGrain.cs`
  - `Aetherium.Server/Management/SessionInfo.cs`
  - `Aetherium.Server/Management/OperationResult.cs`
  - `Aetherium.Server/Management/VisionStatus.cs`


