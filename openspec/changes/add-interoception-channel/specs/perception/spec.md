# perception Specification (Delta)

## ADDED Requirements

### Requirement: Interoception Data Model

`Aetherium.Model.InteroceptionDto` SHALL model a character's self-sense as pure serializable data:
`Health` and `MaxHealth`; a `Statuses` list of `SelfStatusDto` (`Id`, `RemainingTicks`); a `Pools`
list of `ResourcePoolStateDto` (`Tag`, `Current`, `Max`, `IsInverse`); and a `Cooldowns` list of
`AbilityReadinessDto` (`AbilityId`, `RemainingTicks`). `PerceptionDto.Interoception` SHALL be an
optional (`nullable`) block, so a frame without a self-sense is byte-identical on the wire to a
pre-change frame.

**Verified by:** `Aetherium.Test.Perception.InteroceptionTests.InteroceptionDto_SerializesAndRoundTrips_PascalCaseJson`, `.PerceptionDto_Interoception_DefaultsToNull`

#### Scenario: Interoception block round-trips on the wire

- **WHEN** an `InteroceptionDto` with health, statuses, pools, and cooldowns is serialized to JSON and back with the same System.Text.Json/PascalCase settings the hubs use
- **THEN** every field round-trips, and a `PerceptionDto` with no self-sense serializes with `Interoception` null

### Requirement: Interoception Channel in Perception

A perception frame SHALL carry the perceiving character's own body state when — and only when — the
service is given that character as the perceiving `self`. `PerceptionService.ComputePerception` SHALL
accept an optional `self` entity and, when supplied, populate `PerceptionDto.Interoception` by
projecting the character's own `Health` (level/max), `StatusEffects` (each active status's id and
remaining ticks), `ResourcePools` (each pool's tag, current, max, and inverse flag), and
`AbilityCooldowns` (each ability still cooling down and its remaining ticks; a ready ability is
absent). `GameMapGrain.ComputeAgentPerceptionAsync` SHALL pass the resolved player character as `self`
so a live player/agent frame includes interoception.

**Verified by:** `Aetherium.Test.Perception.InteroceptionTests.Interoception_Health_ReflectsSelfLevelAndMax`, `.Interoception_Statuses_ListSelfActiveStatuses_WithRemainingTicks`, `.Interoception_Pools_CarryTagCurrentMaxAndInverseFlag`, `.Interoception_Cooldowns_ListOnlyAbilitiesStillOnCooldown_WithRemainingTicks`, `.ComputeAgentPerceptionAsync_IncludesInteroceptionForThePlayer`

#### Scenario: Own health is felt

- **WHEN** `ComputePerception` is called with a `self` character whose `Health` is 12 of 40
- **THEN** `Interoception.Health == 12` and `Interoception.MaxHealth == 40`

#### Scenario: Own statuses are felt with remaining duration

- **WHEN** the self character has an active `burning` status with 3 ticks remaining
- **THEN** `Interoception.Statuses` contains `{ Id = "burning", RemainingTicks = 3 }`

#### Scenario: Resource pools distinguish drain from fill

- **WHEN** the self character carries a normal `charge` pool and a heat-style inverse pool
- **THEN** each appears in `Interoception.Pools` with its `Tag`, `Current`, `Max`, and an `IsInverse` flag that is true only for the heat pool

#### Scenario: Only unready abilities are listed

- **WHEN** the self character has one ability on cooldown (2 ticks) and one ready ability
- **THEN** `Interoception.Cooldowns` contains only the unready ability with `RemainingTicks == 2`

#### Scenario: Live player frame carries interoception

- **WHEN** `GameMapGrain.ComputeAgentPerceptionAsync(entityId)` computes a player's frame
- **THEN** the returned perception's `Interoception` is populated from that player's own components

### Requirement: Interoception Is Self-Only and Fail-Safe

The interoception block SHALL reflect only the perceiving character's own components and SHALL never
expose any other entity's internal state. When `self` is omitted, `PerceptionDto.Interoception` SHALL
be `null` (leaving every existing `ComputePerception` caller behavior-identical). Component reads
SHALL be guarded so that a `self` character missing a given component yields an empty projection for
that field rather than an error.

**Verified by:** `Aetherium.Test.Perception.InteroceptionTests.Interoception_SelfOnly_DoesNotReflectAnotherEntitysState`, `.Interoception_NullWhenNoSelfProvided_LegacyCallersUnaffected`, `.Interoception_MissingComponents_DegradeToEmpty_WithoutThrowing`

#### Scenario: A second wounded character does not leak into my self-sense

- **WHEN** another wounded character with its own statuses stands in the same frame as the perceiver
- **THEN** `Interoception` reflects only the perceiver's own health and statuses, never the other character's

#### Scenario: Legacy call has no interoception

- **WHEN** `ComputePerception` is called by an existing caller that passes no `self`
- **THEN** `PerceptionDto.Interoception` is `null` and all other frame fields are unchanged

#### Scenario: Missing component does not throw

- **WHEN** the `self` character has no `ResourcePools` component
- **THEN** `Interoception.Pools` is an empty list and no exception is raised
