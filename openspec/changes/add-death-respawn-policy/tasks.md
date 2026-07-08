## 1. Death policy schema + corpse expiry (Phase 1 — this change)

- [x] 1.1 `DeathPolicy` data class + `DropOnDeathPolicy`/`RespawnPointPolicy`/`XpLossPolicy` enums + `Default` preset matching shipped pre-policy behavior
- [x] 1.2 `DeathPolicy.ResolveDyingTicks()` pure function
- [x] 1.3 `CorpseAge` component (new, separate from `Corpse`) + `CorpseExpirySystem`
- [x] 1.4 Unit tests (7 tests): `Default` preset values, `ResolveDyingTicks` with down-state on/off, corpse aging/expiry at threshold, corpse-without-`CorpseAge` persists forever (backward compatibility), non-corpse entities ignored
- [x] 1.5 `openspec/specs/death-respawn-policy/spec.md` delta: ADDED requirements, each with a `**Verified by:**` line

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Persist a `DeathPolicy` per world (likely a new `MapState` field, following the existing `IPersistentState<MapState>` pattern)
- [ ] 2.2 Have the live attack path (`deepen-combat-model` Phase 2) call `policy.ResolveDyingTicks()` instead of `DamagePipeline.Resolve`'s hardcoded `dyingTicks: 3`
- [ ] 2.3 Attach `CorpseAge` to a defeated entity at the moment it becomes a `Corpse`, once a world's policy specifies a finite `CorpseRetentionTicks`
- [ ] 2.4 Design and implement `DropOnDeath`/`RespawnPoint` behavior (loot-drop timing question also tracked in `deepen-combat-model` task 2.2)
- [ ] 2.5 Design and implement `XpLossPolicy` behavior once `add-character-progression` ships a pool to lose XP from
- [ ] 2.6 Scope and implement `WorldPersistencePolicy` (generator-version migration) as its own, separate change against a PCG capability
