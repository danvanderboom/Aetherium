# Aetherium — Visual ECA Game Logic: Vocabulary, Grammar & Extensibility

**Status:** Draft for discussion
**Scope:** The design of Aetherium's Event–Condition–Action (ECA) game-logic language — its vocabulary (the "words"), its grammar (how they combine), its visual representation, and how it is extended by game authors via Roslyn/WASM plugins when the core engine's vocabulary runs out.
**Companion docs:**
- [design-authoring-and-scripting.md](design-authoring-and-scripting.md) — the 5-layer authoring model; this doc is the deep dive on **Layer 4 (§6, ECA rules)** of that doc.
- [design-next-steps.md](design-next-steps.md) — engine roadmap; the continuous-sim (§4.1), abilities (§4.3), factions (§4.6), and content-atlas (§4.10) systems this language references.

**Inspiration.** Microsoft's **Project Spark** ("Kode") — a console, gamepad-driven creation platform whose visual logic used rows of `WHEN … DO …` tiles mixing *concrete* nouns (characters, weapons like "Astro Blaster") with *abstract* ones (teams, health meters, screen regions like "screen top left", controller inputs like "left stick", camera modes, and adverb-like modifiers like "once", "ease between", "deadzone", "0.3"). This doc studies Project Spark and ~10 peer systems and proposes a vocabulary and grammar tuned to Aetherium's render-agnostic, continuous-simulation, genre-agnostic engine.

---

## 1. Design goals & constraints

The ECA language must satisfy the three engine-wide vision constraints (from [design-next-steps.md](design-next-steps.md) §1) **and** be authorable by non-programmers:

