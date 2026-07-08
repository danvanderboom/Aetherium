# Aetherium — Authoring, Scripting & `aetherctl` Design

**Status:** Draft for discussion
**Scope:** How creators define worlds, maps, characters, items, and behavior *without writing or compiling C#*; the scripting model that binds those resources together; and how `aetherctl` serves as the control plane for game creation and management.
**Companion doc:** [design-next-steps.md](design-next-steps.md) — this doc details §4.15 (Modding / Content SDK) and §4.10 (Content Atlas) of that roadmap.

---

## 1. Framing

### 1.1 The goal

A creator should be able to build a complete game on Aetherium — its maps, characters, items, quests, rules, and reactive behavior — using **declarative data files plus a lightweight scripting layer**, and drive the whole lifecycle from `aetherctl`. No Visual Studio. No `dotnet build`. No recompiling the server.

"No-code" is the wrong bar — reactive game logic is inherently programming. The realistic bar is **"low-code with a gentle ceiling"**:

- **80% declarative.** Maps, entity definitions, items, loot tables, quests, factions, dialogue — all authored as data (JSON/YAML), validated against schemas, hot-loaded into a running world.
- **15% rule scripting.** Reactive behavior — "when X happens under condition Y, do Z" — authored as **event-condition-action (ECA) rules** with a small, safe **expression language**. No general-purpose programming; no loops, no unbounded recursion, no file/network access.
- **5% escape hatch.** For genuinely novel mechanics, a **sandboxed scripting host** (Roslyn C# scripting or WASM) runs inside quota-limited grains. This is the ceiling creators grow into, not the floor they start on.

### 1.2 Vision constraints inherited from the engine

These carry over from [design-next-steps.md](design-next-steps.md) §1 and constrain every authoring/scripting decision:

- **Render-agnostic.** Authoring references *semantic tags* (from the content atlas), never glyphs or sprites. The same game data drives console, Unity, and Unreal.
- **Continuous, speed-based simulation.** Scripts never assume turns. A rule's actions enqueue onto an actor's action budget; scripts cannot "stop the world" or assume they run between turns.
- **Genre-agnostic.** Authoring vocabulary is data-driven. Nothing named `spell` or `sword` is baked in; a campaign defines its own damage types, resource pools, entity kinds, and ability effects.
- **Deterministic & server-authoritative.** Scripts run on the server, seedable, reproducible from `(seed, input-log)`. Client-side scripting is presentational only.

### 1.3 What already exists to build on

Aetherium already has the *bones* of a data-driven authoring system. This is not greenfield.

| Existing asset | What it proves | File |
|---|---|---|
| **Prefab JSON** — hand-authored maps as tile grids with embedded entities | Creators already place terrain + entities declaratively | [Data/Prefabs/Buildings/shop.json](../../../Data/Prefabs/Buildings/shop.json), [PrefabTemplate.cs](../../../Aetherium.Server/WorldGen/Prefabs/PrefabTemplate.cs) |
| **Narrative JSON** — quests, objectives, rewards, loot tables, monster density, NPC goals | Rich game content is already fully declarative | [Data/Narratives/tutorial-village.json](../../../Data/Narratives/tutorial-village.json) |
| **Agent tool registry** — `IAgentTool` with id, param schema, capabilities | A discoverable, capability-gated *verb vocabulary* already exists | [IAgentTool.cs](../../../Aetherium.Server/Agents/Tools/IAgentTool.cs), [UseTool.cs](../../../Aetherium.Server/Agents/Tools/Interaction/UseTool.cs) |
| **Event handlers** — `IEventHandler` + `ScheduledEvent.EventData` dictionary | The event backbone for reactive rules is present | [ProceduralEvents.cs](../../../Aetherium.Server/Events/ProceduralEvents.cs), [EventScheduler.cs](../../../Aetherium.Server/Events/EventScheduler.cs) |
| **`aetherctl`** — System.CommandLine CLI over Orleans + SignalR + a worldgen REST server | The control-plane surface already spans worldgen, worlds, narrative, tools, sessions | [Program.cs](../../../Aetherctl/Program.cs), [WorldCommands.cs](../../../Aetherctl/Commands/WorldCommands.cs) |

The recommendation below **generalizes and unifies** these into a coherent authoring platform rather than inventing from scratch.

---

## 2. The layered authoring model

Think of authoring as five layers, from most-declarative (bottom) to most-expressive (top). A creator can build an entire game touching only the bottom three.

```
┌─────────────────────────────────────────────────────────────┐
│ 5. Sandboxed script host (Roslyn / WASM)   — escape hatch     │  ~5% of creators
├─────────────────────────────────────────────────────────────┤
│ 4. ECA rules + expression language         — reactive logic   │  ~15%
├─────────────────────────────────────────────────────────────┤
│ 3. Content definitions (entities, items, abilities, factions) │  everyone
│ 2. Maps & prefabs (hand-authored tile grids)                  │  everyone
│ 1. Content atlas (semantic tag vocabulary)  — the shared ABI  │  everyone
└─────────────────────────────────────────────────────────────┘
        ▲                                              ▲
        │  all of this is packaged as a Game Pack      │
        └──── loaded / hot-reloaded via aetherctl ─────┘
```

Everything a creator makes lives in a **Game Pack** — a versioned, signed directory/zip bundle. `aetherctl` validates, loads, and hot-reloads packs; the server treats the pack as the authoritative definition of the game.

---

## 3. Layer 1 — Content atlas (the shared ABI)

The atlas is the vocabulary every other layer references. It is what makes the engine render-agnostic and genre-agnostic. (Detailed in [design-next-steps.md](design-next-steps.md) §4.10; summarized here for authoring context.)

A creator's pack declares tags; renderers bind them to assets:

```yaml
# pack: neon-station (sci-fi survival)
atlas:
  version: "1.0"
  terrain:
    - id: bulkhead-floor      # semantic, not a glyph
      material: metal
      passable: true
    - id: vacuum
      material: void
      passable: false
      hazard: { type: decompression, dps: 8, resource: oxygen }
  entity_kinds:
    - id: maintenance-drone
      category: creature
    - id: airlock
      category: interactive
  damage_types: [kinetic, plasma, emp, radiation]     # NOT slashing/fire
  resource_pools: [health, oxygen, battery, hack_charges]
  effect_tags: [beam, projectile, aoe_ground, self_buff, hack]
  audio_cues: [alarm_klaxon, drone_whir, hull_groan]
```

- Console client maps `bulkhead-floor → '.'`, Unity maps it to a metal-plate sprite, Unreal to a 3D tile — **the pack author never picks any of those.**
- `damage_types` and `resource_pools` being pack-defined is what lets the *same engine* run a fantasy game (`[slashing, fire, arcane]`, `[health, mana, stamina]`) and this sci-fi one.

---

## 4. Layer 2 — Maps & prefabs (hand-authored, no PCG)

The existing prefab format already does this. The recommendation is to **promote prefabs to full maps** and add authoring ergonomics.

### 4.1 Today

[shop.json](../../../Data/Prefabs/Buildings/shop.json) is a 9×7 grid where each tile carries `TerrainType`, optional `EntityType`, and an `EntityConfig` dictionary:

```json
{ "TerrainType": "Floor", "EntityType": "NPC", "EntityConfig": { "Role": "Shopkeeper" } }
```

### 4.2 Recommended additions

**a) A legend-based compact format** so creators can hand-draw maps in a text editor instead of authoring verbose per-tile JSON. This is the single biggest ergonomics win for non-programmers:

