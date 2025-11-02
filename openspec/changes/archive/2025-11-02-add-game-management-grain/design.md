## Context
Orleans grains need programmatic access to control SignalR hub methods and query game session state. The current architecture has GameSessionManager as an in-memory singleton accessed only within GameHub's SignalR context. AI agents running in Orleans grains (IAgentGrain) cannot directly access this state or invoke hub methods on specific connections. A bridge pattern is required to enable grain-to-hub communication while maintaining the existing session lifecycle.

Key stakeholders:
- AgentCLI users needing remote vision control
- AI agents (future) needing to observe and control sessions
- Server administrators needing session observability
- GameHub maintaining authoritative session state

Constraints:
- Cannot directly inject GameSessionManager into Orleans grains (different DI containers)
- Must preserve existing SignalR-based client control (T key for vision toggle)
- Must maintain thread-safe concurrent access to session state
- Should avoid heavy refactoring of existing GameSession/GameHub code

## Goals / Non-Goals

### Goals
- Provide session observability: list, query, and inspect active game sessions
- Enable remote vision mode control: toggle directional/omnidirectional vision, adjust FOV
- Support session queries by session ID or connection ID
- Enable administrative operations: terminate sessions, adjust time scale
- Prepare groundwork for AI agent integration (future)
- Maintain clean separation: grain as control plane, GameHub as game logic

### Non-Goals
- Real-time game logic execution (stays in GameSession/GameHub)
- Per-session grains (use singleton grain for management simplicity)
- Immediate session persistence (Orleans grain storage deferred to future iteration)
- Direct World/Entity manipulation from grains (use hub methods only)
- Multi-server session coordination (single server for now)

## Decisions

### Decision 1: Singleton Grain Pattern
**What**: Use a single `IGameManagementGrain` instance accessed via fixed grain key (e.g., "GLOBAL").

**Why**: 
- Management/registry grains naturally fit singleton pattern (see IPromptRegistryGrain)
- Simpler than per-session grains for query operations
- Single source of truth for session index
- Matches Orleans best practices for coordinator grains

**Alternatives considered**:
- Per-session grains: Would require grain discovery mechanism, adds complexity for simple management use case
- Stateless worker grain: Would need external state store, adds unnecessary dependency

### Decision 2: Session Metadata Index
**What**: Grain maintains `ConcurrentDictionary<string sessionId, SessionMetadata>` with bidirectional sessionId ↔ connectionId mapping.

**Why**:
- Enables fast lookups by either identifier
- Supports both CLI (uses sessionId) and hub (uses connectionId) access patterns
- Lightweight metadata layer over existing GameSessionManager
- Thread-safe for concurrent grain activation

**Alternatives considered**:
- Query GameSessionManager directly: Not accessible from Orleans grain context
- Event sourcing: Over-engineered for current requirements

### Decision 3: IHubContext Bridge
**What**: Inject `IHubContext<GameHub>` into grain to invoke hub methods on specific connections.

**Why**:
- Standard SignalR pattern for non-hub contexts invoking hub methods
- Allows grain to trigger `ToggleDirectionalVision()`, etc. on specific client connections
- Maintains GameHub as authoritative game logic location
- Avoids duplicating vision control logic

**Alternatives considered**:
- Direct GameSessionManager manipulation: Not accessible, breaks encapsulation
- Event bus: Adds complexity without clear benefit
- Grain-owned state with sync: Creates dual source of truth, high sync overhead

### Decision 4: Registration via Method Calls
**What**: GameHub calls grain methods `RegisterSessionAsync()` and `UnregisterSessionAsync()` in OnConnected/OnDisconnected.

**Why**:
- Simple, explicit lifecycle hooks
- No additional infrastructure needed
- Easy to debug and understand flow
- Works with existing hub lifecycle

**Alternatives considered**:
- Orleans Observers: Over-complicated for simple registration
- Periodic polling: Inefficient, stale data risk
- Message queue: Infrastructure overhead not justified

