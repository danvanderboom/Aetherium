# Design: ECA Scripting (T0 runtime)

## Context

Code survey (2026-07-10) of the event surface this slice hooks:

- **The kill chokepoint is already ECA-shaped.** Both `GameMapGrain.AttackAsync` (melee) and
  `UseAbilityAsync` (ability) converge on the same branch when a monster enters `Dying`:
  `AwardKillXp(killer, type)` → `ApplyFactionAction(killer, KillActionTagFor(victim))` →
  `SpawnMonsterLoot(victim, …)`. `ApplyFactionAction`'s own doc comment already anticipates this
  change: *"the ECA generalization replaces this method's inside, not its call sites."* This is the
  single, proven, dual-entry point to raise a `creature_died` event.
- **`KillActionTagFor`** already derives the stable creature identity (`CreatureTypeTag` value, e.g.
  "wolf") that a rule's `creature_type_is` condition matches — the same id the content slice stamps and
  the faction doctrine judges.
- **The descriptor pattern is settled.** `AbilityEffectDescriptor` is one `[GenerateSerializer]` class
  with a `Kind` enum + the union of per-kind fields (unused fields ignored), compiled by
  `AbilityCompiler.CompileEffect` via a `Kind switch`. ECA conditions and actions adopt the identical
  shape — no polymorphic YAML, no custom deserializer.
- **Every action this slice needs already has a live system.** `spawn_creature` reuses
  `ContentCompiler.ApplyCreature` + `world.AddEntity` + `EntityPlacedDelta` (the content slice's spawn
  path); `deal_damage` reuses the map's `DamagePipeline`/`IHitResolver`; `apply_status` reuses
  `StatusEffectSystem` and the shipped `BurningEffect`/`SlowedEffect`/`ProneEffect`.

## Goals / Non-Goals

**Goals**
- A bundle can add reactive rules to one real event as pure data — zero engine code to add a reaction.
- Closed, validated vocabulary: every trigger/condition/action id is known at compile time; a typo is a
  load-time error, never a silent no-op.
- Pure, unit-testable evaluation; the grain owns all world mutation and delta fan-out.
- Exact legacy behavior when no `EcaConfig` is present.

