# eca-scripting Specification

## Purpose
TBD - created by archiving change add-identity-recognition. Update Purpose after archive.
## Requirements
### Requirement: Character Recognized Trigger
The ECA vocabulary SHALL include a `character_recognized` trigger that fires from the canonical-world recognition sweep, binding the recognizer, the recognized character, both kinds, the effective familiarity, whether this is a first meeting, and the event location.

#### Scenario: Rule fires on recognition
- **WHEN** an encounter-gated recognition event occurs in a world whose rules include a `character_recognized` rule
- **THEN** the rule SHALL be evaluated with the event's bound context
- **AND** its actions SHALL execute through the same execution path as existing triggers

#### Scenario: Vocabulary discovery
- **WHEN** the ECA vocabulary is enumerated
- **THEN** the new trigger and condition tiles SHALL appear with their parameter metadata, and the validator SHALL accept rules using them

### Requirement: Recognition Conditions
The ECA vocabulary SHALL include conditions `recognized_kind_is` (the recognized character's kind matches), `familiarity_at_least` (effective familiarity meets a minimum), and `first_meeting_is` (whether the event is a first meeting).

#### Scenario: Kind filter
- **WHEN** a `character_recognized` rule has `recognized_kind_is` with a kind that does not match the event
- **THEN** the rule SHALL NOT fire

#### Scenario: Familiarity gate
- **WHEN** a rule has `familiarity_at_least` above the event's effective familiarity
- **THEN** the rule SHALL NOT fire

#### Scenario: Stranger vs known
- **WHEN** a rule has `first_meeting_is` set
- **THEN** it SHALL fire only when the event's first-meeting flag matches

### Requirement: Recognition Action Targets
Action targets SHALL include `Recognizer` and `Recognized`, resolvable by existing targeted actions; a target that does not resolve for the current event SHALL cause that action to be skipped, as today.

#### Scenario: Act on the recognized character
- **WHEN** a `character_recognized` rule's action targets `Recognized`
- **THEN** the action SHALL execute against the recognized character's entity

#### Scenario: Mismatched target skips
- **WHEN** a rule action targets `Killer` on a recognition event
- **THEN** the action SHALL be skipped without error

