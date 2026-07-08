## ADDED Requirements

### Requirement: Damage Packets
Combat damage SHALL be represented as a `DamagePacket` — an ordered list of `{tag, amount}` components plus provenance (source entity, delivery kind) — rather than a single flat integer, so a game can define arbitrary campaign-specific damage tags.

**Verified by:** `Aetherium.Test.Combat.ResistancesTests.DamageResolution_SumsAcrossMultipleComponents`, `.DamageResolution_NullResistances_AppliesNoMitigation`

#### Scenario: Packet damage sums across multiple tags
- **WHEN** a `DamagePacket` contains a `{fire, 10}` component and a `{cold, 4}` component
- **THEN** resolving the packet's total (with no resistance) yields `14`

### Requirement: Per-Tag Damage Mitigation
An entity MAY carry a `Resistances` component defining, per damage tag, a flat reduction, a percent reduction, and a minimum floor, applied in that order (flat, then percent, then minimum); mitigation SHALL never reduce damage below zero or amplify it above the original amount.

**Verified by:** `Aetherium.Test.Combat.ResistancesTests.Mitigate_AppliesFlatThenPercent_ThenMinimumFloor`, `.Mitigate_MinimumFloor_AppliesWhenPercentWouldGoLower`, `.Mitigate_NeverExceedsOriginalAmount`, `.Mitigate_NeverGoesNegative`, `.Mitigate_IsPerTag`

#### Scenario: Flat then percent then minimum floor
- **WHEN** a tag's resistance entry is `{flat: 2, percent: 0.5, minimum: 1}` and the incoming amount is `10`
- **THEN** the mitigated amount is `4` (`(10 - 2) * (1 - 0.5) = 4`, above the floor of `1`)

#### Scenario: Minimum floor applies when percent mitigation would go lower
- **WHEN** a tag's resistance entry is `{flat: 9, percent: 0.9, minimum: 2}` and the incoming amount is `10`
- **THEN** the mitigated amount is `2` (the computed `0.1` is floored to the minimum)

#### Scenario: Mitigation cannot amplify damage
- **WHEN** a tag's resistance entry has `minimum` greater than the incoming amount
- **THEN** the mitigated amount equals the original incoming amount, not the minimum

### Requirement: Pluggable Hit Resolution
Hit resolution SHALL be pluggable via an `IHitResolver` interface. The engine SHALL ship at least a deterministic resolver (always hits, never crits) and a probabilistic resolver (attacker accuracy vs target evasion determines hit chance; an independent roll determines a critical, using an injected, seedable random source).

**Verified by:** `Aetherium.Test.Combat.HitResolutionTests.AlwaysHitResolver_AlwaysHits_NeverCrits`, `.RollHitResolver_RollBelowHitChance_Hits`, `.RollHitResolver_RollAboveHitChance_Misses`, `.RollHitResolver_HitChance_IsClampedToRange`, `.RollHitResolver_CritRollBelowCritChance_IsCritical`, `.RollHitResolver_MissingComponents_UseDefaults`

#### Scenario: Deterministic resolver always hits
- **WHEN** `AlwaysHitResolver` resolves a hit between any attacker and target
- **THEN** the result is always a hit and never a critical

#### Scenario: Probabilistic resolver's hit chance derives from accuracy and evasion
- **WHEN** an attacker has `Accuracy = 0.9` and a target has `Evasion = 0.1`, and the injected random source returns a value below `0.8`
- **THEN** the roll is a hit

### Requirement: Status Effects
The system SHALL support entity-on-entity status effects with per-tick behavior and a fixed duration in ticks. Applying a status effect whose id matches an already-active effect on the same entity SHALL refresh its duration rather than stacking a second instance.

**Verified by:** `Aetherium.Test.Combat.StatusEffectSystemTests.Tick_Burning_DealsDamage_AndDecrementsDuration`, `.Tick_ExpiredEffect_IsRemoved`, `.Apply_SameId_RefreshesRatherThanStacks`

#### Scenario: Burning deals damage over time then expires
- **WHEN** a `BurningEffect` with `durationTicks = 2, damagePerTick = 3` is applied to an entity with `Health = 20`, and two world ticks pass
- **THEN** the entity's `Health` is reduced by `3` on each of the two ticks and the effect is removed after the second

#### Scenario: Reapplying the same effect id refreshes duration
- **WHEN** a `BurningEffect` with `durationTicks = 2` is active on an entity and a new `BurningEffect` with `durationTicks = 10` (same id) is applied
- **THEN** exactly one `burning` effect remains active, with `RemainingTicks = 10`

### Requirement: Death State Transition
A lethal hit SHALL NOT remove the target entity from the world immediately. Instead the target SHALL receive a `Dying` component with a tick countdown; once the countdown elapses, the entity SHALL transition to a `Corpse` component, remaining in the world in both states.

**Verified by:** `Aetherium.Test.Combat.DamagePipelineTests.Resolve_LethalHit_EntersDyingState_DoesNotRemoveFromWorld` (Dying entry), `Aetherium.Test.Combat.DeathSystemTests.Tick_CountdownReachesZero_TransitionsToCorpse` (Corpse transition), `.Tick_DecrementsDyingCountdown_WithoutTransitioning`

#### Scenario: Lethal hit enters Dying, not deletion
- **WHEN** a hit reduces a target's `Health` to `0` or below
- **THEN** the target receives a `Dying` component and remains present in `World.Entities`

#### Scenario: Dying countdown transitions to Corpse
- **WHEN** a `Dying` entity's tick countdown reaches zero
- **THEN** the entity's `Dying` component is replaced with a `Corpse` component, and the entity remains present in `World.Entities`

### Requirement: Threat Ledger
A defender MAY carry a `ThreatTable` component crediting cumulative threat per attacker entity id; the table SHALL report the attacker with the highest cumulative threat.

**Verified by:** `Aetherium.Test.Combat.ThreatTableTests.GetTopThreat_ReturnsHighestContributor`, `.AddThreat_Accumulates_PerAttacker`, `.GetTopThreat_EmptyLedger_ReturnsNull`

#### Scenario: Top threat reflects highest cumulative contributor
- **WHEN** attacker A contributes `5` threat, attacker B contributes `12`, and attacker C contributes `7` to the same `ThreatTable`
- **THEN** the table's top-threat query returns attacker B
