## 1. Faction/reputation primitives (Phase 1 — this change)

- [x] 1.1 `Faction`/`FactionRegistry`/`FactionDoctrine` (data-driven standing-delta-by-action-tag)
- [x] 1.2 `Reputation`/`ReputationLedger` (clamped -1000..+1000, doctrine-filtered `ApplyAction`)
- [x] 1.3 `FactionRelations` (directed disposition matrix + `SetMutual` convenience)
- [x] 1.4 Unit tests (13 tests): faction add/get/duplicate-rejection/membership, doctrine-driven standing changes (including two factions reacting oppositely to the same action), clamping at both bounds, accumulation across repeated actions, unset-pair defaults to Neutral, directed vs. mutual disposition setting
- [x] 1.5 `openspec/specs/factions/spec.md` delta: ADDED requirements, each with a `**Verified by:**` line

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Decide grain-vs-plain-class persistence for `Faction` (the design doc names `IFactionGrain`; a plain class held by an existing grain is the alternative)
- [ ] 2.2 Attach `ReputationLedger` to `Character` construction
- [ ] 2.3 Wire `FactionDisposition` mutations into the `NarrativeConsequenceEngine` so world events shift inter-faction relations over time
- [ ] 2.4 Extend `WorldAcl` (`Aetherium.Model/Worlds/WorldContracts.cs`) to accept a faction id as a principal alongside `PlayerId`
- [ ] 2.5 Design and implement rites/ranks actually granting items/titles/abilities from `Reputation.Ranks`
- [ ] 2.6 Decide the relationship between `RelationshipMatrix` (NPC-personal) and this faction layer (e.g. does an NPC's personal disposition modifier stack with their faction's reputation-derived disposition?)
