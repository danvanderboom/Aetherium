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

### Requirement: 3D Occluded Perception Slab
The server SHALL compute vision over a configurable range of altitude bands (not only the player's band), using per-band sight opacity (`ObstructsView.Opacity`) for a vertical line-of-sight test, and SHALL include only cells that pass the 3D FOV test, each tagged with its relative Z. The `PerceptionDto` schema SHALL remain unchanged.

#### Scenario: Flyer overhead visible through a clear column
- **WHEN** a flyer occupies a band above the viewer and every intervening band at that column has `ObstructsView.Opacity = 0` (open air)
- **THEN** the flyer's cell SHALL pass the 3D FOV test
- **AND** the cell SHALL be included in perception tagged with its positive `relativeZ`

#### Scenario: Flyer hidden by an opaque band between
- **WHEN** a flyer occupies a band above the viewer and an intervening band at that column has an opaque `ObstructsView` (for example a stone bridge)
- **THEN** the flyer's cell SHALL fail the vertical line-of-sight test
- **AND** the flyer's cell SHALL NOT be included in perception
- **AND** the opaque bridge underside SHALL be the visible cell for that column

#### Scenario: Visible through a transparent skylight
- **WHEN** the intervening band has `ObstructsMovement` but `ObstructsView.Opacity = 0` (a glass skylight)
- **THEN** the vertical ray SHALL be treated as clear
- **AND** the cell beyond the skylight SHALL be included in perception tagged with its `relativeZ`

#### Scenario: Level below visible through an open grate
- **WHEN** the viewer looks down a column whose intervening band is open (an open stairwell or grate with no opaque `ObstructsView`)
- **THEN** the cell on the lower band SHALL be included in perception tagged with a negative `relativeZ`
- **WHEN** the same column is solid pavement (opaque `ObstructsView`)
- **THEN** the lower cell SHALL NOT be included in perception

#### Scenario: Configurable band range with unchanged DTO schema
- **WHEN** the per-world slab range is configured as `[focusZ - depthBelow, focusZ + depthAbove]`
- **THEN** the server SHALL evaluate the 3D FOV test across exactly those bands and no others
- **AND** the server SHALL emit visible cells using the existing `PerceptionDto`, `VisualDto`, and `WorldLocationDto` fields (`"x,y,z"` visual keys and `relativeZ`) with no schema change

### Requirement: Adaptive Slab Depth
When adaptive slab depth is enabled for a world, the server SHALL bound the emitted band range each frame to the local vertical extent around the viewer — expanding toward the configured budget to cover occupied bands and collapsing toward single-Z over flat terrain — without ever exceeding the configured `depthBelow`/`depthAbove` budget or the depth cap, and without changing which visible cells are emitted relative to a fixed budget of the same size.

#### Scenario: Collapses over flat terrain
- **WHEN** adaptive slab depth is enabled and no band within the configured budget around the viewer's column is occupied
- **THEN** the server SHALL evaluate only the focus band (effective depth 0 in both directions)

#### Scenario: Expands to cover an interchange
- **WHEN** adaptive slab depth is enabled and the furthest occupied band within the budget is `k` bands away in a direction
- **THEN** the server SHALL evaluate that direction to exactly `k` bands
- **AND** SHALL emit the same visible cells it would emit with a fixed budget large enough to include band `k`

#### Scenario: Never exceeds the configured budget
- **WHEN** adaptive slab depth is enabled and content exists beyond the configured `depthBelow`/`depthAbove` budget (or the depth cap)
- **THEN** the server SHALL NOT expand the evaluated range past that budget (or cap)

### Requirement: Flight Envelope in Perception
When the perceiving entity has a Flight component, the server SHALL surface its altitude envelope on the perception — the min/max bands, the current band (as an absolute Z, since relative perception reports the player at Z 0), and the flight state — as an additive, non-breaking field that is absent for non-flyers.

#### Scenario: Envelope present for a flyer
- **WHEN** perception is computed for a perceiver that has a Flight component with bands `[MinBand, MaxBand]` and is at band `z`
- **THEN** the perception SHALL include a flight envelope reporting `MinBand`, `MaxBand`, current band `z`, and the flight state

#### Scenario: Envelope absent for a non-flyer
- **WHEN** the perceiver has no Flight component (or no perceiving entity is supplied)
- **THEN** the perception SHALL omit the flight envelope (null), leaving the DTO otherwise unchanged

### Requirement: Context Tint by Band
When context tint is enabled for a world, the server SHALL derive the default lighting mode from the viewer's band — underground bands enclosed/torch-lit, skyway bands sunlit, surface bands ambient — reusing the existing lighting modes with no new machinery. Opt-in; when disabled the caller's requested lighting mode is used unchanged.

#### Scenario: Underground reads as enclosed
- **WHEN** context tint is enabled and the viewer is on a band below ground (Z < 0)
- **THEN** perception SHALL report the torch (enclosed) lighting mode regardless of the requested mode

#### Scenario: Skyway reads as sunlit
- **WHEN** context tint is enabled and the viewer is on a band at/above the sky threshold
- **THEN** perception SHALL report the sunlight lighting mode

#### Scenario: Disabled leaves the requested mode
- **WHEN** context tint is disabled
- **THEN** perception SHALL report exactly the caller's requested lighting mode

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
absent). Both live perception paths SHALL supply `self`: `GameMapGrain.ComputeAgentPerceptionAsync`
passes the resolved player character (the agent JSON path), and `GameSession.GetPerception` passes
the session's player (the path behind every `ReceivePerceptionUpdate` hub push), so a live
player/agent frame includes interoception on either route.

**Verified by:** `Aetherium.Test.Perception.InteroceptionTests.Interoception_Health_ReflectsSelfLevelAndMax`, `.Interoception_Statuses_ListSelfActiveStatuses_WithRemainingTicks`, `.Interoception_Pools_CarryTagCurrentMaxAndInverseFlag`, `.Interoception_Cooldowns_ListOnlyAbilitiesStillOnCooldown_WithRemainingTicks`, `.ComputeAgentPerceptionAsync_IncludesInteroceptionForThePlayer`, `Aetherium.Client.Tests.InProcServerIntegrationTests.LiveFrame_CarriesInteroception_ThroughTheHubPush`

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

#### Scenario: Hub-pushed frames feel the body too

- **WHEN** the server pushes `ReceivePerceptionUpdate` to a connected client (computed via `GameSession.GetPerception`)
- **THEN** the frame's `Interoception` is populated from the session player's own components

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