```yaml
# map: derelict-lab.map.yaml
size: { w: 12, h: 8 }
legend:
  '#': { terrain: bulkhead-wall }
  '.': { terrain: bulkhead-floor }
  '+': { terrain: bulkhead-floor, entity: airlock, config: { locked: true, keycard: red } }
  'D': { terrain: bulkhead-floor, entity: maintenance-drone, ref: "@drone-hostile" }
  'X': { terrain: vacuum }
  '$': { terrain: bulkhead-floor, spawn: loot, table: "@lab-salvage" }
grid: |
  ############
  #..........#
  #..D...$...+
  #..........#
  #....XXXX..#
  #....XXXX..#
  #..........#
  ############
```

- `ref: "@drone-hostile"` and `table: "@lab-salvage"` are **references** into Layer 3 definitions — the map places instances; the definitions live once and are reused.
- The legend + ASCII grid is *authoring* convenience only; it compiles to the same semantic tile data the engine already consumes. It is **not** an ASCII-rendering decision — a `#` in the source file becomes `bulkhead-wall`, which Unreal renders as a 3D wall.

**b) Multi-level / Z-layer maps** (the engine is already 3D-aware) — stack grids with `level: -1`, `level: 0`, etc., and connect them with `stairs`/`portal` entities.

**c) Region tagging** — named rectangles/polygons (`village-center`, `boss-arena`) that rules and spawn rules reference by name, exactly as [tutorial-village.json](../../../Data/Narratives/tutorial-village.json) already does with `AreaId`.