1. **Render-agnostic.** Vocabulary references *semantic tags* from the content atlas, never glyphs/sprites. The same rule drives console, Unity, and Unreal.
2. **Continuous, speed-based sim.** No turn alternation. Rules evaluate on a fixed logic tick; actions enqueue onto an actor's **action budget**. Idle players never pause the world.
3. **Genre-agnostic.** No `spell`/`sword` baked in. Damage types, resource pools, entity kinds, teams/factions are pack-defined data.
4. **Low floor, high ceiling.** A beginner assembles rules from a palette with no syntax errors possible; an expert extends the palette with real code (Roslyn/WASM) without forking the engine.
5. **Dual representation.** The canonical form is **serializable data** (Game Pack files); the visual node/tile editor and a textual form are both *projections* of that data. (MakeCode's proven model — see §3.)
6. **Deterministic & server-authoritative.** Rules run on the server, seedable, reproducible from `(seed, input-log)`.

---

## 2. Prior-art survey — what to steal, what to avoid

Eleven systems, grouped by paradigm. Each row lists the single most important lesson for Aetherium. (Full research with citations in §12.)

### 2.1 List-based ECA (closest to what we want)

| System | Model | Key lesson for Aetherium |
|---|---|---|
| **Project Spark "Kode"** | A "brain" = **pages → numbered lines → tiles**; each line is `WHEN <sensor/filters/selectors> DO <actuator/modifiers>`, read left-to-right as a sentence; pages paginated by LB/RB. Adds (over Kodu) **real typed variables** (number/bool/text/object/object-set/vector), math, `it`/`them` bindings, camera easing | **Sentence-like tile rows** mixing concrete + abstract nouns + adverbs is the target UX. Empty `WHEN` = "always true". Modifiers (`once`, `ease between`, `0.3`) attach to the tile they follow. Its documented **unmet need** — abstraction/reuse (named subroutines, factoring large brains) — is exactly what Aetherium's §7 extensibility must supply. |
| **Kodu Game Lab** (Project Spark's formally-studied ancestor) | Grammar (Touretzky): `WHEN [sensor filter* selector*] DO [actuator modifier*]` — a **non-recursive** rule, args unordered. **"Laws of Kodu"**: every tick, **all** `WHEN`s are evaluated *before any* `DO` runs (**all-fire concurrent**, not top-to-bottom); a sensor binds the **closest** match; **any rule that can run, will**; rule **order arbitrates only *conflicting* actions** (lower line wins). **Indentation = AND** the child onto the parent. **Pages = states** (one active; `switch page` transitions) | **All-fire, evaluate-then-act, per-tick** is the model to adopt for a continuous sim (see §4.6). **Indentation-as-conjunction** and **pages-as-states** are powerful and simple — but budget for their two documented usability warts: users misread all-fire as sequential, and upward-only indentation is error-prone. A tiny, legible verb set gets non-programmers productive in minutes. |
| **Warcraft III / StarCraft II triggers** | Explicit **Events / Conditions / Actions** sections; multiple events = OR; conditions = AND (with nestable And/Or blocks); actions = ordered sequence with If/Then/Else + loops | **The canonical ECA skeleton.** Multiple-events-OR, conditions-AND-by-default, actions-are-ordered is the grammar. SC2's additions (local variables, user-defined events/conditions/actions/functions, a cooperative `Wait` scheduler) are the features to adopt; WC3's global-only variables and blocking `Wait` are the traps to avoid. |
| **RPG Maker events** | Event = ordered **pages**; each page has guard conditions + a trigger type + a command list; **highest-numbered page whose conditions hold is active** | **The most learnable priority rule in existence:** "highest satisfied page wins." Pages act as a per-entity state machine. **Self-switches** (per-instance local booleans) are the idiom for "this entity remembers it fired." |

### 2.2 Node-graph & dataflow

| System | Model | Key lesson |
|---|---|---|
| **Unreal Blueprints** | Node graph with **two wire kinds**: white **execution** wires (control flow) and typed **data** wires (pulled on demand); **pure** nodes (no exec pins, lazy getters/math) vs **impure** nodes (sequenced, mutate state) | **Separate control flow from data flow**, and separate **pure evaluators** (conditions/values) from **impure actions**. Flow nodes worth having as actions: `Branch`, `Sequence`, `Gate`, `DoOnce`, `ForLoop`, `FlipFlop`. Watch out for "Blueprint spaghetti" — favor list-ECA legibility over free-form graphs for the common case. |
| **Media Molecule Dreams** | **Synchronous reactive dataflow** at a fixed **30 Hz tick**; one signal type (a number in −1..1); booleans are just {0,1}; logic gates are analog min/max; feedback cycles get an automatic 1-frame unit-delay; **microchips** encapsulate a subgraph and expose left=input / right=output **ports** | **Encapsulation + ports** (microchips) is the reuse primitive for gamepad-first creation. The **fixed-tick, whole-graph-recompute** model and **unit-delay-on-feedback** rule are directly relevant to Aetherium's continuous sim. Unified numeric signal (analog ⊇ boolean) is elegant but we'll keep richer types (see §5.5). |

### 2.3 Block-based (the accessibility science)

| System | Model | Key lesson |
|---|---|---|
| **Scratch / Blockly / MakeCode** | Block **shape encodes type**: hat (event, top-only), stack (action, sequenced), hexagon (boolean/condition), oval (value/reporter), C-block (control-flow wrapper), cap (terminator). Invalid combinations are *physically unassemblable*. MakeCode makes **text canonical and blocks a projection**, with **stable serialized block ids** | **Shape-as-type-system** eliminates a whole error class *by construction* — a beginner literally cannot plug a boolean where a number goes. Adopt the shape families. Adopt MakeCode's "text/data is source of truth, visual is a view" and **stable ids** for serialization. |

### 2.4 Text rule-DSLs (ordering & specificity)

| System | Model | Key lesson |
|---|---|---|
| **Inform 7** | Rulebooks of natural-language rules; **automatic specificity sort** (most-specific-first, via documented tie-break "Laws"); staged pipeline **before → instead → check → carry out → after → report** | **Phase-separated actions** (validate → mutate → narrate) is a clean discipline. Automatic specificity resolution is powerful but *opaque* — only adopt it with a visual "why did A beat B?" explainer and a manual override. |
| **PuzzleScript** | Pattern-rewrite rules `[ > Player \| Crate ] -> [ > Player \| > Crate ]`; **purely positional** order, run-to-fixpoint, explicit `late` phase | **Run-to-fixpoint within a tick** and an explicit **late/post-movement phase** are useful for cascade rules. Pure positional order is simple but doesn't scale to many independent authors. |
| **Twine/Harlowe, Ink** | Conditions/actions inline in content; ordering is structural flow + first-true `if/else`; `$` persistent vs `_` temp variables; Ink "gathers" auto-rejoin branches | **Variable-scope sigils** (`$` global vs `_` temp) and **content-attached rules** for dialogue/quests. Ink's gather (branch-then-auto-rejoin) for dialogue graphs. |

### 2.5 The two escape-hatch philosophies (critical for §7)

- **Construct model** — visual events and text code (JavaScript) are *different representations that interoperate* (a JS block sits inside an otherwise-visual sheet). Clean ECA table, but a conceptual seam.
- **GameMaker / SC2 / Blueprint model** — the visual layer *is* the underlying language (DnD ⇄ GML; GUI triggers compile to Galaxy/JASS; Blueprint nodes ⇄ C++ via `UFUNCTION` reflection). No seam; graduating to code is continuous.

**Aetherium chooses the second philosophy** — but with a twist unique to our architecture: because **actions already dispatch through the agent tool registry** (see [design-authoring-and-scripting.md](design-authoring-and-scripting.md) §6.3), a plugin that registers a tool *automatically* becomes a DO tile in the palette. The "expose code to designers" story is the same mechanism as SC2's *Native Function declaration* and Blueprint's `UFUNCTION(BlueprintCallable)` — see §7.

---

## 3. Core principle: three-layer artifact (data / text / visual)

Every rule exists simultaneously as:

```
   Visual tiles/nodes  ⇄  Canonical data (YAML/JSON in the Game Pack)  ⇄  Linear text form
        (editor)               (source of truth, git-diffable)              (power users)
```

- **Canonical data is the source of truth** (MakeCode's lesson). The visual editor reads/writes it; the text form is a 1:1 pretty-print. This keeps rules diff/merge-friendly in git (a weakness of pure node-graph tools like Blueprints) while still offering the console/gamepad tile UX Project Spark pioneered.
- **Every tile/token has a stable id** (`when`, `if`, `deal_damage`, `@emp-zap`) that is what's serialized — display labels and icons can be re-skinned or localized without breaking saved packs (MakeCode's `blockId` lesson).
- **Not everything need be visually expressible.** Advanced constructs may be text- or plugin-only; they degrade to an opaque "script tile" in the visual view rather than blocking the author (MakeCode's graceful-degradation lesson).

The text form (shown throughout this doc) is what a developer reads in a `.rule.yaml`; the visual form is the same data rendered as tiles.

---

## 4. The Aetherium ECA grammar

### 4.1 Rule anatomy

A **rule** has three parts, mirroring Project Spark's `WHEN … DO …` (with conditions folded into `WHEN`, split out here as `IF` for clarity):

```yaml
id: <stable-id>
when:  <trigger>              # the Event — what wakes this rule (edge or every-tick)
if:    <condition-expr>       # the Condition — a boolean guard (optional; default true)
do:    [ <action>, ... ]      # the Actions — an ordered sequence
scope: <selector>             # optional: which entities this rule is "attached to" / runs for
```

Formal-ish grammar (the canonical data shape):

```
Brain       := Page+                              # an entity's logic; one page active at a time
Page        := Rule+                              # rules on a page all-fire per tick (§4.6)
Rule        := Trigger? Condition? ActionSeq Scope? SubRule*   # Trigger optional ⇒ "always"
SubRule     := Rule                               # indented child; its condition AND-ed onto parent (§4.6)
Trigger     := EventTile Binding*                 # e.g. entity.damaged (as: victim); absent ⇒ every tick
Condition   := BoolExpr                           # AND/OR/NOT tree of predicates
ActionSeq   := Action+                            # ordered; includes flow-control actions
Action      := ToolTile Arg* Modifier*            # a DO tile = a registry tool (§7)
             | FlowTile ActionSeq*                 # branch/for-each/wait/do-once
             | 'switch_to_page' PageRef            # FSM state transition (§4.6)
BoolExpr    := Predicate | BoolExpr 'and' BoolExpr | BoolExpr 'or' BoolExpr | 'not' BoolExpr
Predicate   := Value Comparator Value | QueryTile Arg*
Value       := Literal | Property | QueryTile Arg* | Value Op Value
             | 'it' | 'them'                       # entity / entity-set bound by this rule's Trigger/Scope
Selector    := 'me' | 'players' | 'entities' Filter* | Region | ...
```

Note the top of the grammar: a **Brain** is an entity's logic, organized into **Pages** (states), each holding **Rules** that all-fire per tick; rules may nest **SubRules** whose conditions conjoin onto the parent (§4.6). This is the Project Spark/Kodu structure, generalized.

### 4.2 The tile is the atomic unit; shape encodes role

Following Scratch/Blockly, every tile has a **shape family** that makes the grammar unassemblable-if-wrong:

| Shape family | Role | Examples | Serialized as |
|---|---|---|---|
| **Trigger** (hat — top only) | `WHEN` events | `entity.damaged`, `input.pressed`, `every 2s` | `when:` |
| **Predicate** (hexagon) | `IF` booleans | `health < 30%`, `is_in_region`, `and`/`or`/`not` | inside `if:` |
| **Value** (oval) | numbers/strings/refs | `self.health`, `random(1,6)`, `@emp-zap`, `3` | args to tiles |
| **Action** (stack — sequenced) | `DO` verbs | `deal_damage`, `move`, `equip`, `set_flag` | items in `do:` |
| **Flow** (C-block — wraps actions) | control flow | `branch`, `for_each`, `do_once`, `wait` | nesting in `do:` |
| **Modifier** (adverb — attaches to prev tile) | tunes a tile | `once`, `ease between`, `deadzone`, `0.3` | keys on a tile |
| **Cap** (terminator) | stops a sequence | `stop_rule`, `end_world` | last in `do:` |

This is the direct generalization of the Project Spark screenshot: a row like `WHEN [left stick] DO [move]` is Trigger→Action; `DO [follow camera] [transition easing] [ease between] [0.3]` is Action + three Modifiers.

### 4.3 Trigger vs polled — edge vs level (Construct's central lesson)

Two evaluation modes, visually distinct in the editor (Construct marks triggers with a green arrow):

- **Triggers (edge / event-driven):** fire *once*, at the instant the engine raises the event (`entity.died`, `input.pressed`, `region.entered`). Only **one trigger per rule** (you can't AND two events; use multiple rules or OR them).
- **Polled predicates (level / every-tick):** re-tested each logic tick (`health < 30%`, `is_moving`). A polled rule has an empty/implicit `when` and runs whenever its `if` holds.
- **Edge-conversion:** a `once_while(<pred>)` modifier converts a level predicate to an edge (runs only on the tick it *becomes* true) — Construct's `Trigger once while true`, but as a first-class per-rule toggle rather than a separate condition users must remember. Also `once` (Project Spark's tile) = fire a single time ever.

This distinction is the #1 source of confusion in every ECA system studied; making edge-vs-level a **visible property of the rule** (not buried) is a deliberate design choice.

### 4.4 Combining conditions: multiple events OR, conditions AND

From WC3/SC2 (the canonical rule): to keep the common case zero-syntax,

- **Multiple `when` events** on one rule ⇒ **OR** (any fires it).
- **Multiple predicates** in `if` ⇒ **AND** by default (stacked). Explicit `any_of:`/`none_of:` blocks give OR/NOT; predicates are individually invertible.

```yaml
when: [ entity.entered_region, entity.spawned ]   # OR
if:
  all_of:                                          # AND (default; shown explicitly)
    - event.entity.kind == "player"
    - any_of:                                      # nested OR
        - world.flag("alarm_active")
        - self.faction.at_war_with(event.entity.faction)
```

### 4.5 Selectors & instance-picking (Construct's subtle, powerful idea)

A condition doesn't just return true/false — it can **filter a set of entities**, and the actions apply to the filtered set. This is how "for every enemy below 30% health, flee" is one rule with no explicit loop. In Aetherium this maps naturally onto **ECS queries**:

```yaml
when: every 1s
scope: entities.where(faction == "raiders", health.percent < 30)   # picked set
do:
  - set_ai_state: { state: flee }        # applies to EACH picked entity
```

- The picked set defaults to "all" at rule start; each filter narrows it (set intersection).
- `for_each` (a Flow tile) drops to per-entity logic when set-wide actions aren't enough.
- Selectors are the "abstract noun" backbone: `me`, `players`, `party`, `entities`, `region("engine-deck")`, `nearest(enemy)`, `event.entity`.
- **`it` and `them` bindings (Project Spark/Kodu).** Because the picked set lives on the WHEN/scope side, actions refer to it symbolically: **`it`** = the (closest/first) matched entity, **`them`** = the whole matched set. `when: see(enemy) do: attack(them)`. This is the same idea as Construct's picked instances, and it keeps the DO side from having to re-name targets — a proven ergonomics win.

### 4.6 Evaluation model: all-fire concurrent, per tick (the Project Spark / "Laws of Kodu" model)

This is the most important semantic decision, and the survey points clearly at one answer for a **continuous simulation**. The reference system — Project Spark/Kodu — does **not** run rules as a top-to-bottom procedure. It uses an **all-fire, evaluate-then-act, per-tick** model, formalized by Touretzky as the "Laws of Kodu." Aetherium adopts it, because it fits our continuous sim far better than sequential mutation:

1. **Evaluate-then-act, every tick.** Each logic tick, **all** rule conditions (`when`/`if`, over their picked entity sets) are evaluated **first**, against a consistent snapshot; **then** all triggered actions are applied. Rules are *not* an imperative sequence where an early rule's mutation changes a later rule's condition mid-pass. (This mirrors Dreams' whole-graph recompute and eliminates Construct's order-dependent same-tick mutation bugs.)
2. **Any rule that can run, will run.** A rule's position does **not** decide *whether* it fires — only whether its condition holds. Non-conflicting actions from many rules all apply in the same tick (Kodu's "Do Two Things" — e.g. move *and* play a sound).
3. **Order arbitrates only conflicts.** When two rules request **conflicting** mutations of the same state (two different move targets; `set camera follow` vs `set camera fixed`), the **higher-priority rule wins** and the loser is visibly suppressed (grayed out in the editor — Kodu's Conflict Law). Conflict detection is per **mutation target**; the engine knows which tool touches which state.
4. **Actions enqueue on the action budget.** A winning action doesn't execute instantly — it enqueues onto the actor's **action budget** ([design-next-steps.md](design-next-steps.md) §4.1). Idle players never pause the world; a monster's brain keeps firing rules and enqueuing actions on its own cadence. This is the clean marriage of the Kodu all-fire model with continuous, speed-based simulation.
5. **Deterministic.** Condition evaluation and conflict arbitration are ordered by (entity spawn order, rule index), seeded — reproducible from `(seed, input-log)`.
6. **Bounded cascades.** Rules that legitimately need to react within the same tick use an explicit **`late` phase** (PuzzleScript) that runs after primary actions; a depth cap prevents runaway cascades (exceeding it is a validation-time warning).

**Pages = states (a per-entity FSM).** A brain has **pages**; exactly **one page is active** per entity at a time, and its rules are the ones evaluated. Two page-transition styles, both supported:
- **Imperative (Project Spark/Kodu):** a rule runs `switch_to_page(alert)`; entering a page resets its `once` flags. Explicit, legible edges.
- **Declarative (RPG Maker):** pages carry guards and the **highest-priority page whose guard holds is auto-active**. Good for pure state-from-conditions.
Recommend imperative `switch_to_page` as the default (it's the reference model and reads as a clear FSM); offer declarative guards as an option.

**Nesting = AND (Project Spark/Kodu indentation).** An indented child rule runs only if its own `when`/`if` holds **and its parent can run** — the child's condition is conjoined onto the parent's context (and the parent's picked `it`/`them` are in scope). The classic idiom is an indented **blank-WHEN** child that piggybacks a second action onto the parent ("Do Two Things"). This is the same as Construct's sub-events inheriting picked instances.

**Two documented usability warts to design against** (both are the #1 pitfalls reported for Kodu/Spark):
- *All-fire is a misconception magnet* — authors read a page as sequential. Mitigation: the editor should visually signal "all rules evaluate together" (e.g. no implied numbered flow arrows between independent rules; show conflict-arbitration outcomes inline by graying the loser, exactly as Kodu does).
- *Indentation is subtle* — the empty-WHEN-fires-every-frame trap (forgetting to indent a `score +1` makes it run every tick, not once per event) and upward-only attachment cause silent bugs. Mitigation: make nesting unambiguous with explicit brackets/rails in the visual form, and surface an "this rule has no trigger and will run every tick — did you mean to nest it?" validation lint.

**Explicitly rejected:** Inform-7-style automatic specificity sorting as the *default* — too opaque for non-programmers (Inform itself needs a manual-override escape hatch because of it). It may return as an opt-in "smart resolution" mode with a visual "why did A beat B?" explainer, but all-fire + explicit conflict order is the floor.

### 4.7 Phased actions (Inform 7's discipline, adapted)

Action processing is staged so guards, mutations, and presentation don't tangle:

- **check** — validation predicates that can veto (`check attack: if target.invulnerable → stop`).
- **carry out** — the state mutation (dispatches to the tool registry / engine systems).
- **report** — emits a **semantic perception event** for renderers (never text/glyphs on the server). A `deal_damage` carry-out produces a `DamageEvent{type, intensity}` that console/Unity/Unreal each render their own way.

Most authored rules only touch `do:` (carry out); the phases exist for engine actions and advanced authors.

### 4.8 Time, delays & the continuous tick (Dreams' model, adapted)

- Rules evaluate on a fixed **logic tick** (e.g. 20 Hz), decoupled from render framerate — like Dreams' 30 Hz logic tick and Construct's `dt`.
- **`wait`** yields cooperatively (SC2's scheduler; never WC3's blocking wait): the rule's continuation is re-queued after the delay. Multiple waits don't block the sim.
- **Timers** are first-class: `every <t>` triggers, per-entity `timer.start(tag, t)` / `on_timer(tag)` (GameMaker alarms; Construct timer behavior).
- **Feedback safety:** a value that feeds back into its own source is read with a **one-tick delay** (Dreams' unit-delay rule), so integrator/accumulator patterns are well-defined and cycles can't stall a tick.

---

## 5. The vocabulary (the "words")

The heart of the language. Organized as a **taxonomy of tile categories**, each extensible by plugins (§7). This is where Project Spark's breadth — concrete *and* abstract nouns — is generalized and made genre-neutral.

### 5.1 Subjects — concrete nouns (who/what)

- **Self:** `me` (the entity this brain is attached to) — Kodu/Spark's implicit subject.
- **Entities:** by kind (`maintenance-drone`), by id, by tag, by relationship (`nearest enemy`, `event.entity`, `attacker`, `target`).
- **Players:** `players`, `nearest player`, `triggering player` (SC2's "Triggering X" accessors).
- **Items / inventory:** `my items`, `equipped weapon`, item defs (`@plasma-cutter`).
- **Groups:** `party`, `raid`, `squad`.

### 5.2 Subjects — abstract nouns (the Project Spark breadth)

This is what made Project Spark expressive. All genre-neutral and pack-defined:

- **Counters / scores / variables:** global (`world.flag`, `world.counter`), per-entity (`self.var`), per-player, temp (`_local`). Sigils `$`/`_` for scope (Harlowe).
- **Resource pools:** `health`, `oxygen`, `battery`, `mana` — whatever the pack's atlas declares ([design-next-steps.md](design-next-steps.md) §4.3). Referenced as values (`self.battery.percent`) and mutated as actions (`consume_resource`).
- **Timers & clock:** `game_time`, `time_of_day`, `season`, custom timers, `every X`.
- **Teams / factions:** `team 1` (Spark), `faction("raiders")`, `self.faction`, reputation standings ([design-next-steps.md](design-next-steps.md) §4.6).
- **Screen regions (UI space):** `screen top left`, `screen center`, HUD anchors — for `display_meter`-style actions. Render-agnostic: a semantic anchor each client positions itself.
- **Camera:** `follow camera`, `fixed camera`, `first person`, with transition modifiers.
- **Input / controller:** `left stick`, `A button`, `LT/RT`, `key pressed` — abstract input intents (see §5.6). Bound to devices client-side, never on the server.
- **RNG:** `random(a,b)`, `chance(p)`, `pick_random(set)` — always via the seeded per-tick stream (determinism).
- **Regions / space:** named map regions, `within(range)`, `line_of_sight`, distances.

### 5.3 Events (the `WHEN` vocabulary)

Grouped catalog (the hookable event bus — [design-authoring-and-scripting.md](design-authoring-and-scripting.md) §6.4 extends this):

- **Lifecycle:** `spawned`, `destroyed`, `damaged`, `died`, `status.applied/expired`, `resource.depleted`.
- **Spatial:** `entered_region`, `left_region`, `moved`, `line_of_sight_gained`.
- **Interaction:** `item.used`, `door.opened`, `container.looted`, `ability.cast`, `interact`.
- **Input:** `input.pressed/released/held`, `stick.moved` (continuous), `gesture`.
- **Temporal:** `every <t>`, `timer.elapsed`, `day_started`, `season.changed`, `schedule.cron`.
- **Social / economy:** `reputation.changed`, `faction.war_declared`, `market.shortage`, `trade.completed`.
- **Progression / quest:** `quest.started/advanced/completed`, `objective.met`, `level.gained`.
- **World / meta:** `player.joined/left`, `world.pressure_threshold`, `event.started` (live-ops).

### 5.4 Conditions (the `IF` vocabulary)

- **Comparators:** `== != < <= > >=`, `between`, `within_bounds` (SC2).
- **Boolean combinators:** `and`, `or`, `not`, `all_of`, `any_of`, `none_of`.
- **State predicates:** `has_component`, `has_item`, `is_in_region`, `can_see`, `is_status`, `flag_set`, `reputation_at_least`.
- **Selectors as predicates:** `exists(entities.where(...))`, `count(...) >= N`.

### 5.5 Actions (the `DO` vocabulary) — this IS the tool registry

The pivotal architectural decision (from [design-authoring-and-scripting.md](design-authoring-and-scripting.md) §6.3): **every DO tile is an agent-registry tool.** Categories mirror the existing tool categories plus authoring/world-mutation tools:

- **Movement:** `move`, `move_to`, `face`, `teleport`, `follow_path`.
- **Interaction/inventory:** `use`, `pickup`, `drop`, `open`, `close`, `equip`.
- **Combat/abilities:** `deal_damage`, `use_ability`, `apply_status`, `heal`, `consume_resource`.
- **World mutation:** `spawn_entity`, `destroy`, `set_terrain`, `modify_entity` (existing WorldBuilding tools).
- **State/flow:** `set_flag`, `set_var`, `add_to`, `start_quest`, `adjust_reputation`, `start_event`.
- **Presentation (semantic, render-agnostic):** `display_meter`, `broadcast` (audio cue), `play_cue`, `set_camera`, `screen_shake_intensity` — all emit tags clients interpret.
- **Flow-control (C-block tiles):** `branch` (if/else), `for_each`, `repeat`, `while`, `wait`, `do_once`, `do_n`, `gate`, `sequence`, `stop_rule` (Blueprint's flow nodes as actions).

Because actions are tools, `aetherctl tools list/describe` **is** the auto-generated action reference (and its capability gating governs what a rule may do).

### 5.6 Modifiers (the adverbs — Project Spark's tuning tiles)

Modifiers attach to the tile they follow and tune it. Directly from the screenshot:

- **`once`** — fire a single time (edge modifier on `when`).
- **`ease between` / `transition easing` / easing curves** — `linear`, `ease_in`, `ease_out`, `elastic`, `bounce` (Construct's Tween easings) on movement/camera/UI actions.
- **`deadzone`** — input threshold on a stick tile.
- **numeric literals (`0.3`)** — the trailing parameter of the tile before them (speed, duration, radius, chance).
- **`quickly` / speed** — scales an action's action-budget cost / animation cadence.
- **target modifiers** — `self`, `other`, `all` (GameMaker's Applies-To).

### 5.7 Values / reporters (the oval tiles)

- **Literals:** numbers, strings, booleans, refs (`@ability-id`, `#region-id`).
- **Properties:** `self.health.percent`, `event.amount`, `target.position`, `players.count`.
- **Math/string:** `+ - * / mod`, `min/max/clamp/lerp`, `distance`, `angle`, `round`, `join`, ternary `?:`.
- **Queries:** `nearest(...)`, `count(...)`, `random(...)`, `time_of_day`.

---

## 6. Worked examples

### 6.1 Rebuilding the Project Spark "3rd-Person Brawler" screen in Aetherium

The screenshot's page, expressed as Aetherium rules (attached to the player brain):

```yaml
# brains/brawler-player.brain.yaml   (a "page" of rules)
rules:
  - id: assign-team
    when: once
    do: [ { set_var: { target: me, var: team, value: 1 } } ]

  - id: give-weapon
    when: once                       # empty WHEN in Spark = fire on spawn
    do: [ { equip: { item: "@astro-blaster" } } ]

  - id: camera
    when: once
    do:
      - set_camera:
          mode: follow
          transition: ease_between    # "transition easing" + "ease between"
          deadzone: 0.3               # "deadzone" + "0.3"

  - id: hud-health
    when: once
    do:
      - display_meter:
          source: self.health         # "health"
          anchor: screen_top_left     # "screen top left"

  - id: locomotion
    when: input.stick                 # "left stick"
    do: [ { move: { by: event.stick_vector } } ]

  - id: jump
    when: { input.pressed: A }        # partially visible row 6
    do: [ { use_ability: "@jump" } ]
```

Note how concrete (`@astro-blaster`), abstract (`team`, `self.health`, `screen_top_left`, `input.stick`), and adverbial (`ease_between`, `deadzone: 0.3`) vocabulary coexist — exactly the Project Spark breadth, now genre-neutral and render-agnostic.

### 6.2 A continuous-sim NPC rule (no turns)

```yaml
# a raider flees when hurt — acts on its own cadence, not on the player's turn
- id: flee-when-wounded
  scope: me
  when: entity.damaged
  if: self.health.percent < 25
  do:
    - set_ai_state: { state: flee }
    - use_ability: "@smoke-bomb"          # enqueues on the raider's action budget
    - broadcast: { audio_cue: alarm_shout, radius: 8 }
```

### 6.3 A set-wide rule with instance-picking

```yaml
- id: irradiate-engine-deck
  when: entity.destroyed
  if: event.entity.kind == "reactor-core"
  do:
    - for_each:
        in: entities.where(region == "engine-deck", has_component: health)
        as: victim
        do: [ { apply_status: { target: victim, effect: irradiated, duration: 30 } } ]
```

---

## 7. Extensibility — going beyond the core vocabulary

The user's key question. A game author will inevitably need a verb, sensor, or value the core engine doesn't ship. There are **three tiers** of extension, escalating in power and cost. This mirrors the [design-authoring-and-scripting.md](design-authoring-and-scripting.md) 5-layer model but focuses on the *vocabulary* dimension.

### 7.1 Tier 1 — Compose new vocabulary from existing tiles (no code)

Authors define **reusable named rules / macros / sub-routines** — the SC2 "user-defined Events/Conditions/Actions/Functions," Scratch "My Blocks," Dreams "microchips," Blueprint "functions/macros" idea. A composite tile is just a parameterized bundle of existing tiles, saved to the pack and appearing in the palette like a built-in.

```yaml
# actions/alert-nearby-allies.action.yaml   — a new DO tile, built from existing tiles
id: alert-nearby-allies
params: [ { name: radius, type: number, default: 6 } ]
do:
  - for_each:
      in: entities.where(faction == self.faction, within: radius)
      as: ally
      do: [ { set_ai_state: { target: ally, state: alert } } ]
```

Now `alert-nearby-allies(radius: 10)` is a tile any rule can use. **This covers the majority of "the engine doesn't have X" needs without any code.** Composite tiles obey the shape grammar (their param types drive their input-slot shapes — MakeCode's signature-derived shapes).

**The reusable *unit* is the brain (Project Spark) / shared prototype (Kodu "Creatables").** Beyond single composite tiles, a whole **brain** (a set of pages+rules) is savable to a library and droppable onto any entity — Project Spark's Brain Gallery, Dreams' savable/snap-in microchips. Aetherium ties this to **entity prototypes** ([design-authoring-and-scripting.md](design-authoring-and-scripting.md) §5.1): a brain referenced by `@drone-patrol` is authored once and shared by every entity that references it, and **editing the master updates every instance** — exactly Kodu's Creatables (all clones share one brain), which was that system's only inheritance mechanism. This is critical because the single most-cited shortcoming of Project Spark/Kodu was the *lack* of abstraction and reuse as creations grew; Aetherium's composite tiles + shared brains + plugin tiles (Tier 2) are the direct remedy.

### 7.2 Tier 2 — Register new primitive tiles via Roslyn/WASM plugins (the main extensibility story)

When a genuinely new *primitive* is needed — a novel sensor, a math function, an effect the engine can't express by composition — an author ships a **plugin** that registers new tiles into the palette. This is the **keystone extensibility mechanism** and it reuses infrastructure Aetherium already has.

**The model is SC2's "Native Function declaration" + Unreal's `UFUNCTION(BlueprintCallable)`:** engine/plugin code is annotated so it appears as a typed tile in the visual palette. Aetherium already has this exact pattern in the **`AgentToolAttribute`** ([AgentToolAttribute.cs](../../../Aetherium.Server/Agents/Tools/AgentToolAttribute.cs)) — a tool declares its id, categories, capabilities, and parameter schema, and the registry makes it discoverable. **A plugin that registers a tool automatically becomes a DO tile.** We generalize the same attribute-driven registration to the other tile roles:

```csharp
// A plugin assembly (Roslyn-compiled or WASM), loaded from a signed Game Pack.

[EcaAction("cryo_freeze", "Freeze a target in carbonite",
    Categories = new[]{ "combat","control" },
    RequiredCapabilities = new[]{ "mutate_entity" })]
public sealed class CryoFreezeAction : IEcaAction        // → a DO tile (stack shape)
{
    public ToolParameterSchema Schema => /* target: entity, duration: number */;
    public Task<ActionResult> ExecuteAsync(EcaContext ctx, Args a) { /* ... */ }
}

[EcaPredicate("is_overheating", "True if the entity's heat pool is critical")]
public sealed class IsOverheating : IEcaPredicate         // → an IF tile (hexagon shape)
{ public bool Evaluate(EcaContext ctx, Entity e) => e.Get<Heat>().Percent > 0.9; }

[EcaValue("threat_score", "Computed aggro value for a target")]
public sealed class ThreatScore : IEcaValue               // → a value tile (oval shape)
{ public double Evaluate(EcaContext ctx, Entity e) => /* ... */; }

[EcaEvent("supernova_imminent")]                          // → a WHEN tile (hat shape)
public sealed class SupernovaImminent : IEcaEvent { /* raises into the event bus */ }
```

Key properties of the plugin model:

- **Same registry, one implementation.** A plugin-registered action is invocable identically by a rule, an LLM agent, and `aetherctl` — one capability check, one schema, self-documenting via `aetherctl tools describe`. No divergence (the architectural win from [design-authoring-and-scripting.md](design-authoring-and-scripting.md) §6.3).
- **Shape is inferred from the interface** (`IEcaAction`→stack, `IEcaPredicate`→hexagon, `IEcaValue`→oval, `IEcaEvent`→hat) and parameter types drive slot shapes — so plugin tiles slot into the visual grammar with **no invalid-combination possible** (Blockly's connection invariant, MakeCode's signature-derived shapes).
- **Sandboxed & quota-limited.** Plugins run inside a grain with CPU/memory/time quotas (a runaway plugin is killed, not the silo). They see **only** the `EcaContext` API — the tool registry + query surface — never raw `System.IO`/`System.Net`. Enforced by a `using`/assembly allowlist (Roslyn) or capability-scoped host imports (WASM).
- **Deterministic by construction.** The `EcaContext` injects the seeded RNG and sim clock; ambient `DateTime.Now`/`Random` are blocked. A plugin that tries nondeterminism fails validation.
- **Capability-gated.** Each tile declares `RequiredCapabilities`; the pack runs under a capability profile (reusing the existing **agent tool profiles**). A pack can't register or invoke a tile beyond its authorized capabilities — this is the multiplayer/mod safety boundary.
- **Roslyn vs WASM.** Start with **Roslyn C# scripting** (natural for the .NET engine, easy interop with ECS types). Offer **WASM** later when *untrusted third-party* packs need stronger isolation and non-C# authoring languages. Both register tiles through the same attribute/interface surface.

**Round-trip & serialization.** Plugin tiles serialize by their **stable id** plus a `plugin:` reference (`plugin: neon-station@1.2, tile: cryo_freeze`). A pack that uses a tile records the providing plugin + version in `pack.yaml` dependencies. If a plugin is missing at load, the visual editor shows the tile as an unresolved-but-preserved placeholder (graceful degradation) rather than dropping it — avoiding WC3's lossy one-way conversion trap.

### 7.3 Tier 3 — Inline sandboxed script (the last-resort escape hatch)

For one-off logic not worth packaging as a tile, a rule may contain a **script action** — a sandboxed Roslyn/WASM snippet inline in the `do:` sequence (Construct's embedded-JS-block model). Same sandbox, same `EcaContext`, same determinism rules. Appears in the visual editor as an opaque "script tile." Discouraged for shareable content (a registered tile is reusable and self-documenting); available so authors are never hard-blocked.

### 7.4 The extensibility ladder, summarized

| Tier | Mechanism | Who | Cost | Escape from what |
|---|---|---|---|---|
| 0 | Built-in tiles | everyone | none | — |
| 1 | Composite tiles (named rules/macros) | most authors | none (data) | "no single tile does X" |
| 2 | **Plugin-registered primitive tiles** (Roslyn/WASM + `[Eca*]` attributes) | power authors | write+sign a plugin | "no tile *can* do X; I need real code, but reusable & safe" |
| 3 | Inline sandboxed script | experts | write a snippet | "one-off, not worth a tile" |

Tiers 2–3 are the answer to *"how do authors go beyond the core engine?"* — annotated, sandboxed, capability-gated code that **becomes new vocabulary in the same visual palette**, exactly as SC2's Native Functions and Blueprint's `UFUNCTION` expose engine code to designers.

---

## 8. Editor & tooling implications (ties to `aetherctl`)

- **The visual editor** is a client over the canonical data + `aetherctl --json` (as argued in [design-authoring-and-scripting.md](design-authoring-and-scripting.md) §9.6). Tile palette = `aetherctl tools list` + the event catalog + pack-registered tiles.
- **Validation** (`aetherctl pack validate`) type-checks the shape grammar and every tile's schema before load — compiler-grade errors with no compiler (the "syntax errors cannot occur" guarantee extends from shape-assembly to schema-checking).
- **`rules trace`** ([design-authoring-and-scripting.md](design-authoring-and-scripting.md) §9.4) is the debugger — shows firings, condition results, and (if smart-resolution is on) *why* one rule outranked another (the Inform-explainer idea).
- **Gamepad-first authoring.** Project Spark and Dreams prove console/gamepad tile-assembly works; the tile grammar (D-pad to navigate rows, A to place, LB/RB to page) should be a first-class input target, not just mouse/keyboard.

---

## 9. Recommended updates to the other design docs

These edits keep the doc set consistent. (Proposed; not yet applied beyond cross-links.)

1. **[design-authoring-and-scripting.md](design-authoring-and-scripting.md) §6 (ECA rules & expression language):**
   - Add a forward-reference: "The visual/tile grammar, full vocabulary taxonomy, and plugin-extensibility model are specified in [design-eca-visual-scripting.md](design-eca-visual-scripting.md)."
   - Reconcile terminology: that doc's `when/if/do` is unchanged; add `scope`/selector and the trigger-vs-polled distinction from §4.3 here.
   - §6.5 (behavior trees): note that BT nodes and ECA rules **share the same tile vocabulary** (conditions, actions, values) — a BT is an alternate arrangement of the same words, not a separate language.
   - §7 (Layer 5 sandbox): replace the generic "sandboxed host" description with a pointer to the **three-tier extensibility ladder** (§7 here), since Tier 2 (plugin-registered tiles) is the more important mechanism than raw inline scripting.

2. **[design-next-steps.md](design-next-steps.md):**
   - §4.5 (behavior-driven NPC AI): cross-reference that BTs are authored in the same tile language.
   - §4.15 (modding SDK): cross-reference the `[Eca*]` attribute registration as the concrete plugin mechanism; note that content packs and logic plugins share the signing/capability model.
   - §6 (cross-cutting principles): add a 7th question — *"What new tiles (events/conditions/actions/values) does this system contribute to the ECA vocabulary, and are they render-agnostic and genre-neutral?"*

3. **New OpenSpec proposal** (`add-eca-visual-scripting`): promote this doc's §4–§7 into a spec with `tasks.md` covering: the tile schema + shape grammar, the rule evaluator on the logic tick, the selector/instance-picking engine, the `[Eca*]` registration attributes, the Roslyn sandbox host, and `aetherctl` editor/validate/trace support.

---

## 10. Key design decisions (summary)

| Decision | Choice | Rationale / source |
|---|---|---|
| Overall shape | List-based **ECA** (`when/if/do`), not free node-graph | Legibility, diffability, non-programmer floor (WC3/SC2, RPG Maker) vs "Blueprint spaghetti" |
| Type safety | **Shape-as-type-system** (hat/hex/oval/stack/C/cap) | Invalid combinations unassemblable (Scratch/Blockly) |
| Canonical form | **Data**; visual & text are projections; stable tile ids | Git-diffable, re-skinnable, roundtrip-safe (MakeCode) |
| Event model | **Trigger (edge) vs polled (level)**, visibly distinct; `once_while` bridge | #1 confusion point in every system (Construct) |
| Condition combining | multi-event **OR**, multi-predicate **AND** default | Zero-syntax common case (WC3/SC2) |
| Collections | **Selectors / instance-picking** over ECS queries; `it`/`them` bindings | One rule handles dynamic sets, no manual loops (Construct, Project Spark) |
| Evaluation | **All-fire, evaluate-then-act, per tick**; order arbitrates *conflicts* only; actions enqueue on action budget | Project Spark/Kodu "Laws of Kodu"; fits continuous sim; avoids order-dependent mutation bugs |
| State | **Pages = per-entity FSM** (imperative `switch_to_page` default; declarative guards optional) | Project Spark/Kodu; RPG Maker variant |
| Nesting | **Indentation = AND** child onto parent (sub-rules) | Project Spark/Kodu, Construct sub-events |
| Priority default | reject opaque specificity default | Inform needs manual override because of it |
| Actions | **= the agent tool registry** | One impl for rules/agents/CLI; auto-documented (Aetherium-specific) |
| Timing | fixed logic tick; cooperative `wait`; unit-delay on feedback | Continuous sim; no blocking (SC2, Dreams) |
| Extensibility | **3-tier ladder**; Tier 2 = `[Eca*]`-annotated Roslyn/WASM plugins that register **new tiles** | Expose code as designer vocabulary (SC2 Native Functions, Blueprint `UFUNCTION`) |
| Sandbox | grain-hosted, quota-limited, capability-gated, deterministic | Multiplayer/mod safety; determinism |

---

## 11. Open questions

1. **Node-graph as an *optional* view?** Some authors prefer wires (Blueprint/Dreams) to lists. Should the editor offer a graph view of the same rule data for complex flow, while keeping lists as the default? (The canonical data supports both.)
2. **How much automatic resolution?** Ship pure ordered-list priority first; gate Inform-style smart resolution behind an opt-in with a visual explainer — or skip it entirely?
3. **Analog-unified signals (Dreams) vs typed values?** Dreams' single numeric signal is elegant for gamepad tuning; Aetherium leans typed (entities, refs, pools). Do we want a Dreams-like "signal" tile family for continuous control (camera, movement blending) alongside typed logic?
4. **WASM timeline.** Roslyn first is clear. What's the trigger to invest in WASM — a public mod marketplace with untrusted authors? Non-C# author demand?
5. **Determinism vs plugin performance.** Grain-hosting every plugin tile per evaluation may be too slow for hot paths (per-tick predicates on thousands of entities). Do we need an AOT-compiled fast path for verified/signed plugins (mindful that Unreal *removed* Blueprint nativization for maintainability)?
6. **Localization of tiles.** Display labels localize freely (ids are stable), but pack-authored composite/plugin tiles need a localization catalog too — fold into the §14 localization system of the next-steps doc.

---

## 12. Prior-art references (from research)

**Project Spark / Kodu** — screenshot (primary); tile-based `WHEN/DO` "Kode." Academic/primary sources: [Touretzky, *Teaching Kodu with Physical Manipulatives* (ACM Inroads 2014)](https://www.cs.cmu.edu/~dst/Kodu/Tiles/Touretzky-Tiles-Inroads-2014.pdf) (exact grammar), [Touretzky, *Principles of Kodu Computation* / "Laws of Kodu"](https://www.cs.cmu.edu/~dst/Kodu/Essays/kodu-principles.html) (all-fire evaluation, conflict arbitration, indentation semantics), [MacLaurin, *The Design of Kodu* (ACM SIGPLAN 2011)](https://dl.acm.org/doi/10.1145/1925844.1926413), [Fowler & MacLaurin, *Kodu Game Lab: a programming environment* (Computer Games Journal 2012)](https://www.semanticscholar.org/paper/Kodu-Game-Lab:-a-programming-environment-Fowler-Fristoe/90228f8e360314e0d949e69d365546a87fcc78a5), [Stolee & Fristoe, *Expressing Computer Science Concepts Through Kodu* (SIGCSE 2011)](https://dl.acm.org/doi/10.1145/1953163.1953197); Project Spark fan docs — [How the brains work](https://projectspark.fandom.com/wiki/How_the_brains_work), [The Magical It and Them Tiles](https://projectspark.fandom.com/wiki/The_Magical_It_and_Them_Tiles), [Variables](https://projectspark.fandom.com/wiki/Variables). **Construct 3** — [How events work](https://www.construct.net/en/make-games/manuals/construct-3/project-primitives/events/how-events-work), [Conditions](https://www.construct.net/en/make-games/manuals/construct-3/project-primitives/events/conditions), [Sub-events](https://www.construct.net/en/make-games/manuals/construct-3/project-primitives/events/sub-events), [Functions](https://www.construct.net/en/make-games/manuals/construct-3/project-primitives/events/functions). **GameMaker** — [Object Events](https://manual.gamemaker.io/lts/en/The_Asset_Editors/Object_Properties/Object_Events.htm), [Event Order](https://manual.gamemaker.io/beta/en/The_Asset_Editors/Object_Properties/Event_Order.htm). **Warcraft III** — [GUI Triggering](https://wc3we.fandom.com/wiki/GUI_Triggering), [Basics of Triggers](https://www.hiveworkshop.com/threads/basics-of-triggers.32113/). **StarCraft II Galaxy** — [Trigger Conditions](https://s2editor-guides.readthedocs.io/New_Tutorials/03_Trigger_Editor/036_Conditions/), [Multithreading with Action Definitions](https://s2editor-guides.readthedocs.io/New_Tutorials/03_Trigger_Editor/057_Multithreading_with_Action_Definitions/), [GalaxyScript](https://s2editor-guides.readthedocs.io/New_Tutorials/03_Trigger_Editor/058_GalaxyScript/). **Unreal Blueprints** — [Nodes](https://dev.epicgames.com/documentation/en-us/unreal-engine/nodes-in-unreal-engine), [Flow Control](https://dev.epicgames.com/documentation/unreal-engine/flow-control-in-unreal-engine), [Exposing C++ with UFUNCTION](https://dev.epicgames.com/community/learning/tutorials/Klde/unreal-engine-custom-blueprint-nodes-exposing-c-to-blueprint-with-ufunction). **Scratch/Blockly/MakeCode** — [Scratch: Programming for All (CACM)](https://web.media.mit.edu/~mres/scratch/scratch-cacm.pdf), [Scratch Blocks](https://en.scratch-wiki.info/wiki/Blocks), [Blockly connection checks](https://docs.blockly.com/guides/create-custom-blocks/type-checks), [MakeCode defining blocks](https://makecode.com/defining-blocks). **Dreams** — [indreams Logic & Processing](https://docs.indreams.me/en/create/resources/edit-mode-guide/assembly/gadgets/logic-and-processing), [TAPgiles Wires](https://tapgiles.com/docs/wires.html), [TAPgiles limits (30 Hz tick)](https://tapgiles.com/docs/limits.html), [Signal Manipulator](https://tapgiles.com/docs/objects/signal-manipulator.html). **Inform 7** — [Check/carry out/report (WI 12.9)](https://ganelson.github.io/inform-website/book/WI_12_9.html), [Laws for Sorting Rulebooks (WI 19.16)](https://ganelson.github.io/inform-website/book/WI_19_16.html). **PuzzleScript** — [Rules](https://www.puzzlescript.net/Documentation/rules.html), [Execution Order](https://www.puzzlescript.net/Documentation/executionorder.html). **RPG Maker** — [Event Priorities and Triggers](https://www.rpgmakerweb.com/blog/event-priorities-and-triggers), [Event Page](https://rpgm.fandom.com/wiki/Event_Page). **Twine/Harlowe** — [Harlowe 3 manual](https://twine2.neocities.org/). **Ink** — [Writing with Ink](https://github.com/inkle/ink/blob/master/Documentation/WritingWithInk.md).

---

*End of document.*
