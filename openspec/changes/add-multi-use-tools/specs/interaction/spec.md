## ADDED Requirements

### Requirement: Multi-Use Tools
Items SHALL support multiple usage options when appropriate. Each usage option SHALL have a unique usage ID, label, description, and optional context requirements.

#### Scenario: Single usage option auto-executes
- **WHEN** an item has exactly one valid usage option for the current context
- **THEN** the usage executes automatically without requiring selection
- **AND** backward compatibility is maintained with existing single-use items

#### Scenario: Multiple usage options require selection
- **WHEN** an item has multiple valid usage options
- **THEN** the player/agent must select which usage to execute
- **AND** selection can occur proactively (via affordances) or reactively (via server response)

#### Scenario: Context-gated usage options
- **WHEN** a usage option has context requirements (e.g., "target-is-door", "adjacent-target", "in-forest")
- **THEN** the option is only available when all required context tags are present
- **AND** invalid options are filtered out automatically

### Requirement: Context Evaluation
The system SHALL evaluate game context for each interaction to determine available usage options.

#### Scenario: Context tags computed
- **WHEN** evaluating context for a session and optional target
- **THEN** the system computes context tags such as "near-door", "indoors", "in-forest", "target-is-door", "adjacent-target"
- **AND** context requirements are checked against available tags

### Requirement: Proactive Disambiguation
Perception SHALL include usage options in affordances when multiple options are available.

#### Scenario: Affordances include usage options
- **WHEN** perception is computed for a "use" affordance
- **THEN** the affordance includes UsageOptions list with all valid usage options
- **AND** each option includes UsageId, Label, and TargetId

### Requirement: Reactive Disambiguation
When use is called without a usageId and multiple options exist, the server SHALL return available options for selection.

#### Scenario: Server returns options
- **WHEN** TryUse is called without usageId and multiple valid options exist
- **THEN** InteractionResult includes Options list
- **AND** the client can present choices to the user and retry with selected usageId

### Requirement: Usage Mode Execution
The system SHALL execute specific usage modes when usageId is provided.

#### Scenario: Execute specific usage mode
- **WHEN** TryUseWithMode is called with a valid usageId
- **THEN** the system executes the corresponding usage logic
- **AND** returns success or failure based on execution result

## MODIFIED Requirements

### Requirement: Use Action
The use action SHALL support optional usageId parameter and return usage options when disambiguation is needed.

#### Scenario: Use with usageId
- **WHEN** use action is called with usageId parameter
- **THEN** the system executes that specific usage mode directly
- **AND** does not perform option discovery or disambiguation

#### Scenario: Use without usageId
- **WHEN** use action is called without usageId parameter
- **THEN** if exactly one valid option exists, it executes automatically
- **AND** if multiple options exist, they are returned for selection
- **AND** if no options exist, it returns "No effect"

