## Slice — Data-driven factions, live kill→standing→rank loop

### Data tier (Aetherium.Model.Factions)
- [x] 1.1 `RankRule` (`MinStanding`, `RankId`); `FactionDefinition` (`Id`, `Name`, `Tags`, `DoctrineDeltas` (dict actionTag→delta), `RankRules`, `StartingStanding`)
- [x] 1.2 `FactionDispositionKind` enum (Model mirror of `FactionDisposition`) + `FactionRelationDefinition` (`FromFactionId`, `ToFactionId`, `Disposition`, `Mutual` bool)
- [x] 1.3 `StandingBand` (`Id`, `MinStanding`) — world-level named ranges shared by all the world's factions (docs/factions-reputation.md §3 layer 3)
- [x] 1.4 `FactionConfig` bundle (`Factions`, `Relations`, `Bands`), nullable everywhere it's threaded
- [x] 1.5 DTOs: `ReputationDto` (`FactionId`, `Standing`, `Band?`, `Ranks`), `ReputationLedgerDto` (list), `FactionInfoDto`/`FactionRelationDto`/`FactionsStateDto` for the world-factions read accessor

### Runtime tier (Aetherium.Server.Factions)
- [x] 2.1 `FactionCompiler`: `CompileRegistry(defs) -> FactionRegistry` (doctrine deltas → `FactionDoctrine.SetDelta`), `CompileRelations(defs) -> FactionRelations` (`Mutual` → `SetMutual`, else `SetDisposition`), disposition-kind mapping
- [x] 2.2 `FactionRegistry` gains `All` enumeration (additive, mirrors `ResourcePools.All`)
- [x] 2.3 `RankEvaluator.Apply(Reputation, IEnumerable<RankRule>)` — monotonic grants (threshold met + rank absent → add; never revokes)
- [x] 2.4 `BandResolver.Resolve(standing, bands)` — highest `MinStanding` band at or below the standing; null when no bands declared
- [x] 2.5 `CreatureTypeTag : Component` (`Value` string) — general spawn-identity tag

### Per-world threading
- [x] 3.1 `FactionConfig?` added to `WorldConfig`, `WorldTemplate`, `CreateWorldRequest`; mapped in `GameManagementGrain.CreateWorldAsync` (both paths) and `OrleansWorldHost.CreateWorldAsync`
- [x] 3.2 `WorldGrainState.FactionConfig` (set in `WorldGrain.InitializeAsync`); `AddMapAsync` passes it to `IGameMapGrain.InitializeAsync`'s new optional `factionConfig` param
- [x] 3.3 Persisted on `MapState [Id(14)] FactionConfig?`; `GameMapGrain.InitializeAsync` compiles registry/relations + persists; `OnActivateAsync` rehydrates + recompiles (shared `ApplyFactionConfig`)
- [x] 3.4 `JoinPlayerAsync` stamps a fresh `ReputationLedger` seeded with each faction's `StartingStanding` (only when the world declares factions)
- [x] 3.5 `SpawnEntityAsync` stamps `CreatureTypeTag` with the request's `CreatureType` string

### Live loop + observability (GameMapGrain)
- [x] 4.1 `ApplyFactionAction(actor, actionTag)` — the single standing-mutation chokepoint: `ReputationLedger.ApplyAction` against every registry faction whose doctrine has a nonzero delta for the tag → `RankEvaluator.Apply` per changed reputation. Kill-agnostic; future quest/trade/trespass emitters are one-line callers, and the ECA generalization replaces this method's inside, not its call sites
- [x] 4.2 Kill branch calls it: derive the creature tag (prefer `CreatureTypeTag`, fall back to lowercased type name) → `ApplyFactionAction(killer, "kill:<tag>")`, from the shared `TargetEnteredDying && targetWasMonster` branch in both `AttackAsync` and `UseAbilityAsync` (beside `AwardKillXp`)
- [x] 4.3 `GetReputationAsync(sessionId)` accessor: per-faction standing + band + ranks
- [x] 4.4 `GetFactionsAsync()` accessor: the world's factions (id/name/tags), relations (from/to/disposition), and bands

### Tests + spec
- [x] 5.1 `FactionCompiler` unit tests: defs → registry with working doctrines; relation defs → directed + mutual dispositions; null → empty
- [x] 5.2 `RankEvaluator`/`BandResolver` unit tests: below threshold no grant; at/above grants once; no revocation when standing falls; multiple thresholds; band resolution at/below/between boundaries; no bands → null
- [x] 5.3 Grain integration: a kill (melee AND ability) moves standing per doctrine — two factions reacting oppositely to the same kill; `CreatureTypeTag` distinguishes spawn types sharing a C# class; rank granted when a kill crosses a threshold; band reported and changing as standing moves; starting standings stamped at join; no config → no ledger, kill hook no-ops
- [x] 5.4 Per-world threading: a world's `FactionConfig` reaches every map it creates (initial + `AddMapAsync`); a `CreateWorldRequest.FactionConfig` reaches the created map; `GetFactionsAsync` reports factions + relations + bands
- [x] 5.5 `specs/factions/spec.md` delta: ADDED "Per-World Faction Config", "Faction Action Standing Loop", "Standing Bands", "Declarative Rank Grants", "Faction Relations As Data" + `**Verified by:**` lines
- [x] 5.6 Full build + regression suite green

## Later slices (scoped, not built here — tiers T1–T5 of docs/factions-reputation.md §4)

- [ ] L.1 (T1) Reputation-aware NPC behavior: aggression/targeting driven by the attacker's band and inter-faction disposition (behavior-tree condition nodes)
- [ ] L.2 (T1) Client push signal for standing/band/rank changes (mirrors `PlayerVitalsDto` pattern)
- [ ] L.3 (T2) Non-kill standing sources (quest:, trade:, trespass: tag families) as one-line `ApplyFactionAction` callers; standing decay toward doctrine baseline; party-shared standing credit
- [ ] L.4 (T3) Witness/information-propagation model (only perceived acts emit `witnessed:` variants; rumor spread); `RelationshipMatrix` stacking rule (add-factions 2.6: personal grudge ⊕ faction band)
- [ ] L.5 (T4) `NarrativeConsequenceEngine` mutating `FactionRelations` from world events (add-factions 2.3); live-event invasions keyed to faction state
- [ ] L.6 (T4) `WorldAcl` faction principals (add-factions 2.4); territory/market ownership (bridges §4.8)
- [ ] L.7 (T4) Player faction membership/rites (`Faction.MemberIds` consumers); ranks granting items/titles/abilities via the progression bridge (add-factions 2.5); victim-membership/ally-chain kill tags (`kill_member:<factionId>` family)
- [ ] L.8 (T4) Cross-world/cluster-scoped factions (`IFactionGrain` behind the same config seam) + persistent reputation
- [ ] L.9 (T5) ECA graduation: conditions on doctrine rules (band/location/witness), action lists beyond deltas, faction brains as ECA tile graphs; LLM-agent faction leaders emitting decisions as authored ECA rules/quests
- [ ] L.10 YAML/content-pack pipeline populates `FactionConfig` (no downstream change — the data tier is the seam)
