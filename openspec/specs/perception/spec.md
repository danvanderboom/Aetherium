# perception Specification

## Purpose
TBD - created by archiving change add-inventory-keys-doors. Update Purpose after archive.
## Requirements
### Requirement: Affordances in Perception
Perception frames SHALL include nearby interaction affordances for AI agents.

#### Scenario: Affordances listed
- **WHEN** the actor perceives an item or door in the same or adjacent tile
- **THEN** the frame includes actions and parameters (pickup, drop, use, open, close)

### Requirement: Inventory & Visible Items in Perception
Perception frames SHALL include the actor's inventory and visible items metadata.

#### Scenario: Inventory summary
- **WHEN** inventory changes
- **THEN** `inventory` in perception reflects capacity and current items

#### Scenario: Visible items summary
- **WHEN** items are visible within the frame
- **THEN** they are listed with id/label/icon and optional keyId