**d) Composition** — a map can `include` prefabs at offsets, so a town map is assembled from reusable building prefabs rather than one giant grid.

### 4.3 Mixing authored maps with PCG

Creators choose per-map: fully hand-authored, fully procedural, or **hybrid** — hand-author the key rooms (boss arena, town square) and let a PCG pass fill the connective tissue. This uses the existing pass pipeline; the authored prefabs become "pinned" content the generator routes around (the `PrefabStamper` already stamps prefabs into generated maps).

---

## 5. Layer 3 — Content definitions (entities, items, abilities, factions)

All game *nouns* are declared as data. These are the definitions that maps, rules, and quests reference by id.

### 5.1 Entity / character definitions

A character is a **template of components** (matching the existing ECS). Creators compose components rather than subclassing:

```yaml
# entities/drone-hostile.entity.yaml
id: drone-hostile
kind: maintenance-drone           # atlas entity_kind
components:
  health: { max: 30 }
  action_speed: { budget: 100, refill: 12 }   # continuous-sim: acts on its own cadence
  faction: { member_of: station-ai }
  loot: { table: "@lab-salvage" }
  ai: { behavior: "@drone-patrol" }             # ref to a behavior tree (Layer 4)
  abilities: ["@emp-zap"]
  perception: { sight_range: 6, hearing_range: 8 }
  resistances: { emp: -0.5, kinetic: 0.1 }      # weak to EMP
```

Key points:
- **Composition over inheritance** — matches the project's stated ECS convention. No C# subclass per monster; a `drone-hostile` is just a bag of components.
- **`action_speed`** wires the character into the continuous simulation. A fast drone has higher `refill`; it acts more often, independent of what players do.
- **`ai: "@drone-patrol"`** references a behavior tree, keeping the "brain" in the reusable behavior library, not duplicated per entity.
- **Prototype inheritance** — `extends: "@drone-hostile"` lets a `drone-elite` override just `health` and add an ability, DRY-ing large bestiaries.

### 5.2 Item definitions

Items are entities with item-specific components, and — crucially — they reuse the **tool/usage** vocabulary that already exists ([UseTool.cs](../../../Aetherium.Server/Agents/Tools/Interaction/UseTool.cs) already supports multi-use items with usage options):

```yaml
# items/plasma-cutter.item.yaml
id: plasma-cutter
name: "Plasma Cutter"
kind: tool
components:
  carriable: { slots: 1 }
  durability: { max: 100 }
usages:
  - id: cut-door
    label: "Cut open"
    requires: { target_has: sealed-door }
    effects:
      - run_ability: "@plasma-slice"
      - consume_resource: { pool: battery, amount: 10 }
  - id: attack
    label: "Attack"
    effects:
      - run_ability: "@plasma-slice"
```

The `usages` array maps directly onto the existing multi-use tool system — the engine already returns usage options for disambiguation; the pack just declares them as data.

### 5.3 Ability definitions (genre-neutral)

Abilities are data assets built from composable **effects** (per [design-next-steps.md](design-next-steps.md) §4.3):

```yaml
# abilities/emp-zap.ability.yaml
id: emp-zap
cost: { pool: battery, amount: 15 }
charge_time: 0.4        # consumes action budget over 0.4s of sim time
cooldown: 2.0
range: 4
target: { shape: single }
visual_tag: beam        # renderer binds this; server stays glyph-free
effects:
  - deal_damage: { type: emp, amount: 18 }
  - apply_status: { effect: stunned, duration: 1.5, chance: 0.6 }
```

Nothing here is fantasy- or sci-fi-specific except the *data*. A fireball is the same structure with `type: fire` and `visual_tag: projectile`.

