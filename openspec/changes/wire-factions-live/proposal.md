## Why

`add-factions` (Wave 1 §4.6) shipped `Faction`/`FactionRegistry`/`FactionDoctrine`/`Reputation`/`ReputationLedger`/`FactionRelations` as pure, unreferenced primitives — confirmed by grep, nothing outside `Aetherium.Server/Factions/` and its own tests touches them. No entity carries a `ReputationLedger`, no standing ever changes in gameplay, `Reputation.Ranks` has no consumer, and the grain-vs-plain-class ownership question was left deliberately open.

Two things have changed since that slice shipped. First, the per-world-data pattern (`DeathPolicy` → `AbilityConfig` → `ProgressionConfig`) now answers the ownership question without inventing an `IFactionGrain`: factions are per-world *data* compiled into plain runtime classes held by each map grain, with only the config persisted. Second, the kill chokepoint that abilities and progression share (`TargetEnteredDying && targetWasMonster`, feeding `AwardKillXp`) is exactly where "standing changes via actions filtered through the faction's doctrine" hooks — the same one helper, both melee and ability kills.

One genuine gap surfaced by code survey: spawned monsters carry **no** identity beyond their C# class name — the spawn request's `CreatureType` string ("wolf", "bandit", "zombie") is discarded at construction, so every generic monster collapses to "Monster". Doctrine rules keyed on what you killed need that string preserved.

## What Changes

- **New data tier (`Aetherium.Model.Factions`):** `FactionConfig` bundling `FactionDefinition`s (id, name, tags, doctrine deltas keyed by action tag, declarative `RankRule`s, starting standing) and `FactionRelationDefinition`s (directed disposition pairs with a `Mutual` convenience flag). All `[GenerateSerializer]`, reachable from `WorldConfig`/`WorldTemplate` like its three predecessors. Engine ships zero factions.
- **Compiler (`Aetherium.Server.Factions.FactionCompiler`):** config → runtime `FactionRegistry` (building each `FactionDoctrine` from its delta dictionary) + `FactionRelations`; `FactionRegistry` gains an `All` enumeration (additive, like `ResourcePools.All`). A small `RankEvaluator` grants ranks from declarative thresholds.
- **Per-world threading:** `FactionConfig` rides `WorldConfig`/`WorldTemplate`/`CreateWorldRequest` → `WorldGrainState` → `AddMapAsync` → `IGameMapGrain.InitializeAsync` → persisted on `MapState`, rehydrated in `OnActivateAsync`. `JoinPlayerAsync` stamps a fresh `ReputationLedger` seeded with each faction's starting standing.
- **Creature identity preserved:** a new `CreatureTypeTag` component stamps the spawn request's `CreatureType` string onto the spawned entity, so "wolf" and "bandit" stay distinguishable from the shared `Monster` class. Kill-site code prefers the tag, falling back to the C# type name.
- **The standing loop:** at the shared monster-defeat chokepoint (both `AttackAsync` and `UseAbilityAsync`, beside `AwardKillXp`), `ApplyKillStanding` emits the action tag `kill:<creature-type>` and applies it through **every** configured faction's doctrine via the shipped `ReputationLedger.ApplyAction` — so a town that hates zombies and a necromancer cult that reveres them react oppositely to the same kill, purely as data. Rank rules re-evaluate after each change (monotonic grants).
- **Observability:** `GetReputationAsync(sessionId)` (per-faction standing + ranks) and `GetFactionsAsync()` (the world's factions + relations) read accessors.

## Impact

- Affected specs: `factions` (new live-wiring requirements).
- Affected code: `Aetherium.Model/Factions/*` (new), `Aetherium.Server/Factions/*` (new compiler/evaluator; `FactionRegistry.All` additive), `Aetherium.Server/Components or Entities` (new `CreatureTypeTag`), `Aetherium.Server/MultiWorld/{GameMapGrain,IGameMapGrain,WorldGrain,WorldModels}.cs`, `Aetherium.Model/Worlds/WorldContracts.cs`, `Aetherium.Server/Management/GameManagementGrain.cs`, `Aetherium.Server/Services/OrleansWorldHost.cs`.
- Explicitly deferred (see design.md Non-Goals): reputation-aware NPC hostility; `WorldAcl` faction principals; `NarrativeConsequenceEngine` disposition mutations; `RelationshipMatrix` stacking; ranks unlocking items/titles/abilities; player faction membership/joining (rites); non-kill standing sources; cross-world/cluster-scoped factions; client push signal.
