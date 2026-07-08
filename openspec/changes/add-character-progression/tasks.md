## 1. Progression primitives (Phase 1 — this change)

- [x] 1.1 `Attributes` component (string-keyed vector, `Vitality`/`Speed` named constants)
- [x] 1.2 `ProgressPool`/`ILevelCurve`/`LinearLevelCurve` + `ProgressPools` component (`AddXp`, multiple independent pools)
- [x] 1.3 `SkillDefinition`/`SkillCatalog`/`UnlockedSkills`/`SkillUnlockService` (prerequisite-gated unlocking)
- [x] 1.4 `RoleAffinity` component
- [x] 1.5 Unit tests (17 tests): attribute get/set/default, engine-default constants, XP accumulation and level recomputation via an injected curve, multiple independent pools, a custom (non-default) curve is honored, skill unlock root/unknown/already-unlocked/missing-prerequisite/met-prerequisite/multi-prerequisite cases
- [x] 1.6 `openspec/specs/character-progression/spec.md` delta: ADDED requirements, each with a `**Verified by:**` line

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Attach default `Attributes`/`ProgressPools` to `Character` construction
- [ ] 2.2 Decide and implement whether/how `Attributes.Vitality`/`Attributes.Speed` drive `Health.MaxLevel`/`ActionSpeed.Speed`
- [ ] 2.3 Wire XP awards into combat (`deepen-combat-model`), quest, and exploration events
- [ ] 2.4 Document and implement the meta-progression handoff (`MetaProgressionGrain`) — which meta-unlocks seed a new character's start-kit, which within-run milestones post back
- [ ] 2.5 Build a real UI/command path that calls `SkillUnlockService.TryUnlock`
- [ ] 2.6 Resolve `SkillDefinition.UnlocksAbilityId`/`ModifiesAttributeId` against a real `AbilityCatalog` once `add-abilities` ships
