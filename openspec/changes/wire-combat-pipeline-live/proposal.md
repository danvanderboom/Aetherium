## Why

`deepen-combat-model` (Wave 0, §4.2) shipped `DamagePipeline` — typed damage packets, pluggable hit
resolution, per-tag resistances, threat, and a Dying→Corpse death-state transition that replaces
instant deletion — fully unit-tested but never called from a live path. This is the third slice of
the Phase 2 live-wiring pass (after `wire-npc-behavior-trees-live` and `wire-npc-action-budget-live`),
and the one flagged from the start as carrying real product decisions (Dying/Corpse client
visibility, loot-drop timing, whether to introduce RNG). This proposal makes the conservative choice
on each: reuse the exact same damage numbers and hit determinism as before, keep loot/analytics at
their existing trigger moment, and leave anything that would need new design (player death/respawn,
new delta types, non-deterministic combat) explicitly out of scope.

## What Changes

- `GameMapGrain.AttackAsync` (player attacks a monster) now resolves through `DamagePipeline.Resolve`
  instead of `CombatSystem.TryAttack`. Reach/existence/self-attack checks move into the grain (the
  pipeline is deliberately reach-agnostic); damage amount is still
  `CombatSystem.ComputeAttackDamage` (unchanged math) wrapped in a single-component `"physical"`
  `DamagePacket`; hit resolution is `AlwaysHitResolver` (reproduces the always-hits MVP exactly, no
  RNG introduced this slice).
- A lethal hit no longer removes the target from `_world` — it enters `Dying` (via
  `DeathPolicy.Default.ResolveDyingTicks()`, i.e. a 3-tick down state, matching the policy schema's
  own "reproduces pre-policy behavior" default) and is never deleted by the grain. Clients see this
  as an ordinary health-changed delta (health reaching 0), the same as any non-lethal hit — no
  `EntityRemovedDelta` is emitted for a killed monster anymore.
- `GameMapGrain.TickAsync` now ticks `DeathSystem` (Dying → Corpse countdown) and
  `CorpseExpirySystem` (corpse aging/removal per `DeathPolicy`) every tick — silent, canonical-
  state-only bookkeeping with no delta emission, since nothing renders the Dying/Corpse distinction
  yet.
- `GameMapGrain.StepNpcsAsync` now skips any monster carrying `Dying` or `Corpse` — a killed monster
  no longer wanders or retaliates, since it persists in `_world.Entities` instead of being deleted.
- Loot drop and combat analytics (`MonstersDefeated`/`TotalDamageDealt`) still trigger at the same
  moment as before (the lethal hit / entering `Dying`), not deferred to the later Corpse transition —
  preserves existing "kill → loot appears immediately" timing and test coverage exactly.

## Explicitly Out of Scope (deferred, tracked in tasks.md)

- **Monster-attacks-player retaliation** (`StepNpcsAsync`'s attack branch) stays on the old
  `CombatSystem.TryAttack(removeOnDeath: false)` — a downed player entering the Dying/Corpse
  creature-death lifecycle needs its own design pass tied to `add-death-respawn-policy` (still
  Phase-1-only: schema exists, no live wiring, no respawn flow). Applying `DamagePipeline` there
  today would leave a "dying" player permanently unattackable-but-also-never-revived, with no
  mechanic to clear it — an unfinished, confusing state this change does not introduce.
- **`RollHitResolver`** (probabilistic hit/crit) — stays unused; `AlwaysHitResolver` keeps combat
  deterministic, matching every currently-shipped combat test's expectations. Introducing RNG needs
  its own seeding/determinism decision (the pipeline's design doc flags this explicitly).
- **New delta vocabulary** for Dying/Corpse state (e.g. an explicit "entity is now a corpse" delta) —
  not needed yet since nothing renders the distinction; the existing health-changed delta is
  sufficient and accurate (health really did reach 0).
- **`Resistances`/`StatusEffect`/`ThreatTable` component attachment** — no entity carries these yet,
  so `DamagePipeline` runs with its documented no-component defaults (unmitigated damage, no status,
  an empty-then-lazily-created threat table). Wiring these onto real creatures is future content
  work, not an engine-wiring concern.

## Impact

- Affected code: `Aetherium.Server/MultiWorld/GameMapGrain.cs` (`AttackAsync` rewritten,
  `TickAsync` gains `DeathSystem`/`CorpseExpirySystem` ticks, `StepNpcsAsync` gains a Dying/Corpse
  skip-check).
- Affected specs: `combat` (ADDED: "Player Attacks Route Through DamagePipeline", "Live NPC Tick
  Skips Dying/Corpse Monsters").
- `AttackResultDto`'s shape is unchanged — `TargetDefeated` now means "entered Dying" rather than
  "removed from world," which is a meaning change but not a shape change; no client code needs to
  change to keep working (a defeated target still can't be attacked again, which is what client code
  actually observes).
- `LocalMutationGateway` (legacy in-process path) is untouched — it keeps using `CombatSystem`
  directly, consistent with every prior slice's scoping.