### 5.4 Factions, loot tables, quests, dialogue

These already have precedent in [tutorial-village.json](../../../Data/Narratives/tutorial-village.json) (quests, objectives, rewards, loot tables, monster density, NPC goals). The recommendation is to **split them into per-file definitions** referenced by id (better for diffs, reuse, and validation) and add:

- **Factions & reputation** — `faction` definitions with doctrine, ranks, standing thresholds, and inter-faction disposition (fills the gap identified in [design-next-steps.md](design-next-steps.md) §4.6).
- **Dialogue** — node graphs with condition-gated branches and effect hooks (`give_item`, `start_quest`, `adjust_reputation`).
- **Quest objective types** as an *extensible registry* — today's `TalkToNPC`, `CollectItems`, `DefeatEnemies`, `VisitLocation` are hardcoded strings; make them plug-in objective evaluators so packs can add novel objectives without engine changes.

---

## 6. Layer 4 — ECA rules & the expression language

This is the heart of "scripting without programming." Reactive behavior is expressed as **Event → Condition → Action** rules, plus **behavior trees** for NPC AI. Both are data; both use one small, safe **expression language**.

> **Deep dive:** the full **visual/tile grammar**, the complete **vocabulary taxonomy** (concrete + abstract nouns, events, conditions, actions, modifiers, values), the **all-fire per-tick evaluation model** (Project Spark / "Laws of Kodu"), pages-as-states, `it`/`them` bindings, and the **three-tier plugin extensibility ladder** (Roslyn/WASM) are specified in [design-eca-visual-scripting.md](design-eca-visual-scripting.md). This section is the summary; that doc is authoritative for the language design.

### 6.1 Why ECA + expressions, not a general-purpose language

| Option | Verdict |
|---|---|
| Full scripting language (Lua/JS) for everyone | Too much power, too many footguns, hard to sandbox, hard for non-programmers, breaks determinism if it can call out |
| Visual node graph only | Great for onboarding, but painful for large logic and for diffing/versioning in git |
| **ECA rules + expression language (recommended)** | Covers the vast majority of game logic, trivially safe, deterministic, diffable, and can be *authored by a visual editor that emits the same data* |
| Sandboxed script host | Kept as the 5% escape hatch (§7), not the default |

ECA rules read like natural sentences and map cleanly to a future visual editor (each rule is a card: WHEN / IF / THEN).

### 6.2 Rule anatomy

```yaml
# rules/reactor-meltdown.rule.yaml
id: reactor-meltdown
when: entity.destroyed              # an engine event (see event catalog)
if: event.entity.kind == "reactor-core" and world.flag("reactor_stabilized") == false
do:
  - set_flag: { name: "meltdown_active", value: true }
  - start_event: { event: "@hull-breach-cascade" }      # existing event system
  - broadcast: { audio_cue: alarm_klaxon, region: whole-map }
  - for_each:
      in: world.actors_in_region("engine-deck")
      as: victim
      do:
        - apply_status: { target: victim, effect: irradiated, duration: 30 }
  - after: { seconds: 60 }
    do:
      - end_world: { outcome: "station_lost" }
```

Design notes:
- **`when`** is an engine event id. Events come from a **documented catalog** (see §6.4) — the same event backbone that [EventScheduler.cs](../../../Aetherium.Server/Events/EventScheduler.cs) and [ProceduralEvents.cs](../../../Aetherium.Server/Events/ProceduralEvents.cs) already provide, exposed to authors.
- **`if`** is an expression in the safe language — booleans, comparisons, arithmetic, and a curated set of **query functions** (`world.flag`, `world.actors_in_region`, `entity.has_component`, `player.reputation`). No arbitrary calls.
- **`do`** is a sequence of **actions**, and here's the crucial architectural point:

### 6.3 Actions ARE the agent tool registry (the bridge to game resources)

This is the key insight that ties scripting to the engine. Aetherium already has a **discoverable, capability-gated, parameter-schema'd verb vocabulary**: the agent tool registry ([IAgentTool.cs](../../../Aetherium.Server/Agents/Tools/IAgentTool.cs)). Agents already invoke `move`, `use`, `pickup`, `open`, `spawn_entity`, `set_terrain`, etc. through it.

