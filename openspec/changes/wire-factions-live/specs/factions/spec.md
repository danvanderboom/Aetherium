## ADDED Requirements

### Requirement: Per-World Faction Config
Factions SHALL be per-world data, not an engine-hardcoded set. A world's `FactionConfig` (faction definitions with doctrine deltas, rank rules and starting standing; directed relation definitions; world-level standing bands) SHALL be specifiable at world-creation time (via `WorldConfig.FactionConfig` or `WorldTemplate.FactionConfig`/`CreateWorldRequest.FactionConfig`), persisted per-world, and applied to every map the world creates — both the initial map and any map added later. A map on a world with no `FactionConfig` SHALL stamp no `ReputationLedger` onto joining characters and SHALL treat faction actions as no-ops. The engine SHALL ship zero factions; all faction content is campaign-supplied data.

`FactionConfig` SHALL be pure serializable data; a server-side `FactionCompiler` SHALL compile it into the runtime tier (a `FactionRegistry` with per-faction `FactionDoctrine`s, and a `FactionRelations` matrix). A joining character SHALL receive a fresh `ReputationLedger` seeded with each faction's configured starting standing.

**Verified by:** `Aetherium.Test.Factions.FactionCompilerTests.CompileRegistry_ProducesFactionsWithWorkingDoctrines`, `.CompileRegistry_Null_ProducesEmptyRegistry`, `Aetherium.Test.Factions.PerWorldFactionConfigTests.WorldFactionConfig_ReachesEveryMapItCreates`, `.CreateWorldRequest_FactionConfig_ReachesTheCreatedMap`, `Aetherium.Test.Factions.FactionStandingLiveTests.StartingStandings_StampedAtJoin`, `.NoFactionConfig_NoLedger_KillHookNoOps`

#### Scenario: A world's faction config reaches every map it creates
- **WHEN** a world is initialized with a `FactionConfig` and creates both an initial map and a later map via `AddMapAsync`
- **THEN** a player joining either map carries a `ReputationLedger` seeded with the config's factions and starting standings

#### Scenario: No faction config means no factions
- **WHEN** a world is initialized with `FactionConfig` left null
- **THEN** joining characters carry no `ReputationLedger`, kills change no standings, and `GetReputationAsync` reports empty

### Requirement: Faction Action Standing Loop
All standing mutation SHALL flow through a single per-map chokepoint, `ApplyFactionAction(actor, actionTag)`, which applies the tag against **every** configured faction's doctrine (`ReputationLedger.ApplyAction`) — so factions judge the same act independently, including oppositely — and then re-evaluates the actor's rank grants. Doctrine tags SHALL carry no engine-assigned moral valence; a faction with no rule for a tag is unaffected.

When a player defeats a monster, the map SHALL call the chokepoint with the engine-emitted action tag `kill:<creature-type>`, where the creature type prefers the entity's spawn-time `CreatureTypeTag` (stamped by `SpawnEntityAsync` from the request's `CreatureType` string) and falls back to the lowercased C# type name. The award SHALL apply identically whether the kill came from a melee `AttackAsync` or an ability `UseAbilityAsync`, and SHALL credit the killer only.

**Verified by:** `Aetherium.Test.Factions.FactionStandingLiveTests.MonsterKill_Melee_MovesStanding_OppositelyForTwoFactions`, `.MonsterKill_Ability_MovesStanding_SameAsMelee`, `.CreatureTypeTag_DistinguishesSpawnTypes_SharingAClass`

#### Scenario: Two factions judge the same kill oppositely
- **WHEN** a player kills a "zombie" under a config where the town's doctrine maps `kill:zombie → +5` and the cult's maps `kill:zombie → -10`
- **THEN** the killer's town standing rises by 5 and cult standing falls by 10 from the same kill, and a third faction with no `kill:zombie` rule is unaffected

