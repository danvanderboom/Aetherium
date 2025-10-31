## ADDED Requirements

### Requirement: Biome Layout with Adjacency Rules
Outdoor maps SHALL assign biomes (e.g., forest, plains, hills, mountains, water) obeying adjacency and elevation constraints.

#### Scenario: Valid biome transitions
- WHEN a biome map is generated
- THEN invalid adjacency pairs are absent

### Requirement: Rivers, Roads, and Connectivity
The system SHALL generate rivers following elevation flow and roads connecting POIs (cities, villages, dungeons).

#### Scenario: POIs are connected
- WHEN POIs exist
- THEN at least one road path connects them with reasonable length bounds

### Requirement: Settlements and POIs
Cities/villages MUST spawn with internal road grids, entrances, and logical placement near resources/roads.

#### Scenario: Village placement heuristics
- WHEN a village is placed
- THEN it is adjacent to a road and near suitable terrain

### Requirement: Traversal Costs and Terrain Features
The outdoor generator SHALL emit traversal cost layers for navigation and AI.

#### Scenario: Nav cost map exists
- WHEN outdoor generation completes
- THEN a cost map is available for pathfinding


