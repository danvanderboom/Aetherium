# ECA Scripting

Aetherium games are configured as data. Death rules, abilities, progression, factions, and content
(creatures/items/spawns) all load from a YAML bundle. **ECA scripting** is how a bundle expresses
*reactive logic* — "when something happens, if some condition holds, do something" — without engine
code.

This document describes what ships today (the **T0 runtime**). The full language vision — a visual
tile editor, a plugin SDK, selectors, state machines, and a wide event catalog — is specified
separately in
[design-eca-visual-scripting.md](audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md).
What's here is the foundation that vision builds on: rules as data, evaluated deterministically on the
server, wired to a real event.

## Where it fits

| Layer | What it declares | Example |
|---|---|---|
| Content (`content.yaml`) | the *nouns* — what exists | a `wolf` creature, a `wolf_pelt` item |
| ECA rules (`rules.yaml`) | the *verbs* — what reacts | "when a `cult_acolyte` dies, spawn a `wolf`" |

Rules reference content by id, so content is the vocabulary the verbs act on. A rule that spawns a
creature names a creature the bundle defines; the loader rejects a rule that references something
absent.

## Rule anatomy

A rule has three parts — a **trigger** (`when`), optional **conditions** (`if`, AND-ed), and an
ordered list of **actions** (`do`):

```yaml
# rules.yaml — a bundle's reactive logic
rules:
  - id: acolyte-summons-wolf
    when: creature_died                 # the event that wakes this rule
    if:                                 # all must hold (empty ⇒ always fire on the trigger)
      - kind: creature_type_is
        creatureType: cult_acolyte
      - kind: chance
        probability: 0.5
    do:                                 # ordered actions
      - kind: spawn_creature
        creatureId: wolf                # spawns at the death location

  - id: wolf-dying-snarl
    when: creature_died
    if:
      - kind: creature_type_is
        creatureType: wolf
    do:
      - kind: apply_status
        target: Killer                  # the actor who landed the killing blow
        statusId: slowed
        durationTicks: 10
        magnitude: 0.5
```

- `when` names a single **trigger** tile. This slice ships one: `creature_died`, raised when a monster
  is defeated by melee or an ability.
- `if` is a list of **condition** tiles, all of which must pass. An empty `if` always passes.
- `do` is an ordered list of **action** tiles. Actions that target an entity take a `target` of
  `Killer` or `Victim`.

Rules are **data**: the same YAML keys are the camelCase of the underlying types, there is no separate
schema to drift, and every trigger/condition/action id is validated at load against the vocabulary
below — a typo is a load-time error, never a silent no-op.

## Evaluation model (T0)

- Rules fire on the server when their trigger's event is raised, **after** the engine's built-in
  reactions to that event (for `creature_died`: award XP, apply faction standing, drop loot). Rules
  augment the defaults; they don't race them.
- Conditions are evaluated in order; `chance` draws from a per-world RNG seeded from the world seed, so
  a given `(seed, event order)` reproduces the same firings.
- Actions are resolved to concrete requests (target entity, coordinates) and executed by the map — a
  spawn materializes a content creature, `deal_damage` routes through the combat pipeline, `apply_status`
  attaches a shipped status effect.
- **No cascades this slice:** a creature spawned by a rule, or a kill caused by a rule's `deal_damage`,
  does not itself re-enter ECA within the same event. Control flow stays single-level and obvious.

## Maturity ladder

| Tier | State | What it adds |
|---|---|---|
| **T0** | **shipped** | rules as data; one trigger (`creature_died`); closed condition/action vocabulary; deterministic evaluation; `rules.yaml` bundle section; a reflectable vocabulary registry |
| T1 | design | more triggers (spatial, temporal, input, economy, quest, player death); target selectors (`nearby`, sets) |
| T2 | design | flow-control actions (`branch`, `for_each`, `wait`, `do_once`); bounded same-tick cascades (`late` phase) |
| T3 | design | state-machine pages; sub-rule nesting; `it`/`them` bindings |
| T4 | design | the `[Eca*]` Roslyn/WASM plugin SDK — author-registered tiles, capability-gated |
| T5 | design | the visual tile editor + text projection over `aetherctl`; a `rules trace` debugger |

## Type metadata

Every trigger, condition, and action is a C# **tile type** exposing an `EcaTileDefinition` — id, role,
description, and a typed parameter schema. `EcaVocabulary` reflects these into one registry that is the
single source of truth: the validator checks rules against it, the reference below is generated from it,
the runtime keys its evaluation on the same tile-id constants, and a future editor palette reads the
same surface. Adding a tile type extends the language everywhere at once. This mirrors the shipped
`AgentToolRegistry` pattern the design vision names as the extensibility model.

## Vocabulary reference

<!-- eca:vocab:start -->

_This section is generated from `EcaVocabulary`; edit the tile definitions, not this table._

### Triggers

#### `creature_died`

Fires when a creature is defeated (by melee or ability). Binds the victim's creature type, the killer, and the death location.

_No parameters._

### Conditions

#### `chance`

Passes with the given probability (0..1), drawn from the world's seeded rule RNG.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `probability` | Number | yes | Probability in [0, 1] that this condition passes. |

#### `creature_type_is`

True when the event's creature is of the named type.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `creatureType` | CreatureRef | yes | The content creature id to match against the event's victim. |

### Actions

#### `apply_status`

Applies a shipped status effect to the target for a duration.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `statusId` | StatusRef (burning / slowed / prone) | yes | The status to apply. |
| `durationTicks` | Integer | no | How many ticks the status lasts. |
| `magnitude` | Number | no | Per-status magnitude: damage-per-tick for burning, speed multiplier for slowed; ignored for prone. |

_Targets:_ `Killer`, `Victim`

#### `deal_damage`

Deals damage to the target through the map's damage pipeline (e.g. a death-surge that hurts the killer).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `amount` | Number | yes | Damage amount (must be > 0). |
| `damageType` | Text | no | Damage type tag (campaign-defined; defaults to "physical"). |

_Targets:_ `Killer`, `Victim`

#### `spawn_creature`

Spawns a creature from the world's content catalog at the death location (plus offset).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `creatureId` | CreatureRef | yes | The content creature id to spawn. |
| `offsetX` | Integer | no | Tiles east of the death location (default 0). |
| `offsetY` | Integer | no | Tiles south of the death location (default 0). |

<!-- eca:vocab:end -->