**Non-Goals** (see proposal — the T5 vision's ceiling): visual editor, plugin SDK, selectors/FSM/
nesting, triggers beyond `creature_died`, action-budget integration, full determinism model.

## Decisions

### D1. Rule shape mirrors the settled descriptor discipline

```yaml
rules:
  - id: acolyte-summons-wolf
    when: creature_died           # the single trigger id this slice knows
    if:                           # optional; AND-ed; empty ⇒ always fire on the trigger
      - kind: creature_type_is
        creatureType: cult_acolyte
      - kind: chance
        probability: 0.5
    do:                           # ordered
      - kind: spawn_creature
        creatureId: wolf
        offsetX: 0
        offsetY: 0
```

`EcaRule { Id, When (EcaTriggerKind), If (List<EcaConditionDescriptor>), Do (List<EcaActionDescriptor>) }`.
`EcaConditionDescriptor { Kind, CreatureType?, Probability }` and
`EcaActionDescriptor { Kind, Target, CreatureId?, OffsetX, OffsetY, DamageType?, Amount, StatusId?, DurationTicks, Magnitude }`
— one class per role, `Kind` + union-of-fields, exactly like `AbilityEffectDescriptor`.

### D2. Closed vocabularies (the whole language, this slice)

| Role | Ids | Routes through |
|---|---|---|
| Trigger | `creature_died` | the shared monster-defeat branch |
| Condition | `creature_type_is`, `chance` | pure predicates over the event + seeded RNG |
| Action | `spawn_creature`, `deal_damage`, `apply_status` | content catalog / `DamagePipeline` / `StatusEffectSystem` |

`EcaActionTarget { Killer, Victim }` — `deal_damage`/`apply_status` target one of them; `spawn_creature`
ignores it and spawns at the victim's death location + offset. Each id is an enum member, so the
compiler `switch` is exhaustive and validation is a set-membership check.

### D3. Pure evaluator, grain executor (the behavior-tree seam, reused)

`EcaRuntime.Evaluate(EcaEventContext) → List<EcaActionRequest>`:
- `EcaEventContext` (input, pure data): `TriggerKind`, `VictimCreatureType`, `KillerEntityId`,
  `VictimX/Y/Z`.
- For each rule whose `When` matches and whose `If` predicates all hold (chance rolled from an injected
  seeded RNG), emit one resolved `EcaActionRequest` per `do:` action — target entity id and coordinates
  already resolved, so the executor needs no rule knowledge.
- `EcaActionRequest` is a resolved union: `SpawnCreature{creatureId, x, y, z}` /
  `DealDamage{targetEntityId, damageType, amount}` / `ApplyStatus{targetEntityId, statusId, duration, magnitude}`.

`GameMapGrain` executes the requests after the existing kill reactions, mutating `_world` and fanning
out the same deltas those paths already emit (`EntityPlacedDelta` for a spawn, `IntFieldDelta` for a
damage-driven health change). Evaluator = unit-testable with no grain; executor = integration-tested.

*Why not execute inside the evaluator?* Actions need grain-owned services (spawn + delta fan-out, the
damage pipeline, state writes). Keeping the evaluator pure matches how `MonsterBehaviors` writes
outcomes to a blackboard and `StepNpcsAsync` fans out — the established, tested split.

### D4. Determinism: seeded per-map RNG, best-effort this slice

`EcaRuntime` draws `chance` from a `Random` seeded from the world's generation seed, created at map
bind time and advanced per evaluation. This is reproducible for a fixed event order; the full
`(seed, input-log)` replay guarantee from the vision is out of scope. `spawn_creature` reuses the
content catalog's own deterministic materialization.

### D5. Threading follows the established recipe verbatim

`GameDefinition.Rules` (+ `rules.yaml` sibling) → `CreateWorldRequest.EcaConfig` →
`WorldTemplate.EcaConfig` → `WorldConfig.EcaConfig` → `WorldGrain` state →
`IGameMapGrain.InitializeAsync(..., ecaConfig)` → `MapState.EcaConfig` + `ApplyEcaConfig` (compile at
bind, like abilities/factions/progression/content), recompiled on reactivation.

### D6. `spawn_creature` reuses the content catalog — and needs it

A `spawn_creature` action names a creature id that must exist in the world's `ContentConfig`. At execute
time the grain resolves it through `_contentCatalog` (the content slice's runtime form) and materializes
it via `ContentCompiler.ApplyCreature`, identical to a spawn-table draw. A rule that spawns a creature in
a world with no content catalog is a load-time validation error (see D7), so this can't fail at runtime.
This is the concrete payoff of sequencing content before ECA: the verbs act on real nouns.

### D7. A reflectable vocabulary registry is the single source of truth

Every trigger/condition/action is a small C# **tile type** exposing a static `EcaTileDefinition`
(pure metadata in `Aetherium.Model.Eca`: `Id`, `Role` ∈ {Trigger, Condition, Action}, `Description`,
`Parameters` — each an `EcaParameter { Name, ValueType, Required, Description }` — and `ValidTargets`).
`EcaVocabulary` (in `Aetherium.Server.Eca`) collects them into an `Id → EcaTileDefinition` registry,
mirroring the shipped `AgentToolRegistry`/`AgentToolAttribute` pattern the ECA vision names as the
extensibility model.

The registry is consumed three ways, so the vocabulary is defined exactly once:
- **Validation** (D8) reads it — closed-set membership and required-parameter checks are generic over
  the registry, not a hand-maintained per-kind switch.
- **Docs** (D9) are generated from it.
- **The runtime** keys its compile/execute switch on the same tile-id constants the definitions expose,
  so a new tile can't be half-added (metadata without behavior, or vice versa) without a test noticing.

`EcaValueType` (`Boolean`/`Integer`/`Number`/`Text`/`CreatureRef`/`StatusRef`/`EnumChoice`) types each
parameter — `CreatureRef`/`StatusRef` are what make cross-reference validation and (later) editor
autocomplete data-driven rather than special-cased.

The serialized rule descriptors stay **flat and typed** (D1) — `Kind` is a string id resolved against
the registry, the union-of-fields shape keeps YAML binding schema-free. The registry describes which
fields each kind uses; it does not replace the typed descriptor.

### D8. Validation (extends `GameDefinitionValidator`, driven by the registry)

Errors: duplicate rule ids; a trigger/condition/action `Kind` not in `EcaVocabulary` (closed set); a
required parameter left at its default; a `CreatureRef` parameter (e.g. `spawn_creature.creatureId`)
naming a creature absent from `content` (or `content` absent entirely); a `StatusRef` parameter
naming a status outside `{burning, slowed, prone}`; `chance.probability` outside `[0, 1]`;
`deal_damage.amount` ≤ 0. Warning: a `creature_type_is` `CreatureRef` naming a creature absent from
`content` (typo detector, mirroring the faction `kill:` warning — the rule would never fire).

The `CreatureRef`/`StatusRef`/range checks are expressed once against the parameter metadata, so adding
a new tile that references creatures needs no new validator code — only its `EcaTileDefinition`.

### D9. Docs generated from the vocabulary, drift-guarded by a test

`EcaVocabularyDoc.GenerateMarkdown()` renders `EcaVocabulary` as a reference table (every tile, its
role, description, and parameter schema). `docs/eca-scripting.md` is a hand-written language guide
(where it fits vs the T5 vision, rule anatomy, the Emberfall worked example, the maturity ladder) whose
vocabulary-reference section — between `<!-- eca:vocab:start -->` / `<!-- eca:vocab:end -->` markers —
is that generated output. `EcaVocabularyDocTests` regenerates the section and asserts it matches the
committed file, so the docs cannot silently drift from the code.

### D10. Execution ordering and safety

- ECA actions run **after** the engine's three built-in kill reactions (XP, faction, loot), so a rule
  augments the defaults rather than racing them.
- A spawned creature does not itself re-trigger `creature_died` unless later killed — no synchronous
  self-cascade. `deal_damage` from a rule could kill the killer; that death routes through the normal
  death path but does **not** re-enter ECA this slice (single-level, no same-tick cascade), keeping the
  first slice's control flow bounded and obvious. (Cascades/`late` phase are a vision-level Later item.)

## Risks / Trade-offs

- **A rule that kills the player via `deal_damage`** interacts with `DeathPolicy` — that's correct and
  desirable (a death-surge that downs you), but the sample rules keep amounts modest so the demo isn't
  punishing.
- **No cascade this slice** means a rule can't chain off a rule-spawned creature's death in the same
  event; acceptable and explicitly bounded (D8).
- **Single trigger** limits expressiveness, by design — it proves the spine end-to-end before we widen
  the event catalog.

## Migration

None. `EcaConfig` optional everywhere; absent ⇒ bit-identical legacy kill behavior.

## Open Questions

- Should `deal_damage`/`apply_status` gain a `nearby(radius)` target before more triggers land? Deferred
  — needs the selector engine, a vision-level item.
- Do we raise `creature_died` for player deaths too (enabling revenge/bounty rules)? Deferred — this
  slice scopes the event to monster defeats, matching where the kill reactions already live.