### Decision 5: Session ID as Primary Key
**What**: Use session ID (GUID) as primary identifier for grain operations, with connection ID as convenience lookup.

**Why**:
- Session ID is stable across potential reconnection scenarios (future)
- More semantic for external callers (agents, CLI)
- Connection ID is SignalR implementation detail
- Supports future multi-connection per session scenarios

**Alternatives considered**:
- Connection ID primary: Ties API to SignalR implementation, less stable
- Composite key: Unnecessarily complex

## Risks / Trade-offs

### Risk 1: State Desynchronization
**Risk**: Grain's session index becomes stale if GameHub crashes or registration fails.

**Mitigation**: 
- Start with simple registration; validate correct lifecycle hookup
- Future: Add periodic reconciliation comparing grain index to GameSessionManager
- Future: Add health check endpoint comparing counts

**Trade-off**: Accept eventual consistency for simplicity in v1.

### Risk 2: IHubContext Performance Overhead
**Risk**: Frequent grain→hub calls could add latency.

**Mitigation**:
- IHubContext is designed for this pattern, used widely in production
- Vision control is infrequent (admin/agent commands, not per-frame)
- Measure if issues arise; optimize with batching if needed

**Trade-off**: Clean architecture separation worth potential minor latency.

### Risk 3: Concurrent Access to GameSessionManager
**Risk**: Grain and hub both accessing GameSessionManager could cause race conditions.

**Mitigation**:
- GameSessionManager already uses ConcurrentDictionary (thread-safe)
- Grain doesn't directly access GameSessionManager (uses hub bridge)
- GameSession properties read by grain are stable or atomic

**Trade-off**: None; existing thread safety sufficient.

### Risk 4: Orleans Grain Activation Delays
**Risk**: First grain access might have activation delay affecting CLI responsiveness.

**Mitigation**:
- Singleton grain activates once, stays resident
- Pre-activate grain on server startup if needed (future optimization)
- Typical activation < 10ms, acceptable for admin commands

**Trade-off**: Accept minor first-call latency for Orleans benefits.

## Migration Plan

### Phase 1: Initial Implementation (This Change)
1. Create grain interface and implementation
2. Add registration hooks to GameHub
3. Implement vision control methods
4. Update AgentCLI commands
5. Test with manual client connections

### Phase 2: Validation
- Test session lifecycle (connect, disconnect, register, unregister)
- Validate vision control from CLI while client connected
- Verify no regression in direct client control (T key)
- Test error cases (invalid session IDs, disconnected sessions)

### Phase 3: Future Enhancements (Out of Scope)
- Add Orleans grain persistence for session state durability
- Add periodic reconciliation between grain and GameSessionManager
- Implement session query filters (by vision mode, time scale, etc.)
- Add session metrics (FPS, update counts, perception size)
- Enable multi-connection per session scenarios

### Rollback Plan
If issues arise:
1. Disable grain registration calls in GameHub (comment out)
2. AgentCLI commands revert to showing TODO messages
3. No database migrations to revert (stateless)
4. No breaking changes to existing client-server protocol

### Compatibility
- Fully backward compatible: existing GameHub/GameSession unchanged
- AgentCLI vision commands go from TODO to functional
- No changes to client-server SignalR protocol
- Orleans can be disabled via existing `DISABLE_ORLEANS=1` env var

## Open Questions

1. **Q**: Should grain track session creation timestamps for observability?
   **A**: Yes, include `ConnectedAt` in SessionInfo DTO.

2. **Q**: Should vision control be synchronous or queued?
   **A**: Synchronous via IHubContext; apply immediately on next perception update.

3. **Q**: Should we validate FOV degrees in grain or hub method?
   **A**: Both. Grain validates range (1-360), hub method applies to entity.

4. **Q**: How to handle session operations if Orleans is disabled?
   **A**: Grain registration becomes no-op; CLI commands return "Orleans disabled" message.

5. **Q**: Should GetSessionInfo return current player location?
   **A**: No. Maintain perception-based security; don't expose absolute coordinates to observers.

