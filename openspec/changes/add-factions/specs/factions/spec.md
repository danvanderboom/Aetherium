## ADDED Requirements

### Requirement: Faction Registry
The system SHALL provide a `Faction` (id, name, tags, a `FactionDoctrine`, member ids) and a `FactionRegistry` that rejects a duplicate `Id`.

**Verified by:** `Aetherium.Test.Factions.FactionRegistryTests.Add_ThenTryGet_ReturnsTheSameFaction`, `.Add_DuplicateId_IsRejected`, `.Faction_AddMember_ThenIsMember_ReflectsMembership`

#### Scenario: A registered faction is retrievable by id
- **WHEN** a `Faction` with `Id = "merchants_guild"` is added to a `FactionRegistry`
- **THEN** `TryGet("merchants_guild", ...)` returns it with its name and tags intact

#### Scenario: A duplicate faction id is rejected
- **WHEN** a `Faction` with an already-registered `Id` is added
- **THEN** the add is rejected

### Requirement: Reputation Ledger
The system SHALL provide `ReputationLedger`, tracking one `Reputation` per faction an actor has interacted with. `ApplyAction` SHALL adjust standing by the acting faction's `FactionDoctrine`-derived delta for the given action tag (zero if the doctrine has no rule for that tag), clamped to [-1000, +1000], independently per faction.

**Verified by:** `Aetherium.Test.Factions.ReputationLedgerTests.ApplyAction_UsesDoctrineDelta_ToAdjustStanding`, `.ApplyAction_DifferentFactionDoctrines_ReactDifferently_ToTheSameAction`, `.ApplyAction_UnknownActionTag_AppliesZeroDelta`, `.Standing_ClampsAtMaximum`, `.Standing_ClampsAtMinimum`, `.ApplyAction_Repeated_Accumulates`

#### Scenario: Two factions react oppositely to the same action
- **WHEN** a pacifist faction's doctrine assigns `violence` a delta of `-20` and a militant faction's doctrine assigns it `+15`, and the same action is applied to both
- **THEN** the actor's standing with the pacifist faction decreases while standing with the militant faction increases

#### Scenario: Standing never exceeds the maximum or minimum
- **WHEN** a doctrine's delta for an action would push standing above `+1000` or below `-1000`
- **THEN** the resulting standing is clamped to that bound, not the raw computed value

#### Scenario: An action tag with no doctrine rule leaves standing unchanged
- **WHEN** `ApplyAction` is called with an action tag the faction's doctrine has no rule for
- **THEN** standing is unchanged (delta of zero)

### Requirement: Inter-Faction Disposition
The system SHALL provide `FactionRelations`, a directed sparse matrix of `FactionDisposition` (`War`/`Cold`/`Neutral`/`Ally`/`Subordinate`) between faction pairs, defaulting to `Neutral` for any unset pair. Setting a disposition in one direction SHALL NOT automatically set it in the reverse direction; a separate `SetMutual` helper SHALL set both directions at once for the naturally-bilateral dispositions.

**Verified by:** `Aetherium.Test.Factions.FactionRelationsTests.GetDisposition_UnsetPair_DefaultsToNeutral`, `.SetDisposition_IsDirected_NotAutomaticallyMirrored`, `.SetMutual_AppliesToBothDirections`, `.SetDisposition_CanBeChangedOverTime`

#### Scenario: An unset faction pair defaults to Neutral
- **WHEN** `GetDisposition` is queried for a faction pair that has never been set
- **THEN** it returns `Neutral`

#### Scenario: Subordinate is directional
- **WHEN** faction A is set `Subordinate` to faction B
- **THEN** `GetDisposition(A, B)` returns `Subordinate` while `GetDisposition(B, A)` returns `Neutral` (unless separately set)

#### Scenario: SetMutual sets both directions for a bilateral disposition
- **WHEN** `SetMutual` sets `War` between faction A and faction B
- **THEN** both `GetDisposition(A, B)` and `GetDisposition(B, A)` return `War`
