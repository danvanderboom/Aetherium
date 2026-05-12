## MODIFIED Requirements

### Requirement: Grain Mutation Methods
`IGameMapGrain` SHALL expose typed mutation methods that apply gameplay actions to the grain's `_world`: `MoveAsync(sessionId, direction, distance)`, `RotateAsync(sessionId, degrees)`, `ChangeLevelAsync(sessionId, deltaZ)`, `PickupAsync(sessionId, targetEntityId)`, `DropAsync(sessionId, itemEntityId)`, `UseAsync(sessionId, itemEntityId, onEntityId, usageId)`, `OpenAsync(sessionId, targetEntityId)`, `CloseAsync(sessionId, targetEntityId)`. Orleans's single-threaded grain contract SHALL serialize concurrent invocations. Where an equivalent verb exists on `InteractionSystem`, the grain method SHALL delegate to the stateless `InteractionSystem.Try*(ActionContext, ...)` overload rather than reimplementing the verb's logic.

#### Scenario: Pickup/Drop/Open/Close delegate to InteractionSystem
- **WHEN** `IGameMapGrain.PickupAsync`, `DropAsync`, `OpenAsync`, or `CloseAsync` is invoked
- **THEN** the grain SHALL build an `ActionContext { World, Player, ViewLocation }` from its `_world` and the looked-up player Character
- **AND** SHALL call the corresponding `InteractionSystem.Try*(ActionContext, ...)` method to apply the mutation
- **AND** SHALL emit the appropriate `MapDelta` on success
- **AND** SHALL NOT reimplement the verb's pre-conditions or post-conditions in the grain itself

#### Scenario: Move/Rotate/ChangeLevel stay native
- **WHEN** `IGameMapGrain.MoveAsync`, `RotateAsync`, or `ChangeLevelAsync` is invoked
- **THEN** the grain SHALL implement the verb natively (no `InteractionSystem` equivalent)
- **AND** SHALL emit the appropriate `MapDelta` after mutation

#### Scenario: UseAsync remains limited to key-on-door
- **WHEN** `IGameMapGrain.UseAsync` is invoked with a non-key-on-door usage
- **THEN** the grain MAY return a failure with `Reason` indicating the mode is not yet supported in grain-bound sessions
- **AND** full `Use` disambiguation in grain mode is a future change that requires new delta DTOs (consume/place/lockpick/climb post-conditions)

#### Scenario: Mutation rejects unknown session
- **WHEN** a mutation method is invoked with a `sessionId` not in `_mapState.PlayerIds`
- **THEN** the grain SHALL return a failure result with a `Reason` indicating the unknown session
- **AND** SHALL NOT mutate `_world`

#### Scenario: Concurrent mutations are serialized
- **WHEN** two players concurrently invoke `PickupAsync(sessionId, sameItemId)` on the same map grain
- **THEN** exactly one call SHALL succeed (Orleans serialization)
- **AND** the other SHALL receive an `InteractionResultDto` with `Success=false` and `Reason` matching the existing "Already picked up" failure
- **AND** the item SHALL appear in exactly one player's inventory

## ADDED Requirements

### Requirement: ActionContext Stateless API for Grain-Routed Verbs
`InteractionSystem` SHALL expose `TryPickup`, `TryDrop`, `TryOpen`, and `TryClose` as pairs of overloads: one taking `GameSession` (legacy / `LocalMutationGateway` consumers) and one taking `ActionContext { World, Character Player, WorldLocation ViewLocation }` (grain consumers). The session overload SHALL be a thin forwarder. The `ActionContext` overload SHALL hold the canonical implementation. Other verbs (`TryUse` and the disambiguation chain `GetUseOptions` / `TryUseWithMode` / `TryActivate` / `TryConsume` / `TryPlace` / `TryClimb` / `TryForceOpen` / `TryLockpick` / `TryEquip`) remain session-bound in this change and are reachable only via `LocalMutationGateway`; their migration is deferred to a future change that also extends the delta DTO vocabulary to cover the post-conditions those verbs trigger.

#### Scenario: Both overloads produce identical outcomes
- **WHEN** a caller invokes `interactionSystem.TryPickup(session, targetId)` and another caller invokes `interactionSystem.TryPickup(new ActionContext(session.World, session.Player, session.ViewLocation), targetId)` on equivalent state
- **THEN** both calls SHALL produce equivalent `InteractionResult` values
- **AND** both calls SHALL mutate the underlying world identically (same entity moved/removed, same inventory changes)

#### Scenario: ActionContext fields are non-null
- **WHEN** `ActionContext` is constructed
- **THEN** the record SHALL require non-null `World`, `Player`, and `ViewLocation`
- **AND** a caller that has null values SHALL surface that as a precondition failure before constructing the context (the session overload's null-checks remain the canonical guard)

#### Scenario: Use disambiguation remains session-bound
- **WHEN** any caller invokes `InteractionSystem.TryUse` or any of the disambiguation helper methods
- **THEN** the call SHALL go through the existing session-taking signature
- **AND** the grain's `IGameMapGrain.UseAsync` SHALL retain its native key-on-door-only implementation until a future change migrates the Use chain
