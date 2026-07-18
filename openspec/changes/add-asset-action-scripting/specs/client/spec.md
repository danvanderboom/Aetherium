## ADDED Requirements

### Requirement: Unity Asset Action Executor
The Unity client SHALL execute asset-action commands received from the server — tweening an entity's scale, playing an Animator clip, and applying a tint or attached effect — and SHALL ignore any action name it does not support without raising an error.

#### Scenario: Unity executes received asset actions
- **WHEN** the Unity client receives a `scale` asset action for a rendered entity
- **THEN** it SHALL tween that entity's transform scale toward the target over the action's duration
- **WHEN** the Unity client receives a `playAnimation` action
- **THEN** it SHALL play the named Animator clip on that entity
- **WHEN** the Unity client receives a `tint` or `attachEffect` action
- **THEN** it SHALL apply the tint color or attach the named effect to that entity

#### Scenario: Unity ignores an unsupported asset action
- **WHEN** the Unity client receives an asset action whose action name it does not support
- **THEN** it SHALL ignore the action as a no-op
- **AND** SHALL continue rendering without error
