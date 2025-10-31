## ADDED Requirements

### Requirement: Meta-Progression State
The system SHALL track player achievements across multiple worlds, enabling unlocks of new generation parameters and templates.

#### Scenario: Discovery tracking
- **WHEN** a player visits a new world/map or completes a cross-world quest
- **THEN** MetaProgressionGrain SHALL record the discovery (world template, tags, or quest completion)
- **AND** discoveries SHALL be persisted per player/account

#### Scenario: Unlock system
- **WHEN** a player meets unlock criteria (e.g., visited N worlds of type X, completed cross-world quest chain)
- **THEN** MetaProgressionGrain SHALL unlock new generator templates or parameters
- **AND** GetAllowedGenerators() SHALL return only unlocked templates for world creation UIs

#### Scenario: Cross-world quest unlocks
- **WHEN** a player completes a quest that spans multiple worlds
- **THEN** MetaProgressionGrain SHALL record the quest completion
- **AND** this SHALL contribute to unlock criteria for advanced generation options

