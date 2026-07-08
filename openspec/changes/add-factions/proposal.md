## Why

The [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §4.6 (Wave 1) confirms there is no faction/group abstraction: `RelationshipMatrix` (verified, `Aetherium.Server/Narrative/Social/RelationshipMatrix.cs`) is a pairwise NPC-to-NPC graph — a per-*individual* relationship layer, not a group. There is no notion of a faction as a first-class entity, no shared standing between a player and a group, and `WorldAclGrain`'s ACL entries (verified) are bare `PlayerId`s with no faction-as-principal concept.

## What Changes

- Add `Faction`/`FactionRegistry`/`FactionDoctrine`: a group as first-class data, with a data-driven doctrine mapping action tags to standing deltas — "a pacifist faction ranks you up for peaceful resolutions" is expressed entirely as doctrine data, not engine code.
- Add `Reputation`/`ReputationLedger`: a per-actor, per-faction standing ledger (clamped -1000..+1000, with ranks/flags slots for future rites), whose standing changes are always filtered through the relevant faction's doctrine.
- Add `FactionRelations`: a sparse, **directed** inter-faction disposition matrix (`War`/`Cold`/`Neutral`/`Ally`/`Subordinate`) — directed because "subordinate" is inherently one-directional, unlike the other four dispositions which are naturally bilateral (a `SetMutual` convenience covers that common case).
- **Phase 1 (this change): all of the above are new, additive, plain-C#-class primitives, fully unit-tested (13 tests), in isolation.** `RelationshipMatrix` is untouched and remains the NPC-personal layer; no grain exists, no NPC/player carries a `ReputationLedger`, and `WorldAclGrain` is untouched.
- Phase 2 (follow-up change): decide whether `Faction` becomes an Orleans grain (the design doc's own sketch names `IFactionGrain`) or stays a plain class owned by a narrative/world grain — a persistence and cluster-scoping decision deliberately not made here; attach `ReputationLedger` to `Character`; wire `FactionDisposition` mutations into the `NarrativeConsequenceEngine`; extend `WorldAclGrain`'s principal type to accept faction ids alongside `PlayerId`; wire rites/ranks to grant items/titles/abilities (`add-abilities`, `add-character-progression`).

## Impact

- Affected specs: new capability `factions` (faction registry, reputation ledger, inter-faction disposition)
- Affected code: new `Aetherium.Server/Factions/*.cs`, new tests under `Aetherium.Test/Factions/`. No changes to `RelationshipMatrix.cs`, `IWorldAclGrain.cs`, `WorldAcl` (`Aetherium.Model/Worlds/WorldContracts.cs`), or `NarrativeConsequenceEngine.cs` in this change.