#### Scenario: An ability kill is judged identically to a melee kill
- **WHEN** the same lethal blow is delivered via `UseAbilityAsync` instead of `AttackAsync`
- **THEN** the identical `kill:` tag is emitted and the same standing deltas apply

#### Scenario: Spawn-time creature identity survives to the kill site
- **WHEN** two monsters sharing the `Monster` C# class are spawned with `CreatureType` "wolf" and "bandit", and the config's doctrine has a rule only for `kill:bandit`
- **THEN** killing the bandit moves standing and killing the wolf does not

### Requirement: Standing Bands
A world MAY declare named standing bands (`StandingBand {Id, MinStanding}`) shared across all its factions. A reputation's current band SHALL resolve to the band with the highest `MinStanding` at or below the standing value, SHALL be reported on the reputation read model, and SHALL change as standing moves across boundaries. Bands are the public vocabulary future consumers (NPC behavior, prices, access control) bind to in place of raw numbers; a world declaring no bands SHALL report no band.

**Verified by:** `Aetherium.Test.Factions.RankAndBandTests.BandResolver_ResolvesHighestBandAtOrBelowStanding`, `.BandResolver_NoBands_ReturnsNull`, `Aetherium.Test.Factions.FactionStandingLiveTests.StandingBand_ReportedAndChanges_AsStandingMoves`

#### Scenario: Band resolution picks the highest threshold at or below standing
- **WHEN** bands `hostile(-1000)`, `neutral(-100)`, `friendly(+200)` are declared and an actor's standing is `+50`
- **THEN** the actor's band with that faction is `neutral`; at `+200` it becomes `friendly`

#### Scenario: A kill can move an actor across a band boundary
- **WHEN** an actor's standing sits just below a band threshold and a kill's doctrine delta pushes it past
- **THEN** the reputation read model reports the new band

### Requirement: Declarative Rank Grants
A faction definition MAY declare rank rules (`RankRule {MinStanding, RankId}`). After any standing change, every rule whose threshold is at or below the new standing SHALL grant its rank if not already held. Grants SHALL be monotonic — a rank, once granted, is not revoked when standing later falls. Ranks SHALL be reported on the reputation read model; rank *effects* (items, titles, abilities) are a later slice.

**Verified by:** `Aetherium.Test.Factions.RankAndBandTests.RankEvaluator_BelowThreshold_NoGrant`, `.RankEvaluator_AtOrAboveThreshold_GrantsOnce`, `.RankEvaluator_DoesNotRevoke_WhenStandingFalls`, `Aetherium.Test.Factions.FactionStandingLiveTests.KillCrossingThreshold_GrantsRank`

#### Scenario: Crossing a rank threshold grants the rank once
- **WHEN** an actor's standing crosses a faction's `RankRule` threshold via a kill, and further kills raise it higher
- **THEN** the rank is granted exactly once and retained even if standing subsequently falls below the threshold

### Requirement: Faction Relations As Data
A world's `FactionConfig` MAY declare directed inter-faction relations (`FactionRelationDefinition {From, To, Disposition, Mutual}`); `Mutual = true` SHALL set the disposition in both directions. The compiled `FactionRelations` matrix and the world's factions and bands SHALL be observable via a `GetFactionsAsync` read accessor. No gameplay system consumes dispositions in this slice — the matrix is threaded, compiled data awaiting its first consumer (reputation-aware NPC behavior or consequence-engine mutation, per docs/factions-reputation.md tiers T1/T4).

**Verified by:** `Aetherium.Test.Factions.FactionCompilerTests.CompileRelations_DirectedAndMutual`, `Aetherium.Test.Factions.PerWorldFactionConfigTests.GetFactionsAsync_ReportsFactionsRelationsAndBands`

#### Scenario: Directed and mutual relations compile correctly
- **WHEN** a config declares `empire → vassal: Subordinate` (directed) and `town ↔ cult: War` (mutual)
- **THEN** the vassal is subordinate to the empire but not vice versa, and town/cult report War in both directions
