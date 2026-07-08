## 1. Damage/mitigation/hit-resolution primitives (Phase 1 — this change)

- [x] 1.1 `DamageComponent`/`DamagePacket` (typed, tag-keyed damage; melee/ranged/aoe delivery)
- [x] 1.2 `Resistances` component (per-tag flat/percent/minimum mitigation) + `DamageResolution` helper
- [x] 1.3 `IHitResolver` + `AlwaysHitResolver` + `RollHitResolver` (accuracy/evasion/crit, seedable `Random`)
- [x] 1.4 `StatusEffect` base + `BurningEffect`/`SlowedEffect`/`ProneEffect` + `StatusEffects` component + `StatusEffectSystem`
- [x] 1.5 `Dying`/`Corpse` components + `DeathSystem`
- [x] 1.6 `ThreatTable` component
- [x] 1.7 `DamagePipeline` composing 1.1–1.6
- [x] 1.8 Unit + integration tests (37 tests across mitigation, hit resolution, status effects, death transition, threat, and the composed pipeline)
- [x] 1.9 `openspec/specs/combat/spec.md` delta: ADDED requirements
- [x] 1.10 Cross-link every requirement with a `**Verified by:**` line naming the test(s) that cover it

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Reroute `GameMapGrain.AttackAsync` and monster retaliation (`StepNpcsAsync`) through `DamagePipeline`
- [ ] 2.2 Decide and implement loot-drop timing relative to `Dying` vs `Corpse`
- [ ] 2.3 Give monsters/players real `Resistances`/`Accuracy`/`Evasion`/`CritChance` component defaults
- [ ] 2.4 Wire `ThreatTable.GetTopThreat()` into monster targeting once NPC AI (§4.5) exists
- [ ] 2.5 Decide whether `SlowedEffect`/`ProneEffect` throttle `ActionSystem` cost/availability, and wire it if so
- [ ] 2.6 Emit semantic damage/status/death perception events once the content atlas (Phase 2) defines the tags to carry them
