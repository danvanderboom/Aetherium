## Context

`RelationshipMatrix` (verified) is a plain C# class, not a grain, holding a symmetric NPC-id → NPC-id → float graph — the *personal* layer. §4.6 specs a *group* layer above it: factions as first-class entities with reputation and inter-faction politics. This change ships that group layer as plain data; see [proposal.md](proposal.md) for why grain persistence, ACL integration, and narrative wiring are a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - `FactionDoctrine` fully data-driven — two factions with opposite values (pacifist vs. militant) differ only in the dictionary they're constructed with, never in engine code.
  - `Reputation`/`ReputationLedger` clamped correctly at both ends, independent per faction.
  - `FactionRelations` models the asymmetric case (`Subordinate`) correctly, not just the symmetric ones — this was the one place a naive symmetric-matrix copy of `RelationshipMatrix`'s shape would have been wrong.
- Non-Goals (Phase 2 / later):
  - Whether `Faction` becomes an `IFactionGrain` (as the design doc's own sketch names it) or stays a plain class held by an existing grain (e.g. `ClusterGrain`). This is a real architectural fork — grains buy per-faction Orleans persistence/concurrency at the cost of cross-grain call overhead for every reputation check — and deserves its own decision once a live caller exists, not a guess now.
  - Attaching `ReputationLedger` to any live entity.
  - Wiring `FactionDisposition` changes from world events (the `NarrativeConsequenceEngine` mutating relations over time, per the design doc).
  - Extending `WorldAcl` (`Aetherium.Model/Worlds/WorldContracts.cs`) to accept a faction id as a principal alongside `PlayerId` — a real breaking-ish change to an existing DTO, out of scope for a schema-only Phase 1.
  - Rites/ranks actually unlocking anything (items, titles, abilities) — `Reputation.Ranks` is a bare `List<string>` slot today with no consumer.

## Decisions

- **`FactionRelations` is directed, with a `SetMutual` convenience — not symmetric like `RelationshipMatrix`.** The design doc's own disposition list includes `Subordinate`, which cannot be modeled symmetrically (a vassal state being subordinate to an empire does not make the empire subordinate to the vassal). Copying `RelationshipMatrix`'s symmetric-by-construction shape here would have silently produced a wrong model for exactly the disposition that most needed direction.
- **`FactionDoctrine.DeltaFor` returns `0` for an unrecognized action tag**, not throwing or requiring every tag to be pre-registered. A faction doctrine describing only the tags it cares about (a merchant guild might have zero rules about "arcane_ritual_performed") is the common case; treating "no rule" as "no effect" avoids forcing every doctrine to enumerate every possible action tag in the game.
- **`Faction` is a plain class holding a `HashSet<string> MemberIds`**, not entity references — same reasoning as `ThreatTable`/`RelationshipMatrix` using bare string ids: it keeps the faction model decoupled from whichever entity/grain type ends up representing members, which Phase 2's grain-vs-plain-class decision might change.

## Risks / Trade-offs

- **No live entity or grain uses any of this yet.** Zero risk to running gameplay — `Aetherium.Server/Factions/` is new and unreferenced outside its own tests.
- **The grain-vs-plain-class question is explicitly unresolved**, meaning Phase 2 has real design work ahead of it, not just wiring. Accepted: guessing wrong here (e.g. building `IFactionGrain` now) would be harder to unwind than deferring the decision to when a real caller (the ACL/narrative integration) forces it.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) makes the grain-vs-plain-class call and wires factions into `WorldAcl`/`NarrativeConsequenceEngine`/character entities.

## Open Questions

- Should `Reputation.Ranks`/`Flags` be populated by threshold rules on `FactionDoctrine` (e.g. "standing ≥ 500 grants rank X") baked into this schema now, or left for Phase 2 to design once rites actually unlock something? Left for Phase 2 — no consumer exists yet to validate the shape against.
