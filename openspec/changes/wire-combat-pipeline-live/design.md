## Context

Third slice of the Phase 2 live-wiring pass. Flagged from the initial gap-analysis research as the
riskiest of the four primitives because it carries real, previously-unresolved product decisions
(Dying/Corpse client visibility, loot-drop timing, RNG determinism) rather than being a pure
mechanical swap. This design resolves each decision conservatively — see below — so the slice stays
a behavior-preserving wiring change, matching the pattern of the prior two slices, with every new
behavior additive (a killed monster now persists instead of vanishing) rather than a change to
existing observable outcomes.

## Decisions

### Player→monster only; monster→player retaliation stays on `CombatSystem`

The two live attack call sites are `AttackAsync` (player attacks monster) and `StepNpcsAsync`'s
retaliation branch (monster attacks player). Both could theoretically route through
`DamagePipeline`, but only the target types differ in a way that matters: a monster entering
Dying→Corpse is exactly the "creature death, ready for loot/harvest" lifecycle the pipeline was
designed for. A *player* entering that same lifecycle is a different problem — there is no revive
mechanic, no respawn flow, and no policy decision yet for what "a player is a Corpse" even means
(does their session stay attached? can they still receive commands? `add-death-respawn-policy`
shipped the *schema* for exactly these questions but explicitly deferred all live consumption). Router
scope. Routing player deaths through `DamagePipeline` today would produce a permanently-unattackable,
never-revived "Dying" player with no way out — worse than the current down-at-0-HP-forever behavior,
not better. So this slice draws the line at the target type: monsters get the new lifecycle, players
don't, until a dedicated death/respawn slice exists.

This means `MonsterBehaviors.BuildWanderAndMeleeTree`'s signature is **unchanged** — it still takes a
`CombatSystem` and still calls `TryAttack(removeOnDeath: false)` for its attack action, exactly as
`wire-npc-behavior-trees-live` left it. Task 2.3 in that change's tasks.md ("switch monster attacks to
DamagePipeline") is resolved here only for the player-attacks-monster direction; the note is updated
to be explicit about the split rather than claiming full resolution.

### `AlwaysHitResolver`, not `RollHitResolver`

Every existing combat test (`GameMapGrainCombatTests`, `CombatSystemTests`, `EndToEndSharedMutationTests`)
assumes deterministic, always-hits combat with exact damage numbers (`AttackPower` ± weapon bonus, no
variance). Switching to `RollHitResolver` would need a seeded RNG source threaded through the grain
(itself a determinism-policy decision — per-world seed? per-attack? reproducible from a replay log?)
that has no answer yet. `AlwaysHitResolver` reproduces the exact pre-pipeline behavior — hit
determinism is unchanged by this slice, full stop. Introducing accuracy/evasion/crit is real future
work, not something to smuggle in as a side effect of a wiring change.

### Loot and analytics trigger on entering Dying, not on Corpse conversion

The pipeline's own design doc flagged this as open: does loot drop the instant a monster is lethally
hit, or only once it fully becomes a Corpse (ticks later)? This slice picks **on Dying** — the same
tick as the lethal hit, identical timing to the pre-pipeline instant-delete behavior. This is the
choice that changes nothing observable: `Attack_KillingMonster_DropsLoot_AndRecordsStats` (already
shipped, asserts loot appears in the very next `GetWorldSnapshotAsync` call, no intervening tick)
needed zero changes to keep passing. Moving loot to the Corpse transition would be a legitimate
design (matches "you loot the body," not "you loot the disappearing husk") but is a deliberate,
visible gameplay change this conservative slice does not make.

### No new delta type for the Dying/Corpse transition

`GameMapGrain.AttackAsync` now emits the same `ComponentFieldChangedDelta` (Health.Level → 0) it
already emitted for a non-lethal hit, instead of the old `EntityRemovedDelta`. This is accurate — the
entity's health genuinely reached 0, and it genuinely did not leave the world — so no new delta
vocabulary is needed to describe it truthfully. A client that previously treated "health hit 0" and
"entity removed" as the same signal (many prior UIs might, since they always arrived together) will
now see only the health delta; whether that requires client-side changes is explicitly not this
slice's concern (server/engine-only pass), but worth flagging: **any client that inferred "monster
died" purely from `EntityRemovedDelta` will not fire that inference anymore.** A future client-facing
change should add an explicit "entered Dying"/"became Corpse" signal if that inference matters,
rather than clients continuing to guess from health reaching 0.

### `DeathSystem`/`CorpseExpirySystem` ticked unconditionally, every `TickAsync`

Both are `O(entities-with-Dying)`/`O(entities-with-Corpse-and-CorpseAge)` respectively — cheap,
self-contained, and (per `DeathPolicy.Default`) `CorpseExpirySystem` is a no-op today since
`CorpseRetentionTicks = int.MaxValue`. No `SimulationOptions` gate was added (unlike
`EnableNpcBehavior`/`NpcMoveIntervalTicks` for `StepNpcsAsync`) — there is no scenario yet where a
world would want combat's death lifecycle running but NPC behavior or vice versa, and adding an
options flag with no consumer would be speculative.

## Risks / Coverage gaps

- **`_monsterTrees` (from `wire-npc-behavior-trees-live`) is not pruned for Dying/Corpse monsters.**
  They're still present in `_world.Entities` (by design — that's the whole point), so the existing
  "prune when absent from `_world.Entities`" logic never fires for them. Their cached `BehaviorTree`
  instance sits unused, forever (bounded by total-monsters-ever-killed-on-this-map, not unbounded
  growth from live monster churn — the existing "prune on removal" still catches any future removal
  path, e.g. once `CorpseExpirySystem` starts actually expiring corpses under a non-default policy).
  Acceptable for now; worth a follow-up if it ever shows up in profiling.
- **A killed monster is still `OfType<Monster>()`** and gets re-collected into `StepNpcsAsync`'s
  `monsters` list every tick (just skipped immediately after, via the new Dying/Corpse check) — a
  small, bounded per-tick cost that scales with total-corpses-on-the-map. Not a correctness issue,
  flagged for the same future profiling pass as above.
- **No grain-level test exercises the eventual Dying→Corpse conversion** (would need 3+ `TickAsync`
  calls after a kill, per `ReviveWindowTicks = 3`) — the unit-level `DeathSystemTests` already cover
  the conversion mechanism in isolation; this slice's new tests cover that the *live* Dying state is
  reached and that it correctly makes the monster inert, which is the actual live-wiring risk. A
  live-level Corpse-conversion test would be a reasonable follow-up but low-value given the unit
  coverage already exists.
