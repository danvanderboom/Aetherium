## Slice — Data-driven progression, live XP→level→skill→grant loop

### Data tier (Aetherium.Model.Progression)
- [x] 1.1 `LevelCurveKind` (Linear) + `LevelCurveDefinition` (kind + `XpPerLevel`); `ProgressPoolDefinition` (`Id`, curve, `StartingXp`/`StartingLevel`)
- [x] 1.2 `SkillDefinitionData` (serializable mirror of `SkillDefinition`: id, description, prerequisites, `UnlocksAbilityId?`, `ModifiesAttributeId?`, `ModifierAmount`, + optional `RequiredPoolId`/`RequiredLevel`)
- [x] 1.3 `XpAwardEvent` enum (`MonsterDefeated`) + `XpAwardRule` (`OnEvent`, `PoolId`, `Amount`, optional `EnemyTypeFilter`)
- [x] 1.4 `DerivedStat` enum (`HealthMax`, `ActionSpeed`) + `AttributeDerivation` (`AttributeId`, `DerivedStat`, `PerPoint`, `Base`)
- [x] 1.5 `ProgressionConfig` bundle (`Pools`, `Skills`, `StartingAttributes` (dict), `StartingRoleAffinity` (dict), `XpAwardRules`, `AttributeDerivations`, `RequireSkillToCastAbilities` bool); `ProgressionStateDto`/`ProgressPoolDto`/`UnlockSkillResultDto` read/return DTOs

### Runtime tier (Aetherium.Server.Progression)
- [x] 2.1 `ProgressionCompiler`: `CompileSkillCatalog(defs) -> SkillCatalog`; `BuildProgressPools(defs) -> (ProgressPools, curvesById)`; `BuildAttributes/BuildRoleAffinity(dicts)`; curve def → `ILevelCurve` (Linear → `LinearLevelCurve`)
- [x] 2.2 `GrantedAbilities : Component` (per-player set of granted ability ids; `Has`/`Grant`/enumerate)
- [x] 2.3 `SkillUnlockService` extended (or a thin wrapper) to also check `RequiredPoolId`/`RequiredLevel` against the actor's `ProgressPools` — new `SkillUnlockResult.PoolLevelTooLow`

### Per-world threading
- [x] 3.1 `ProgressionConfig?` added to `WorldConfig`, `WorldTemplate`, `CreateWorldRequest`; mapped in `GameManagementGrain.CreateWorldAsync` (both paths) and `OrleansWorldHost.CreateWorldAsync`
- [x] 3.2 `WorldGrainState.ProgressionConfig` (set in `WorldGrain.InitializeAsync`); `AddMapAsync` passes it to `IGameMapGrain.InitializeAsync`'s new optional `progressionConfig` param
- [x] 3.3 Persisted on `MapState [Id(13)] ProgressionConfig?`; `GameMapGrain.InitializeAsync` compiles catalog/curves + stores config + persists; `OnActivateAsync` rehydrates + recompiles (shared `ApplyProgressionConfig`)
- [x] 3.4 `JoinPlayerAsync` stamps `ProgressPools`/`Attributes`/`UnlockedSkills`/`RoleAffinity`/`GrantedAbilities` from config; applies `AttributeDerivation`s once (initial derived stats)

### Live loop + observability (GameMapGrain)
- [x] 4.1 `AwardKillXp(killer, defeatedType)` helper: applies matching `XpAwardRule`s (`MonsterDefeated`, `EnemyTypeFilter`) → `ProgressPools.AddXp(pool, amount, curve)`; called from the shared `TargetEnteredDying && targetWasMonster` branch in both `AttackAsync` and `UseAbilityAsync`
- [x] 4.2 `UnlockSkillAsync(sessionId, skillId)`: `SkillUnlockService.TryUnlock` (prereq + `RequiredPoolLevel`) → on success apply effects: `ModifiesAttributeId` → `Attributes.Set` (+ re-derive), `UnlocksAbilityId` → `GrantedAbilities.Grant`; returns `UnlockSkillResultDto`
- [x] 4.3 `UseAbilityAsync` gains a `GrantedAbilities` gate **only when** `_progressionConfig?.RequireSkillToCastAbilities == true` (default false = unchanged)
- [x] 4.4 `ApplyAttributeDerivations(player)` helper (read attribute → write `Health.MaxLevel`/`ActionSpeed.Speed`), called at join and after skill-modify
- [x] 4.5 `GetProgressionAsync(sessionId)` accessor: pools (id/xp/level), attributes, unlocked skills, granted abilities

### Tests + spec
- [x] 5.1 `ProgressionCompiler` unit tests: pool defs → working pools + curves; skill defs → catalog; curve mapping; attribute/role dicts
- [x] 5.2 `SkillUnlockService` `RequiredPoolLevel` unit tests: below level rejected, at/above accepted, no-requirement unaffected
- [x] 5.3 Grain integration: a monster kill (melee AND ability) awards XP and levels the pool per `XpAwardRule`; `UnlockSkillAsync` respects prereq + pool-level gates; unlocking a skill grants its ability and modifies its attribute; `AttributeDerivation` sets max health at join; skill-modify re-derives; `RequireSkillToCastAbilities` true → ungranted cast rejected, granted cast allowed; false → any catalog ability castable
- [x] 5.4 Per-world threading: a world's `ProgressionConfig` reaches every map it creates (initial + `AddMapAsync`); a `CreateWorldRequest.ProgressionConfig` reaches the created map; no config → no progression components stamped
- [x] 5.5 `specs/character-progression/spec.md` delta: ADDED "Per-World Progression Config", "XP Award On Kill", "Skill Unlock & Ability Grant", "Attribute-Derived Stats" + `**Verified by:**` lines; `specs/abilities/spec.md` delta: MODIFIED cast gate for optional skill-gating
- [x] 5.6 Full build + regression suite green

## Later slices (scoped, not built here)

- [ ] L.1 Meta-progression handoff: `MetaProgressionGrain` ↔ within-run (which meta-unlocks seed a new character's start-kit; which milestones post back)
- [ ] L.2 More `XpAwardEvent` triggers (exploration, quest completion, crafting) + the events that fire them
- [ ] L.3 Skill-point economy (earn points per level; spend to unlock) as declarative config
- [ ] L.4 ECA generalization: award rules and skill effects as ECA condition→action tiles (`design-eca-visual-scripting.md`)
- [ ] L.5 Cross-session/reactivation persistence of earned progression (needs the meta-handoff or a per-player progression grain)
- [ ] L.6 YAML/content-pack pipeline populates `ProgressionConfig` (no downstream change — the data tier is the seam)
- [ ] L.7 `RoleAffinity` consumers (biasing available skills/abilities) — stamped now, unread this slice
