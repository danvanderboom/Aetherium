## ADDED Requirements

### Requirement: Action Intent Abstraction
The system SHALL provide an `ActionIntent` (stable id + description) and an `ActionIntentCatalog` registry, with a default seed catalog covering the game's real, already-shipped actions (movement, attack, interact-open/close, pickup, drop, use-item, inventory toggle). `ActionIntent` SHALL carry no keybinding or input-device data — binding an intent to a specific key, button, or gesture is a client-side concern.

**Verified by:** `Aetherium.Test.Accessibility.ActionIntentTests.Add_ThenTryGet_ReturnsTheSameIntent`, `.Add_DuplicateId_IsRejected`, `.TryGet_UnknownId_ReturnsFalse`, `.DefaultActionIntents_CoversRealExistingGameActions`

#### Scenario: The default seed covers real, already-shipped actions
- **WHEN** `DefaultActionIntents.Build()` is called
- **THEN** the resulting catalog contains, at minimum, `move`, `attack`, `pickup`, `drop`, and `use_item`

#### Scenario: A duplicate action intent id is rejected
- **WHEN** an `ActionIntent` with an already-registered `Id` is added
- **THEN** the add is rejected

### Requirement: Colorblind Contract Enforcement
The system SHALL provide `SemanticDistinction` (a thing the player must be able to tell apart, marked with the `AccessibilityChannel`s — `Color`/`Shape`/`Label`/`Audio` — that encode it) and `ColorblindLintRule`, which SHALL flag exactly those distinctions encoded by `Color` with no other channel also present. A distinction with no color channel at all SHALL NOT be flagged by this rule, regardless of what other channels it does or doesn't have.

**Verified by:** `Aetherium.Test.Accessibility.ColorblindLintRuleTests.ColorOnly_Distinction_IsAViolation`, `.ColorPlusShape_Distinction_IsNotAViolation`, `.ColorPlusAudioTag_Distinction_IsNotAViolation`, `.NoColorChannel_Distinction_IsNeverAViolation`, `.FindViolations_ChecksEachDistinctionIndependently`

#### Scenario: A distinction encoded only by color is flagged
- **WHEN** a `SemanticDistinction` is marked as encoded by `Color` and no other channel
- **THEN** `ColorblindLintRule.FindViolations` includes its id

#### Scenario: A distinction encoded by color plus another channel is not flagged
- **WHEN** a `SemanticDistinction` is marked as encoded by both `Color` and `Shape` (or `Color` and an `AudioTag`-backed `Audio` channel)
- **THEN** `ColorblindLintRule.FindViolations` does not include its id

#### Scenario: A distinction with no color channel is never flagged by this rule
- **WHEN** a `SemanticDistinction` is marked as encoded only by `Label` (no `Color` channel at all)
- **THEN** `ColorblindLintRule.FindViolations` does not include its id
