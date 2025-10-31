## 1. Core Infrastructure
- [x] 1.1 Create `Aetherium.Server/Management/` directory
- [x] 1.2 Create `IGameManagementGrain.cs` interface with all method signatures
- [x] 1.3 Create `SessionInfo.cs` DTO class
- [x] 1.4 Create `OperationResult.cs` DTO class with Ok() and Error() factory methods
- [x] 1.5 Create `VisionStatus.cs` DTO class
- [x] 1.6 Create `GameManagementGrain.cs` implementation (singleton, ConcurrentDictionary for state)
- [x] 1.7 Verify grain compiles and no namespace conflicts exist

## 2. GameHub Integration
- [x] 2.1 Add `IClusterClient` field to GameHub constructor for grain access
- [x] 2.2 In `OnConnectedAsync`, call grain.RegisterSessionAsync after session creation
- [x] 2.3 In `OnDisconnectedAsync`, call grain.UnregisterSessionAsync before session removal
- [x] 2.4 Handle grain call exceptions gracefully (log and continue)
- [x] 2.5 Add null check for Orleans disabled mode (DISABLE_ORLEANS=1)
- [x] 2.6 Test session lifecycle with manual client connect/disconnect

## 3. Grain Session Management Methods
- [x] 3.1 Implement RegisterSessionAsync (add to sessionIndex, bidirectional mapping)
- [x] 3.2 Implement UnregisterSessionAsync (remove from sessionIndex)
- [x] 3.3 Implement ListSessionsAsync (return all SessionInfo)
- [x] 3.4 Implement GetSessionInfoAsync (lookup by sessionId)
- [x] 3.5 Implement GetSessionByConnectionIdAsync (reverse lookup)
- [x] 3.6 Implement GetSessionCountAsync (return index.Count)
- [x] 3.7 Add OnActivateAsync logging for grain lifecycle debugging

## 4. Vision Control Methods
- [x] 4.1 Inject `IHubContext<GameHub>` into GameManagementGrain constructor
- [x] 4.2 Implement SetDirectionalVisionAsync (call hub method via IHubContext)
- [x] 4.3 Implement SetFieldOfViewAsync with validation (1-360 degrees)
- [x] 4.4 Implement GetVisionStatusAsync (query session state and return VisionStatus)
- [x] 4.5 Add private helper method to get GameSessionManager from hub context (if needed)
- [x] 4.6 Test vision toggle from grain while client is connected

## 5. Additional Control Methods
- [x] 5.1 Implement SetLightingModeAsync (validate enum, invoke hub)
- [x] 5.2 Implement SetVisionModeAsync (validate enum, invoke hub)
- [x] 5.3 Implement TerminateSessionAsync (disconnect via IHubContext)
- [x] 5.4 Implement SetTimeScaleAsync (validate > 0, invoke hub)
- [x] 5.5 Implement SetAllSessionsVisionModeAsync batch operation
- [x] 5.6 Add error handling for all hub context invocations

## 6. AgentCLI Integration
- [x] 6.1 Add GetGameManagement() method to AgentClient (returns singleton grain with key "GLOBAL")
- [x] 6.2 Update `vision directional` command: call grain.SetDirectionalVisionAsync(sessionId, true)
- [x] 6.3 Update `vision omnidirectional` command: call grain.SetDirectionalVisionAsync(sessionId, false)
- [x] 6.4 Update `vision fov` command: call grain.SetFieldOfViewAsync(sessionId, degrees)
- [x] 6.5 Update `vision status` command: call grain.GetVisionStatusAsync and format output
- [x] 6.6 Remove all "TODO: Requires GameManagementGrain" messages
- [x] 6.7 Add error handling and user-friendly messages for grain operation failures
- [x] 6.8 Test all CLI commands with running server and connected client

## 7. Testing & Validation
- [x] 7.1 Start server with Orleans enabled (no DISABLE_ORLEANS env var)
- [x] 7.2 Connect game client and note session ID from server console
- [x] 7.3 Test `agentcli vision status <sessionId>` - verify output shows session info
- [x] 7.4 Test `agentcli vision directional <sessionId>` - verify vision changes in game client
- [x] 7.5 Test toggling vision with 'T' key in client - verify still works (no regression)
- [x] 7.6 Test `agentcli vision fov <sessionId> 90` - verify FOV changes
- [x] 7.7 Test error case: invalid session ID - verify graceful error message
- [x] 7.8 Test error case: client disconnects - verify grain handles missing session
- [x] 7.9 Test with DISABLE_ORLEANS=1 - verify no crashes, graceful degradation
- [x] 7.10 Test concurrent operations: multiple CLI commands while client is active
- [x] 7.11 Run `openspec validate add-game-management-grain --strict` and confirm passes


