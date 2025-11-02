## ADDED Requirements

### Requirement: Interaction Commands
The system SHALL provide server actions for item and object interactions usable by human and AI clients.

#### Scenario: Pick up an item
- **WHEN** client sends `Pickup(targetEntityId)`
- **THEN** server adds the item to the actor's inventory if carriable and co-located
- **AND** returns success/failure with a reason on failure

#### Scenario: Drop an inventory item
- **WHEN** client sends `Drop(entityId|inventoryIndex)`
- **THEN** server places the item at the actor's location if capacity rules allow
- **AND** returns success/failure

#### Scenario: Use an item on a target
- **WHEN** client sends `Use(itemEntityId|inventoryIndex, onEntityId)`
- **THEN** server applies the item's effect to the target (e.g., unlock door with matching key)
- **AND** returns success/failure

#### Scenario: Open/Close a door
- **WHEN** client sends `Open(targetEntityId)` or `Close(targetEntityId)`
- **THEN** server toggles the door if unlocked and adjacent/accessible
- **AND** returns success/failure

### Requirement: Interaction Events
The system SHALL emit world events after successful interactions.

#### Scenario: Item picked up event
- **WHEN** an item is picked up
- **THEN** `ItemPickedUp` world event is emitted (actorId, itemId)

#### Scenario: Door state change events
- **WHEN** a door is opened/closed/locked/unlocked
- **THEN** a corresponding world event is emitted and obstructions updated

