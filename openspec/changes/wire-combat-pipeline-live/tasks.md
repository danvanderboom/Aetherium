## 1. Live wiring (this change)

- [x] 1.1 `GameMapGrain.AttackAsync` resolves through `DamagePipeline.Resolve` (player→monster only); reach/existence/self-attack checks moved into the grain
- [x] 1.2 `AlwaysHitResolver` + `DeathPolicy.Default` used — no RNG introduced, death-lifecycle defaults reproduce pre-policy behavior
- [x] 1.3 A lethal hit no longer removes the target; loot/analytics trigger on entering `Dying`, same tick as before
- [x] 1.4 `GameMapGrain.TickAsync` ticks `DeathSystem` and `CorpseExpirySystem` every tick (silent, no delta emission)
- [x] 1.5 `GameMapGrain.StepNpcsAsync` skips monsters carrying `Dying` or `Corpse`
- [x] 1.6 Integration tests: a killed monster persists in the world snapshot at 0 HP; a Dying monster no longer moves or retaliates
- [x] 1.7 Cross-link the added requirement(s) with `**Verified by:**` lines
- [ ] 1.8 Full regression suite green (all existing combat/multiworld tests must be unaffected — same damage numbers, same loot/analytics timing, same `TargetDefeated`-on-third-hit semantics)

## 2. Still open (tracked here, not resolved by this change)

- [ ] 2.1 Monster-attacks-player retaliation through `DamagePipeline` — blocked on a player death/respawn design pass (see `add-death-respawn-policy`, still Phase-1-only)
- [ ] 2.2 `RollHitResolver` (probabilistic hit/crit) live — needs an RNG-seeding/determinism decision first
- [ ] 2.3 `Resistances`/`StatusEffect`/`ThreatTable` component attachment on real creatures — content work, not engine wiring
- [ ] 2.4 A client-facing signal for "entity entered Dying" / "became Corpse", if any client needs to distinguish that from an ordinary health-changed delta (currently nothing does)
- [ ] 2.5 Prune `_monsterTrees` entries for Dying/Corpse monsters (currently only pruned on removal from `_world.Entities`, which corpses never leave under the default retention policy)
- [ ] 2.6 A live-level test of the full Dying→Corpse conversion (3+ ticks after a kill) — unit coverage exists (`DeathSystemTests`); a live-path version would be a reasonable but low-priority follow-up