**Recommendation: ECA rule actions dispatch through the same tool registry** (extended with authoring/world-mutation tools). This means:

- A rule's `spawn_entity` action, an LLM agent's `spawn_entity` call, and a `aetherctl world spawn` command all hit **one implementation** with **one capability check** and **one parameter schema**. No divergence, no triple-maintenance.
- Scripts get the tool registry's **capability gating for free** — a pack's rules run under a capability profile (like the existing agent tool profiles), so a rule can't do what its pack isn't authorized to do. Multiplayer/mod safety falls out of this.
- The tool registry is **self-documenting** — `aetherctl tools list` and `aetherctl tools describe <id>` already exist. That instantly becomes the **reference manual for scriptable actions**. Creators discover what their rules can do with a CLI command.
- New engine capabilities become scriptable the moment a tool is registered — no separate "expose to scripting" step.

So the answer to *"how does scripting interact with game resources?"* is: **through the same typed, validated, capability-gated tool interface that agents and the CLI use.** Scripts read state via the expression language's query functions; scripts mutate state via tools.

```
   ECA rule action  ─┐
   LLM agent call   ─┼──►  AgentToolRegistry  ──►  engine (World, entities, systems)
   aetherctl command─┘        (one impl, one capability check, one schema)
```

### 6.4 The event catalog (what `when` can hook)

Publish a versioned catalog of hookable events so authors know the vocabulary. Grouped, e.g.:

- **Lifecycle:** `entity.spawned`, `entity.destroyed`, `entity.damaged`, `entity.died`, `status.applied/expired`
- **Movement/space:** `actor.entered_region`, `actor.left_region`, `actor.moved`
- **Interaction:** `item.used`, `door.opened`, `container.looted`, `ability.cast`
- **Social/economy:** `reputation.changed`, `faction.war_declared`, `market.shortage`
- **Progression/quest:** `quest.started/advanced/completed`, `objective.met`, `level.gained`
- **Temporal:** `time.day_started`, `season.changed`, `timer.elapsed`, `schedule.cron`
- **World/meta:** `player.joined`, `player.left`, `world.pressure_threshold`

Each event documents its payload shape (`event.entity`, `event.region`, `event.amount`, …) so `if`/`do` expressions can reference fields with schema validation.

### 6.5 Behavior trees for NPC AI

NPC brains ([design-next-steps.md](design-next-steps.md) §4.5) are authored as data trees using the same expression language for conditions and the same tool registry for actions:

```yaml
# behaviors/drone-patrol.bt.yaml
id: drone-patrol
root:
  selector:
    - sequence:                                   # engage if enemy seen
        - condition: "self.can_see_enemy()"
        - action: { tool: face, target: "self.nearest_enemy()" }
        - action: { tool: use_ability, ability: "@emp-zap", target: "self.nearest_enemy()" }
    - sequence:                                   # otherwise patrol waypoints
        - action: { tool: move_to, target: "self.next_waypoint()" }
        - action: { tool: wait, seconds: 2 }
```

- Actions enqueue on the drone's **action budget** — the drone competes for its own cadence and keeps acting whether or not players are nearby (continuous sim).
- Conditions are expression-language queries over the drone's blackboard/perception.
- Because actions are tools, an LLM agent can *author* a behavior tree as a pack contribution, and a heuristic NPC can *run* it — same vocabulary.
- **A behavior tree and an ECA brain share one tile vocabulary** (the same events, conditions, actions, and values from [design-eca-visual-scripting.md](design-eca-visual-scripting.md) §5). A BT is an alternate *arrangement* of the same words — not a separate language. Authors who learn the ECA palette already know the BT palette.

### 6.6 Safety properties of the expression language

- **Total, not Turing-complete:** comparisons, arithmetic, boolean logic, string ops, and whitelisted query functions. `for_each` iterates a bounded, engine-provided collection; there is no `while`, no unbounded recursion.
- **Deterministic:** any randomness goes through the seeded per-tick RNG; no wall-clock, no I/O.
- **Sandboxed by construction:** the only side effects are tool invocations, each capability-checked. A rule literally cannot touch the filesystem or network.
- **Statically validatable:** `aetherctl pack validate` type-checks expressions against the event catalog and tool schemas *before* the pack ever loads.

---

