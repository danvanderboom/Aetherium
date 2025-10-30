## 1. Core Infrastructure
- [ ] 1.1 Create `ConsoleGameServer/Management/` directory
- [ ] 1.2 Create `IGameManagementGrain.cs` interface with all method signatures
- [ ] 1.3 Create `SessionInfo.cs` DTO class
- [ ] 1.4 Create `OperationResult.cs` DTO class with Ok() and Error() factory methods
- [ ] 1.5 Create `VisionStatus.cs` DTO class
- [ ] 1.6 Create `GameManagementGrain.cs` implementation (singleton, ConcurrentDictionary for state)
- [ ] 1.7 Verify grain compiles and no namespace conflicts exist

## 2. GameHub Integration
- [ ] 2.1 Add `IClusterClient` field to GameHub constructor for grain access
- [ ] 2.2 In `OnConnectedAsync`, call grain.RegisterSessionAsync after session creation
- [ ] 2.3 In `OnDisconnectedAsync`, call grain.UnregisterSessionAsync before session removal
- [ ] 2.4 Handle grain call exceptions gracefully (log and continue)
- [ ] 2.5 Add null check for Orleans disabled mode (DISABLE_ORLEANS=1)
- [ ] 2.6 Test session lifecycle with manual client connect/disconnect

## 3. Grain Session Management Methods
- [ ] 3.1 Implement RegisterSessionAsync (add to sessionIndex, bidirectional mapping)
- [ ] 3.2 Implement UnregisterSessionAsync (remove from sessionIndex)
- [ ] 3.3 Implement ListSessionsAsync (return all SessionInfo)
- [ ] 3.4 Implement GetSessionInfoAsync (lookup by sessionId)
- [ ] 3.5 Implement GetSessionByConnectionIdAsync (reverse lookup)
- [ ] 3.6 Implement GetSessionCountAsync (return index.Count)
- [ ] 3.7 Add OnActivateAsync logging for grain lifecycle debugging

## 4. Vision Control Methods
- [ ] 4.1 Inject `IHubContext<GameHub>` into GameManagementGrain constructor
- [ ] 4.2 Implement SetDirectionalVisionAsync (call hub method via IHubContext)
- [ ] 4.3 Implement SetFieldOfViewAsync with validation (1-360 degrees)
- [ ] 4.4 Implement GetVisionStatusAsync (query session state and return VisionStatus)
- [ ] 4.5 Add private helper method to get GameSessionManager from hub context (if needed)
- [ ] 4.6 Test vision toggle from grain while client is connected

## 5. Additional Control Methods
- [ ] 5.1 Implement SetLightingModeAsync (validate enum, invoke hub)
- [ ] 5.2 Implement SetVisionModeAsync (validate enum, invoke hub)
- [ ] 5.3 Implement TerminateSessionAsync (disconnect via IHubContext)
- [ ] 5.4 Implement SetTimeScaleAsync (validate > 0, invoke hub)
- [ ] 5.5 Implement SetAllSessionsVisionModeAsync batch operation
- [ ] 5.6 Add error handling for all hub context invocations

## 6. AgentCLI Integration
- [ ] 6.1 Add GetGameManagement() method to AgentClient (returns singleton grain with key "GLOBAL")
- [ ] 6.2 Update `vision directional` command: call grain.SetDirectionalVisionAsync(sessionId, true)
- [ ] 6.3 Update `vision omnidirectional` command: call grain.SetDirectionalVisionAsync(sessionId, false)
- [ ] 6.4 Update `vision fov` command: call grain.SetFieldOfViewAsync(sessionId, degrees)
- [ ] 6.5 Update `vision status` command: call grain.GetVisionStatusAsync and format output
- [ ] 6.6 Remove all "TODO: Requires GameManagementGrain" messages
- [ ] 6.7 Add error handling and user-friendly messages for grain operation failures
- [ ] 6.8 Test all CLI commands with running server and connected client

## 7. Testing & Validation
- [ ] 7.1 Start server with Orleans enabled (no DISABLE_ORLEANS env var)
- [ ] 7.2 Connect game client and note session ID from server console
- [ ] 7.3 Test `agentcli vision status <sessionId>` - verify output shows session info
- [ ] 7.4 Test `agentcli vision directional <sessionId>` - verify vision changes in game client
- [ ] 7.5 Test toggling vision with 'T' key in client - verify still works (no regression)
- [ ] 7.6 Test `agentcli vision fov <sessionId> 90` - verify FOV changes
- [ ] 7.7 Test error case: invalid session ID - verify graceful error message
- [ ] 7.8 Test error case: client disconnects - verify grain handles missing session
- [ ] 7.9 Test with DISABLE_ORLEANS=1 - verify no crashes, graceful degradation
- [ ] 7.10 Test concurrent operations: multiple CLI commands while client is active
- [ ] 7.11 Run `openspec validate add-game-management-grain --strict` and confirm passes

