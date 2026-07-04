## ADDED Requirements

### Requirement: Visible Characters in Perception
Perception SHALL report the other characters (monsters/NPCs and co-located players) within the perceiving player's field of view as structured, renderable data — not merely an opaque count — so a client can draw each character distinctly from the terrain beneath it. The perceiving player SHALL be excluded from this set.

#### Scenario: A visible monster is reported with its tile
- **WHEN** perception is computed for a player and a monster stands on a cell within the player's field of view
- **THEN** `PerceptionDto.VisibleCharacters` SHALL contain an entry for that monster
- **AND** the entry SHALL carry the monster's tile (glyph and colors) and its location relative to the player (player at 0,0,0)
- **AND** the entry SHALL be flagged hostile for monster/NPC characters

#### Scenario: The perceiving player is excluded
- **WHEN** perception is computed for a player standing at their own location
- **THEN** `VisibleCharacters` SHALL NOT contain an entry at the relative origin (0,0,0)
- **AND** the player remains the client's centre marker

#### Scenario: Perception tolerates inventory-less characters
- **WHEN** the character at the perceived location has no Inventory component
- **THEN** perception SHALL still compute without error (inventory and navigation data degrade to empty rather than throwing)