## 7. Layer 5 — Sandboxed script host (the 5% escape hatch)

> **Note:** [design-eca-visual-scripting.md](design-eca-visual-scripting.md) §7 refines this into a **three-tier extensibility ladder**. The more important mechanism than raw inline scripting (Tier 3 below) is **Tier 2: plugins that register new *tiles*** (events/conditions/actions/values) into the visual palette via `[Eca*]` attributes — the same `AgentToolAttribute` pattern the engine already uses, so plugin code becomes designer-facing vocabulary (à la SC2 Native Functions / Blueprint `UFUNCTION`). Read that section for the full model; this section covers the inline-script escape hatch specifically.

For mechanics ECA rules genuinely can't express, offer a sandboxed host — **Roslyn C# scripting** (natural for a .NET shop) or **WASM** (language-agnostic, stronger isolation). Recommendation: start with Roslyn scripting behind a capability wall, evaluate WASM if untrusted third-party packs become a priority.

Constraints:
- Runs **inside a grain** with CPU/memory/time quotas; a runaway script is killed, not the silo.
- Exposes **only the tool registry + query API** — the same surface as ECA rules, no raw `System.IO`/`System.Net`. Enforced by a restricted `using` allowlist and an assembly reference whitelist.
- Still deterministic (seeded RNG injected; ambient `DateTime.Now`/`Random` blocked).
- Packs using scripts are flagged; a cluster can refuse unsigned scripted packs (`aetherctl pack policy`).

This is the ceiling. Most creators never reach it; its existence means power users aren't blocked.

---

## 8. The Game Pack

Everything above ships as one versioned unit.

```
neon-station/
  pack.yaml                 # id, version, engine-compat, atlas-version, capabilities, signature
  atlas/                    # Layer 1: tag vocabulary
  maps/                     # Layer 2: *.map.yaml, prefabs
  entities/ items/ abilities/ factions/   # Layer 3: definitions
  rules/ behaviors/ dialogue/ quests/     # Layer 4: reactive logic
  scripts/                  # Layer 5: optional sandboxed scripts
  loot/ localization/ audio/
  tests/                    # authoring tests (see §10)
```

- **`pack.yaml`** declares the **capability profile** the pack runs under (reusing agent tool profiles), engine compatibility range, atlas version, and a signature.
- **Composability:** packs can `depends_on` other packs (a "sci-fi core" pack provides atlas + abilities; a "campaign" pack adds maps + quests). Enables shared foundations and marketplaces.
- **Hot-reload:** editing a pack file and running `aetherctl pack reload` re-validates and swaps definitions into a live world where safe (definitions and rules hot-swap; structural map changes may require a region regen).

---

## 9. Where `aetherctl` fits — the creator & operator control plane

`aetherctl` is already a System.CommandLine CLI spanning `worldgen`, `world`, `narrative`, `tools`, `agent`, `session`, `prompts`, `monitor`, `vision`, `server` ([Program.cs](../../../Aetherctl/Program.cs)). It talks to Orleans grains, the SignalR ManagementHub, and a worldgen REST server. It is the natural home for the **entire game-creation and management lifecycle** — the "IDE without an IDE."

The recommendation is to organize `aetherctl` around the creator's journey:

### 9.1 Authoring (new `pack` command group)

```bash
aetherctl pack new neon-station --genre scifi      # scaffold a pack from a template
aetherctl pack validate .                          # schema + expression + reference check (offline)
aetherctl pack lint .                              # style/best-practice warnings
aetherctl pack test .                              # run authoring tests headless (§10)
aetherctl pack build . -o neon-station.aepack      # produce a signed, versioned bundle
```

`validate` is the workhorse: it type-checks every expression against the event catalog and tool schemas, resolves every `@ref`, confirms atlas tags exist, and proves quest/lock reachability using the **existing PCG validation** (`GenerationValidationService`, `pcg-validation` spec). A creator gets compiler-grade errors **without a compiler**.

### 9.2 Discovery (existing `tools` group, repurposed as the scripting manual)

```bash
aetherctl tools list --category interaction        # what actions can my rules take?
aetherctl tools describe spawn_entity              # exact params + capabilities for a rule action
aetherctl events list                              # NEW: the hookable event catalog for `when`
aetherctl events describe entity.damaged           # NEW: payload fields for `if`/`do`
```

Because rule actions *are* tools (§6.3), the existing `tools describe` output **is** the scripting API reference. Add a parallel `events` group for the `when` vocabulary.

### 9.3 Iteration loop (existing `world` + `worldgen` + `monitor`)

```bash
aetherctl world create --pack neon-station.aepack --name "Test Run" --seed 42
aetherctl worldgen render --template ... --ascii    # preview a map before running
aetherctl pack reload --world <id>                  # hot-swap edited rules/definitions
aetherctl monitor --world <id> --ascii              # watch the live sim (existing monitor)
aetherctl world sim --world <id> --for 5m --agents 8 # NEW: headless soak with agent players
```

The **tight authoring loop** — edit YAML → `pack validate` → `pack reload` → `monitor` — is the closest thing to hot-reload game scripting, entirely from the terminal. No editor, no rebuild.

### 9.4 Debugging (new inspection subcommands)

```bash
aetherctl world inspect <id> --entity <eid>        # dump an entity's components/state
aetherctl world eval <id> --expr 'world.flag("meltdown_active")'   # run a query expression live
aetherctl rules trace <id> --rule reactor-meltdown # show recent firings + condition results
aetherctl world spawn <id> --def @drone-hostile --at 3,4          # manual spawn to test a rule
```

`rules trace` is the debugger: it shows when a rule fired, what its condition evaluated to, and which actions ran/failed — the reactive-logic equivalent of breakpoints, delivered as data.

### 9.5 Management & operations (existing + roadmap)

`aetherctl` already does world lifecycle (`create/list/info/pause/resume/shutdown`), ACLs and invites, narrative CRUD, session and agent management, and prompt registry. Extend along the [design-next-steps.md](design-next-steps.md) roadmap:

```bash
aetherctl pack publish neon-station.aepack --registry <url>   # marketplace delivery
aetherctl pack policy --cluster <id> --require-signed         # trust model
aetherctl cluster events <id>                                 # live-ops event director (§4.9)
aetherctl telemetry query --world <id> --metric ttk           # gameplay telemetry (§4.12)
```

### 9.6 `aetherctl` as the substrate for GUI tools

Because `aetherctl` speaks JSON (`--json` global option already exists) and wraps the grain/REST APIs, a future **web-based visual editor** (map painter, rule-card editor, dialogue-graph editor) is a *front-end over the same commands and endpoints* — not a parallel implementation. Ship the CLI first; the GUI is a thin client on top. The worldgen REST server ([`aetherctl worldgen serve`](../../pcg-tools.md)) is the precedent.

---

## 10. Authoring tests — quality without programming

Give creators a declarative way to assert their game works, run headless via `aetherctl pack test`:

```yaml
# tests/reactor.test.yaml
scenario: "Destroying the reactor starts a meltdown"
given:
  pack: neon-station
  map: derelict-lab
  seed: 42
when:
  - spawn: { def: "@player", at: [2, 2] }
  - destroy: { entity_kind: reactor-core }
then:
  - flag: { meltdown_active: true }
  - event_fired: hull-breach-cascade
  - within: { seconds: 60 }
    assert: { world_ended: station_lost }
```

This reuses the **agent/benchmark harness** that already exists ([Data/Benchmarks](../../../Data/Benchmarks), curriculum/telemetry systems) — a pack test is a benchmark scenario with assertions. Creators get CI-grade confidence with zero C#.

---

## 11. Worked example: a small game, end to end

A creator builds *"Derelict"*, a 3-room sci-fi escape scenario, touching only YAML and `aetherctl`:

1. **`aetherctl pack new derelict --genre scifi`** — scaffolds atlas (sci-fi damage types, `oxygen`/`battery` pools), an empty map, and example rules.
2. **Author the atlas** — add `reactor-core`, `airlock`, `maintenance-drone` entity kinds; `alarm_klaxon` audio cue.
3. **Draw the map** in `derelict.map.yaml` using the legend + ASCII grid (§4.2) — three rooms, an airlock, a reactor, two drones placed by `ref`.
4. **Define content** — `drone-hostile.entity.yaml` (with `@drone-patrol` AI and `@emp-zap` ability), `plasma-cutter.item.yaml`, `keycard-red.item.yaml`, a `lab-salvage` loot table.
5. **Write the logic** — `reactor-meltdown.rule.yaml` (§6.2), a `cut-airlock` rule that opens the exit when the plasma cutter is used on it, a win condition rule (`actor.entered_region "escape-pod" → end_world: escaped`).
6. **Give the drones brains** — `drone-patrol.bt.yaml` (§6.5).
7. **`aetherctl pack validate .`** — catches a typo'd `@ref` and an expression referencing a non-existent flag. Fix, re-validate: clean.
8. **`aetherctl pack test .`** — runs `reactor.test.yaml` and an "escape works" scenario headless. Green.
9. **`aetherctl world create --pack derelict.aepack --seed 42`** then **`aetherctl monitor --ascii`** — plays it, tweaks drone `refill` speed, **`aetherctl pack reload`** to hot-swap, no restart.
10. **`aetherctl pack build . && aetherctl pack publish`** — ships it.

At no point did the creator open an IDE, write C#, or compile the server. The one place they *could* drop to Roslyn scripting (§7) — say, a custom reactor-instability curve — they didn't need to.

---

## 12. Recommendations summary

| # | Recommendation | Builds on | Priority |
|---|---|---|---|
| 1 | **Game Pack** format — versioned, signed, composable bundle of all layers | prefab/narrative JSON | P0 |
| 2 | **Content atlas** as the shared, render-/genre-agnostic tag ABI | `TileTypeDto` | P0 (shared w/ §4.10) |
| 3 | **Legend + ASCII-grid map format** compiling to semantic tiles | `PrefabTemplate` | P0 |
| 4 | **Component-composition entity/item/ability definitions** with prototype inheritance | ECS, multi-use tools | P0 |
| 5 | **ECA rules + safe expression language** for reactive logic | event system | P0 |
| 6 | **Route rule/BT actions through the agent tool registry** (one impl for scripts, agents, CLI) | `AgentToolRegistry` | P0 — keystone |
| 7 | **Published event catalog** for `when` hooks | `EventScheduler` | P1 |
| 8 | **Behavior trees** as data for NPC AI | agent tools | P1 (shared w/ §4.5) |
| 9 | **`aetherctl pack` group** — new/validate/lint/test/build/reload/publish | existing CLI | P0 |
| 10 | **`aetherctl events` + richer `tools describe`** as the scripting manual | `tools` commands | P1 |
| 11 | **`aetherctl` debug subcommands** — inspect/eval/rules trace/spawn | grain APIs | P1 |
| 12 | **Declarative authoring tests** via `pack test` | benchmark harness | P1 |
| 13 | **Sandboxed script host** (Roslyn → maybe WASM) as the 5% escape hatch | — | P2 |
| 14 | **Web visual editor** as a thin client over `aetherctl --json`/REST | worldgen REST server | P2 |

The throughline: **Aetherium already has the pieces** — declarative content (prefabs/narratives), a capability-gated verb registry (agent tools), an event backbone, and a capable CLI. The work is to *unify* them into a coherent Game Pack + ECA-scripting model and to make `aetherctl` the single, discoverable control plane for the whole create → validate → run → debug → publish loop.

---

## 13. Open questions

1. **YAML vs JSON as the authoring surface.** JSON matches existing assets and needs no new dependency; YAML is far friendlier for hand-authoring (comments, multi-line grids, less punctuation). Recommendation: accept both, document YAML, since the ASCII-grid map format is painful in JSON.
2. **Expression language: adopt or build?** Candidates: a small custom evaluator (full control, deterministic, no deps) vs. an existing embeddable one. Custom is recommended for determinism and sandbox guarantees, but it's real work — scope it deliberately.
3. **Trust model for third-party packs.** Signed-only by default is safe but adds friction for solo creators. Tiered: unsigned packs allowed on local/dev clusters, signed required on shared/hosted.
4. **How much of Layer 4 should the first visual editor cover?** Map painting and entity placement are easy wins; a full rule-card editor is a bigger lift. Ship CLI-first, GUI incrementally.
5. **Versioning & migration of packs against engine/atlas versions.** When the engine adds a tool or the atlas bumps, how do old packs behave? Needs a compatibility-range policy in `pack.yaml` and a `pack migrate` command.

---

*End of document.*
