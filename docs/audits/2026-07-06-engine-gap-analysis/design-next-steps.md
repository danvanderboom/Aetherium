# Aetherium — Design Gap Analysis & Next-Steps Roadmap

**Status:** Draft for discussion
**Scope:** Broad design review of the engine as of 2026-07. Identifies missing subsystems and proposes a prioritized set of additions.
**Audience:** Engine maintainers and OpenSpec proposal authors.

---

## 1. Framing

### 1.1 Product vision (informs every recommendation)

Three vision statements shape the recommendations below. They are non-negotiable constraints on any subsystem design.

- **Render-agnostic engine.** The console/ASCII client is a *reference* renderer, not the target. First-class clients include tile/sprite renderers (Unity 2D, Unreal 2D or 3D isometric, potential web/mobile). The server, model, and protocol MUST NOT assume glyphs, fonts, ANSI colors, or character grids. Every perception payload MUST be semantic (entity kind, state, orientation, material, lighting, animation cue) so any renderer can bind it to its own asset pack.
- **Continuous, speed-based simulation — not alternating turns.** There is no "your turn / my turn" ordering. Each actor (player, NPC, monster, hazard) has an independent **action budget** that refills at that actor's action speed. When the budget is full, the actor may perform its next queued action. Idle players do not pause the world; monsters continue to hunt, patrol, decay, and starve. In multiplayer, every player acts independently against the same continuous simulation clock.
- **Genre-agnostic content, not fantasy-locked.** The engine is a substrate for many games: fantasy dungeon crawlers, sci-fi station survival, post-apocalyptic exploration, cyberpunk heists, horror, historical. Systems named "magic" or "spells" are wrong; the correct abstraction is **abilities** with data-driven **resource pools** and **effect types**. A campaign or mod defines what those pools and types mean.

### 1.2 What this document is not

- Not an OpenSpec proposal. Each recommended subsystem should become its own `openspec/changes/*` proposal.
- Not a schedule. Priorities are proposed, but sequencing depends on team capacity and product bets.
- Not a critique of existing code. The current codebase is unusually mature for a project still finding its game loop; this doc catalogs what would round it out.

---

## 2. What Aetherium is today

An ambitious **server-authoritative, Orleans-backed, ECS-driven simulation** with substantial infrastructure already in place.

| Layer | Status |
|---|---|
| ECS core (Entity/Component, World, movement, terrain passability) | Solid — see [engine-core spec](../../../openspec/specs/engine-core/spec.md) |
| PCG pipeline (pass-based, dungeons/outdoor/cities, validation, narrative constraints, deterministic seeds) | Mature — see [Aetherium.Server/WorldGen](../../../Aetherium.Server/WorldGen) |
| Multi-world / cluster (WorldGrain, ClusterGrain, portals, ACLs, invites, content catalog) | Built out — see [Aetherium.Server/MultiWorld](../../../Aetherium.Server/MultiWorld) |
| Emergent narrative (procedural quests, NPC goals, relationship matrix, consequence engine, lore, environmental storytelling) | Prototype-strong — see [narrative-systems.md](../../narrative-systems.md) |
| Agent tool system (26+ discoverable tools, LLM agents with profiles, telemetry, curriculum, benchmarks, Blazor dashboard) | Deep — see [agents/](../../agents/) |
| Simulation infrastructure (world clock, seasons, weather, spawn management, temporal modifiers) | In place — see [Aetherium.Server/Simulation](../../../Aetherium.Server/Simulation) |
| Perception model (vision, heat, affordances streamed over SignalR) | Working |
| Clients | Console (mature), Unity 2D (in progress with gamepad), Unreal (planned) |
| Ops / management | `aetherctl` CLI, ManagementHub with B2C auth, Azure Table Storage for grain state |
| Party grouping | `PartyGrain` scaffolding exists; no gameplay mechanics wired to it yet |

The infrastructure investment is substantial. **The gap is game systems** — the loops that make players actually play.

---

## 3. Verified missing systems

These claims were verified by direct code search, not assumed from spec titles.

### 3.1 Combat — BASIC MVP EXISTS, DEPTH MISSING

**Correction (2026-07-08):** this section originally claimed combat was entirely missing. That was wrong even at the time of writing — Phase 5 item P3-7 (`add-combat-system`, merged 2026-07-04, two days before this doc's 2026-07-06 date) shipped a real melee pipeline: `Aetherium.Server/Core/CombatSystem.cs` resolves `TryAttack` (reach-checked, deterministic damage from `AttackPower` + best carried `Weapon`), `Health` is decremented and a defeated target is removed or downed, monsters retaliate on the world tick, kills feed the `enemy_defeated` quest hook, death drops loot (`SwordItem`), and per-map combat analytics are exposed (`GetCombatStatsAsync` / `aetherctl combat stats`). See [docs/audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md](../2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md) (P3-7) for the verified detail. Treat combat as **present but shallow**, not absent.

**What's genuinely still missing**, per direct code search: no damage-type packets (`kinetic`/`thermal`/etc.) or per-tag mitigation — damage is a single flat integer; no pluggable hit resolution (attack always lands if in range, no RNG/crit/miss); no status effects (`Burning`/`Slowed`/`Prone`); no `Dying`/`Corpse` entity-state transition — a defeated target is deleted or left at 0 HP, not given an interaction affordance; no threat/aggro ledger beyond simple on-tick retaliation; no ranged/projectile combat. §4.2 below still describes real, unshipped work — it should be read as "deepen the existing MVP," not "build from zero."

### 3.2 Factions & reputation — MISSING

**Evidence.** Search for `Faction|Guild|Alliance|Standing|Reputation|Allegiance` found only `RelationshipType.Alliance` inside `Narrative/Social/RelationshipMatrix.cs` — a **per-NPC-pair relationship graph** (guard likes merchant, merchant fears bandit). This is not a faction system: there is no notion of a group as a first-class entity, no shared standing between a player and a group, no rank/rite membership, no faction-gated content.

### 3.3 Character progression — MISSING

**Evidence.** No XP, no levels, no skills, no talents, no classes, no attribute stats beyond `Health`. There is `Aetherium.Server/MetaProgression` for cross-world discoveries and unlocks — a **meta layer**, not a within-character progression.

### 3.4 Abilities / spells — MISSING

**Evidence.** Search for `Ability|Spell|Cooldown|Mana|Stamina|Talent|Experience` matched only irrelevant occurrences (management API, class declarations, generator parameters). There is no ability registry, no cooldown tracker, no resource pool component, no cast/channel/instant taxonomy.

### 3.5 Behavior-driven NPC AI — MISSING

**Evidence.** `Aetherium.Server/Entities/Monster.cs`, `Snake.cs`, `Zombie.cs` are stubs with elementary `Heartbeat`-style movement (the crash trace in `Goals.txt:69` refers to `Monster.GetValidDirections`). The rich agent tool system in `Aetherium.Server/Agents/` targets LLM-driven agents for training/benchmarks, not the thousands of routine mobs a live world needs. There is **no behavior tree, no GOAP, no utility-AI evaluator, no shared blackboard, no perception→decision→action loop** for cheap NPCs.

### 3.6 Party gameplay — SCAFFOLDED, NOT WIRED

`Aetherium.Server/Groups/PartyGrain.cs` and `RaidGrain.cs` exist as Orleans grains with persistence, but there are no gameplay rules attached: no shared loot, no revive, no aggro sharing, no party-scoped quest state, no party-visible affordances in Perception.

### 3.7 Economy simulation — REFERENCED, NOT BUILT

Cluster / multi-world proposals mention markets, trade routes, transport schedules. The implementation has `ContentCatalogGrain` and cluster infrastructure, but no supply/demand model, no price signals, no caravan agents, no scarcity engine driving quests or population movements.

### 3.8 Live world events — SEEDED, NOT ORCHESTRATED

`EventSeedPass`, `SpawnControllerGrain`, `ProceduralEvents.cs`, and `SeasonManager` exist. What's missing is a **live-ops event orchestrator** that reacts to aggregate player behavior across a cluster: invasions triggered by faction defeats, plagues that spread on movement graphs, festivals timed to real-world dates, world bosses that scale to online population.

### 3.9 Death, permadeath, and respawn semantics — UNSPECIFIED

No spec of what dying costs, whether corpses persist as retrievable objects, whether characters have lives or delete, whether hardcore modes exist, or how party revive interacts with permadeath.

### 3.10 Save/migration semantics for generator changes — UNSPECIFIED

PCG is deterministic per `(seed, generator-version)`, which is right. What's missing is what happens when a generator version bumps and a live world has to migrate — do we re-roll unexplored chunks only? Freeze the world? Fork?

### 3.11 Player identity beyond session — MISSING

ManagementHub uses B2C for admin/CI. There is no unified player identity for gameplay: no profile, cosmetics, friends list, presence indicator, blocklist, mute.

### 3.12 Gameplay telemetry pipeline — MISSING (distinct from dev monitoring)

The monitoring WebSocket at `/monitor` is a dev inspector. Production games need a structured, aggregated pipeline: deaths/hour by zone, drop-rate histograms, funnel completion, exploration heatmaps, retention cohorts. The agent-training telemetry is close in shape but is scoped to agent skill, not game balance.

### 3.13 Accessibility surface — MISSING

Ironically, the Perception DTO is *already* the ideal accessibility abstraction — it describes the world semantically — but there is no screen-reader-friendly client that reads it, no colorblind palette contract for renderers, no sonification of perception events, no rebindable-input contract.

### 3.14 Localization — MISSING

Lore fragments, procedural quest text, NPC dialogue, and item names are all English strings. No i18n resource system, no translator-facing catalog, no glyph/font agnosticism for CJK/RTL scripts (which matters even more for the Unity/Unreal clients).

### 3.15 Modding / content SDK — IMPLIED, NOT SHIPPED

Prefabs, JSON audio profiles, and OpenSpec together suggest a modding culture. What's missing is a public, versioned content schema; a sandboxed scripting hook (Roslyn scripting, Lua, or WASM); and a delivery pipeline. Given Aetherium's LLM-agent bones, **LLM-authored content packs** would be a genuinely differentiating first product for this SDK.

### 3.16 Render-agnostic contract — PARTIALLY THERE, NOT ENFORCED

`TileTypeDto` and `VisualDto` are already semantic-ish (`TileTypeDto` is `{Name, Settings<string,string>}`), but the `Settings` dictionary is untyped, and the console client's rendering assumptions have leaked into naming and structure over time. There is no **content atlas** that clients register against, no versioned tile/entity vocabulary, no orientation/animation-cue schema, no light-color/intensity model that a sprite renderer can use.

---

## 4. Recommended subsystems, in detail

Each subsystem below is written to become its own OpenSpec proposal. Every one is written under the three vision constraints from §1.

### 4.1 Continuous action pipeline (foundation for combat, abilities, NPC AI)

**Purpose.** Provide the shared timing and action-resolution substrate that every actor uses. Combat, abilities, movement, and NPC AI all sit on top of this.

**Design sketch.**
- New `ActionSpeed` component on any actor. Defines an **action budget** in *action points* (AP) and a *refill rate* per world tick.
- New `ActionQueue` component. Holds the actor's next intended action (`Move`, `Attack`, `UseAbility`, `Interact`, `Wait`), plus target/parameters.
- New `ActionSystem` runs each world tick:
  1. For every actor with `ActionQueue.Head != null`, subtract the action's AP cost from the budget.
  2. If budget goes negative, action is deferred to the next tick that fills it.
  3. If budget is sufficient, execute (dispatch to `CombatSystem`, `AbilitySystem`, `MovementSystem`, `InteractionSystem`).
  4. Refill budgets by `Speed` at end of tick, capped at `MaxBudget`.
- Actions may be **interruptible** (a channeled ability drops if the actor is hit and its `Concentration` check fails) or **committed** (a swing lands even if the actor is hit).
- The engine tick rate (e.g. 20 Hz) is decoupled from actor speed. A "fast" enemy has higher Speed; a slowed one has lower. There is no global initiative order.

**Multiplayer implications.**
- Player input events arrive asynchronously over SignalR and enqueue on the player's `ActionQueue`. If the player queues faster than their budget refills, the queue caps (default depth 1 — no input buffering by default; enable per-game via config).
- Idle players never pause the world; their `ActionQueue` sits empty and monsters continue.
- All actors resolve on the same server tick, so the simulation remains authoritative and reproducible from `(seed, input-log)`.

**Render-agnostic implications.**
- Every action emits a **semantic action event** in the perception stream (e.g. `{kind: "melee_attack", actor: 42, target: 17, weapon_tag: "kinetic", intensity: 0.6}`). Clients render however they like: ASCII flash, sprite animation, 3D particle burst.
- Speed is a numeric attribute; clients may translate it to animation blend speed or attack-cycle duration.

**Priority: P0.** Nothing else in this document ships coherently without it.

---

### 4.2 Combat model

**Purpose.** Deepen the existing melee-only MVP (§3.1) into a real damage/mitigation/status pipeline usable by any genre.

**Design sketch.**
- **Damage packets** are the currency. A packet: `{types: [{tag: "kinetic", amount: 12}, {tag: "thermal", amount: 4}], source, delivery, hitLocation?, tags}`. Damage `tag` values are **data-driven per campaign** (fantasy: slashing/piercing/fire/cold/arcane; sci-fi: kinetic/plasma/EMP/radiation).
- **Mitigation** is a per-`tag` resistance stack: flat + percent + minimums. Applied in a stable order defined by the campaign.
- **Hit resolution** is pluggable. Deterministic (crit on ability tag), probabilistic (attack vs defense roll with a seedable RNG per world tick), or hybrid. The engine ships two resolvers; games can register more.
- **Status effects** are entities-on-entities with their own tick behavior (`Burning` deals thermal per tick and expires; `Slowed` reduces `ActionSpeed.Refill`; `Prone` restricts action types). Effects stack per rules the campaign declares (unique, refresh, N-stack, additive).
- **Death** is an entity state transition, not a delete. A `Dying` state gives interaction affordances (loot, revive, harvest) before the entity transitions to `Corpse` or is removed. Permadeath vs. respawn is a per-world policy set in `WorldGrain` config.
- **Threat / aggro** is a per-target ledger held by the attacker (or party). Simple by default (top-of-list heuristic); overridable per NPC AI.

**Client-agnostic.** Damage numbers, floating text, screen shake, hit sparks are all *client-side interpretations* of a semantic `DamageEvent` in the perception stream. Server emits type tags and intensity buckets; each renderer decides.

**Genre-agnostic.** No mention of swords or bullets in the core; both fit as `Weapon` components with `AbilityRef` and damage packet templates.

**Priority: P0** (co-ships with 4.1).

---

### 4.3 Abilities & resource pools (replaces "magic/spells")

**Purpose.** A generic, data-driven system for any active or triggered capability an actor can invoke — swings, spells, hacks, tech powers, gadgets, prayers, psi.

**Design sketch.**
- **`Ability` is a data asset**, not a class. Fields: `Id, ResourceCost{poolTag, amount}, ChargeTime, CastTime, RecoverTime, Cooldown, Range, TargetShape, Effects[], Tags[]`.
- **`Effects`** are composable: `DealDamage(packet)`, `ApplyStatus(effect, duration)`, `Teleport(shape)`, `Spawn(entity)`, `Summon(agent)`, `ModifyResource(pool, delta)`, `TriggerNarrativeEvent(id)`. Effect authors ship new effect kinds via the modding SDK.
- **Resource pools** are data-driven per campaign: fantasy might define `mana`, `stamina`, `focus`; sci-fi might define `battery`, `heat` (a *cooldown-inverse* pool that fills as you shoot and forces a vent), `oxygen`, `hack_charges`. Each pool has `Max`, `Regen`, `RegenPolicy` (out-of-combat, on-hit, continuous), and optional `Overheat` thresholds.
- **Charge/cast/recover phases** all consume `ActionSpeed` budget from §4.1. A channelled cast holds the budget; interrupts are `Ability`-tagged so a `Silence` status prevents `arcane`-tagged abilities but not `tech`-tagged.
- **Learning / unlocking** hooks into §4.4 progression and §5.5 modding. An ability can be granted by an item, a skill unlock, an environmental interaction (learn from a shrine / data-cache), a faction reward, or a quest.

**Client-agnostic.** Ability declares a `visual_tag` (`beam`, `melee_arc`, `projectile`, `aoe_ground`, `channel_beam`, `self_buff`). Clients bind tags to their own effects.

**Priority: P1** (needs 4.1; can ship before or beside 4.2 for non-damaging abilities).

---

### 4.4 Character progression

**Purpose.** Give characters a meaningful within-run growth arc, without locking the engine into an RPG-only role.

**Design sketch.**
- **Attributes** are a per-campaign named vector. Engine ships `Vitality` (Health max) and `Speed` (ActionSpeed refill) as defaults; campaigns add whatever they need (`Strength`, `Intellect`, `Hacking`, `Piety`).
- **Skills / talents** are `Ability`-adjacent data assets that modify effects, unlock abilities, or reshape passives. Trees / webs / point-buy are all expressible.
- **XP** is a generic `ProgressPool`; multiple pools are allowed (combat XP, exploration XP, crafting XP, faction rep). A campaign chooses which pools exist and how they convert.
- **Class / role** is optional — a campaign can enable freeform builds or fixed archetypes. The engine offers a `RoleAffinity` component with `{roleTag: weight}` that biases which abilities and skills are available.
- **Meta-progression** ([Aetherium.Server/MetaProgression](../../../Aetherium.Server/MetaProgression)) already exists across worlds. Character progression is *within-run*; the two layers should be joined by a documented handoff (which meta-unlocks add to the character start-kit, which character achievements post to meta).

**Priority: P1.**

---

### 4.5 Behavior-driven NPC AI

**Purpose.** Cheap brains for the tens of thousands of NPCs and monsters that will exist in a live cluster. Distinct from LLM agents, which remain reserved for training, hero NPCs, and mod authors.

**Design sketch.**
- Ship **behavior trees** as the default (well-understood, tool-friendly, easy to author). Nodes: `Sequence`, `Selector`, `Parallel`, `Condition`, `Action`, `Wait`, `Random`, `Utility` (score-and-pick).
- **Blackboard** per NPC, filtered from Perception. Shared blackboards for packs/hives.
- **Utility AI** as a first-class node — score candidate actions by weighted considerations (distance, threat, resource state, morale). This bridges to designers who prefer knobs over trees.
- **Actions map to the §4.1 action queue.** An AI does not "attack"; it enqueues an `Attack` action, competes for its actor's budget, and can be interrupted by higher-priority tree branches.
- **GOAP** as an optional overlay for planners (a settlement builder NPC, a heist boss coordinating a mob) — heavier and off by default.
- **Authoring.** Trees are JSON assets shipped in mod packs; a designer tool in `aetherctl` renders and validates them. LLM agents can *author* trees as a mod contribution.
- **Shared vocabulary.** Behavior trees and the ECA rule language are the *same tile vocabulary* in different arrangements — see [design-eca-visual-scripting.md](design-eca-visual-scripting.md). A BT node's conditions/actions are the same events/predicates/tools an ECA brain uses.

**Priority: P0** (co-ships with 4.1 — without it, combat has no one to fight).

---

### 4.6 Factions & reputation

**Purpose.** Groups as first-class simulation entities. Enables faction-gated content, cross-world politics, social consequences.

**Design sketch.**
- `Faction` is a persistent grain (`IFactionGrain`) with `{id, name, tags, doctrine, homeWorlds, members, rivals, allies}`. Cluster-scoped by default.
- `Reputation` is an actor-to-faction ledger: `{faction: id, standing: -1000..+1000, ranks: [], flags: []}`. Standing changes via actions (help/harm) filtered through the faction's doctrine (a pacifist faction ranks you up for peaceful resolutions).
- **Faction disposition** modifies NPC starting attitude, price mods, quest availability, access control (`WorldAclGrain` already has ACLs — factions plug in as principals).
- **Rites / ranks** unlock abilities, titles, and start-kit items, feeding §4.4 and §4.3.
- **Inter-faction state** is a sparse matrix (war, cold, neutral, ally, subordinate) that the consequence engine ([NarrativeConsequenceEngine](../../../Aetherium.Server/Narrative/Consequence/NarrativeConsequenceEngine.cs)) can mutate over time based on world events. Existing `RelationshipMatrix` becomes the *NPC-personal* layer atop this new faction layer.

**Priority: P1.**

---

### 4.7 Party & shared play

**Purpose.** Wire the existing `PartyGrain` to actual gameplay so multiplayer feels multiplayer.

**Design sketch.**
- **Shared perception scope.** A party publishes a merged perception frame client-side (server still streams per-player; client merges) so party members see through each others' senses within a documented radius.
- **Threat sharing.** Damage against a party member optionally credits threat to the whole party per §4.2's threat ledger; per-world configurable.
- **Loot rules.** Free-for-all, need/greed, round-robin, master-looter — pick per party session.
- **Revive.** Downed / Dying state (from §4.2) is party-revivable within a window; corpse-run if not.
- **Party-scoped quests.** Quest state can be party-shared (all members progress) or per-player (each solves individually). Narrative subsystem decides per quest template.
- **Voice / chat / pings** live in a separate `PartyCommsHub` — text and location pings first; voice is a later add.

**Priority: P2** (unblocks the cluster/multiworld investment; can wait for combat/abilities to land).

---

### 4.8 Economy simulation

**Purpose.** Make the cluster's markets/trade routes/scarcity behave as a real substrate for quests, faction moves, and player choices.

**Design sketch.**
- **`Market` grain per settlement** with `{stock: item→count, prices, elasticity, factionOwner}`.
- **Supply/demand** driven by two flows: a `Producer` component on entities/terrain (a mine produces `ore` at a rate) and a `Consumer` component (a settlement of size N consumes `grain` at a rate).
- **Trade routes** are directed edges on the world-graph with a capacity and a carrier (caravan NPC, teleport pad, portal). Blocked routes cause scarcity that shifts prices and generates emergent "escort the caravan" quests.
- **Currency** is a per-campaign named resource. Multi-currency (gold, credits, ration cards) is supported.
- **Scarcity → quest hooks.** The narrative subsystem subscribes to `MarketEvents` (shortage, glut, embargo) and mints quests keyed to the scarcity.

**Priority: P2** (needs factions and combat/abilities to be interesting; huge leverage once those exist).

---

### 4.9 Live event orchestrator

**Purpose.** A cluster-wide director that reacts to aggregate player behavior with world events.

**Design sketch.**
- `LiveEventDirectorGrain` reads aggregate signals (player online counts per world, recent deaths, faction standings, market shortages) and periodically evaluates a set of **event rules**.
- Events are data assets: `{id, triggers, weight, duration, worldFilter, spawnPass, narrativeHooks}`. An "Invasion" event pins a faction spawn, activates an environmental storytelling pass, and posts a cluster-wide quest.
- Real-world-clock events (festivals, seasonal cycles) integrate via cron-style triggers.
- Player influence: player actions accumulate into `WorldPressure` signals that raise event weights (many undead kills raise "necromancer response" weight).

**Priority: P2.**

---

### 4.10 Content atlas & render contract

**Purpose.** Formalize what §3.16 describes: a versioned, typed vocabulary that renderers bind to their asset packs so the server never leaks ASCII assumptions.

**Design sketch.**
- **`ContentAtlas` schema.** Types: `TerrainTag`, `EntityKindTag`, `MaterialTag`, `LightSourceTag`, `AnimationCueTag`, `EffectTag`, `AudioTag`. Each has a stable string id, a semantic description, and typed metadata (a material has `hardness`, `friction`, `combustibility`; a light source has `color`, `intensity`, `flicker`).
- **Perception frames reference atlas ids only.** Renderers subscribe to a `content-atlas.v{n}.json` and bind ids to sprites/models/glyphs. Missing bindings render as a fallback "unknown" glyph or sprite.
- **Versioning.** Atlas versions are semver. Additive changes are minor; renaming or removing a tag is a major bump. Clients declare which atlas versions they support during connection handshake.
- **Orientation & animation cues.** Actor perception includes `heading` (already exists), `intent` (`idle`, `moving`, `casting`, `attacking`, `hit`, `dying`), and `cadence` (a phase 0..1 for cycle animations). Sprite/3D clients drive animation blends from these; ASCII can ignore or use to pick a flash frame.
- **Lighting.** Replace the current scalar `LightLevel` on `VisualDto` with `{intensity, colorTag, source[]}`. Sprite clients can multiply sprite colors; ASCII picks an ANSI ramp; audio can drive reverb dampening.
- **Test:** ship a "null renderer" that consumes perception frames and asserts every referenced tag exists in the current atlas — this becomes CI-worthy protection against server-side rendering assumptions creeping in.

**Priority: P0** (blocks Unity/Unreal richness; every new gameplay system emits perception, so we want the contract set first).

---

### 4.11 Death, respawn, and world persistence policy

**Purpose.** Make the "what happens on death?" question first-class and per-world configurable.

**Design sketch.**
- **`DeathPolicy` per world:** `{permadeath: bool, corpseRetention: duration, dropOnDeath: enum, respawnPoint: enum, xpLoss: enum, downState: bool, reviveWindow: duration}`.
- **`WorldPersistencePolicy`:** how the world reacts to generator-version bumps. Options: `freeze` (world stays on old version), `regenUnexplored` (bumps only chunks with no player memory), `fork` (spin up a new world at new version, sunset the old), `migrate` (author a migration between versions).
- **Session snapshotting.** A world can be paused, snapshotted, replayed. Combined with deterministic PCG this enables replay-based debugging and cheat-detection review.

**Priority: P1.**

---

### 4.12 Gameplay telemetry pipeline

**Purpose.** Separate from dev monitoring — a production data pipeline for balance and live-ops.

**Design sketch.**
- Every gameplay-relevant event emits a structured record (`GameplayEvent{kind, actor, world, cluster, ts, data}`) into an out-of-band pipeline (Azure Event Hub / Kafka / Application Insights, per deploy).
- Rollups: deaths/hour by world/room, drop rates by item tag, quest funnel completion, TTK distributions by encounter template, exploration heatmaps per seed.
- A minimal dashboard in `Aetherium.Dashboard` (already Blazor) with cohort filters. Reuses `AgentDashboardHub` patterns.
- Feedback loop: telemetry can feed the adaptive PCG system already present in `WorldGen/Adaptation/` (which currently reacts to per-run agent telemetry).

**Priority: P2** (immediate value once combat is live; blocks tuning at scale).

---

### 4.13 Accessibility contract via perception

**Purpose.** Turn the perception model's existing semantic-first design into an actual accessibility win.

**Design sketch.**
- Ship a **screen-reader companion client** that consumes the same Perception stream any renderer does and speaks it (e.g. "goblin, 3 tiles north-east, wounded, moving toward you"). This lands almost for free once §4.10 is in place.
- **Colorblind contract.** Renderers may not encode information solely in color; every semantic distinction must be also encoded in shape/glyph/label/audio. Enforced via a lint pass on renderer bindings.
- **Sonification.** Perception events emit an `AudioCueTag` (already partly there in `AudioPerceptionDto`); games can map to spatial audio for blind or low-vision play.
- **Input contract.** Every game action has an abstract `ActionIntent` id; renderers may bind it to keyboard, gamepad, touch, gaze, sip-and-puff — none of this leaks into the server.

**Priority: P1.** Cheap to spec, differentiating to ship.

---

### 4.14 Localization

**Purpose.** Get English strings out of code.

**Design sketch.**
- All player-facing text lives in localization catalogs keyed by string id (`quest.rescue.title`, `item.torch.name`). Server sends ids and interpolation parameters; clients render in their locale.
- Generated names (lore fragments, quest titles) use grammars authored per locale, not string concatenation.
- Fonts are a client concern; the render contract must accept any Unicode string in text fields (already true).
- Machine-translation baseline (DeepL / Azure Translator) with human polish as a pipeline; LLM-authored grammars per language are a mod contribution.

**Priority: P2.**

---

### 4.15 Modding / content SDK

**Purpose.** Ship what the architecture is already promising, and make LLM-authored content a first-class citizen.

**Design sketch.**
- **Content pack format:** a zipped bundle of `content-atlas.json` deltas, prefabs, ability definitions, behavior trees, faction definitions, quest templates, localization catalogs, and (optionally) sandboxed scripts.
- **Sandboxed scripting** via Roslyn scripting or WASM with a capability-scoped API. Scripts run *inside grains* with quota limits.
- **Logic-plugin tiles.** Beyond content data, plugins register **new ECA vocabulary** (events/conditions/actions/values) into the visual palette via `[Eca*]` attributes — the same `AgentToolAttribute` registration pattern the engine already uses (SC2 Native Function / Blueprint `UFUNCTION` model). Content packs and logic plugins **share one signing/capability model.** See [design-eca-visual-scripting.md](design-eca-visual-scripting.md) §7.
- **Signing & verification.** Packs are signed; clusters can accept only whitelisted signers by default. Multiplayer parity is enforced at handshake.
- **Authoring surface:** LLM agents already speak the tool registry; extend it with a `ContentAuthoring` tool profile so an agent (or a Claude Code session) can propose a mod as a PR-shaped bundle. Combine with the existing `openspec` workflow.
- **Delivery:** local sideload first; a hosted registry later.

**Priority: P2** (pays off years of infra investment; not blocking on any specific gameplay).

---

## 5. Priority roadmap

The recommendations above collapse into four waves. Each wave is roughly one OpenSpec proposal plus prerequisites.

### Wave 0 — Foundations (P0)

Ship these first; every later system depends on them.

1. **Content atlas & render contract** (§4.10). Sets the semantic vocabulary before we grow gameplay events that would leak ASCII assumptions.
2. **Continuous action pipeline** (§4.1). The `ActionSpeed`/`ActionQueue`/`ActionSystem` triad. Cheap to spec, everything else stands on it.
3. **Combat model** (§4.2). Deepen the existing melee-only MVP into a real damage/mitigation/status system.
4. **Behavior-driven NPC AI** (§4.5). Combat needs opponents.

### Wave 1 — Character depth (P1)

5. **Abilities & resource pools** (§4.3). The genre-agnostic replacement for "spells."
6. **Character progression** (§4.4). Joins to existing meta-progression.
7. **Factions & reputation** (§4.6). Turns the relationship matrix into a full social sim.
8. **Death / respawn policy** (§4.11). Formalizes what dying means.
9. **Accessibility contract** (§4.13). Screen-reader client is a genuine differentiator; enabled by §4.10.

### Wave 2 — Living cluster (P2)

10. **Party & shared play** (§4.7). Cashes in the multiplayer investment.
11. **Economy simulation** (§4.8). Makes the cluster feel like a world.
12. **Live event orchestrator** (§4.9). Turns players into narrative pressure.
13. **Gameplay telemetry pipeline** (§4.12). Balance and live-ops.
14. **Localization** (§4.14). Enables international launch.

### Wave 3 — Platform maturity

15. **Modding / content SDK** (§4.15). Pays off the LLM-agent and OpenSpec investments.
16. Player-identity beyond session, cosmetics, friends, presence.
17. Anti-cheat surface hardening (input rate limits, teleport detection, replay-based review).

---

## 6. Cross-cutting principles that every proposal must honor

Every OpenSpec proposal spun off from this document should include a section that answers these questions explicitly.

1. **Render agnosticism.** What semantic tags does this system emit into the perception stream? Are they in the content atlas? Does the ASCII client render them purely from those tags? Would a sprite renderer or a 3D isometric renderer have all it needs? Is there any place the server has to know about a glyph?
2. **Continuous timing.** How does this system consume the `ActionSpeed` budget? What is its AP cost model? Is it interruptible? What happens on the tick when a player is idle — does anything freeze, or does the world roll on?
3. **Genre neutrality.** Are the names in the API (e.g. `spell`, `magic`, `sword`) or in the schema (data-driven tags, ability definitions, damage types)? Would this system be able to power a sci-fi station-survival campaign without renaming a class?
4. **Determinism.** Given `(seed, input-log)`, does this system reproduce byte-identically? If it uses randomness, is the RNG stream seedable and per-tick?
5. **Multiplayer symmetry.** Does this system treat all N players symmetrically? Are there any implicit assumptions of a single "current player" or a single client?
6. **Grain locality.** Which Orleans grain owns each piece of new state? How does it interact with `WorldGrain`, `ClusterGrain`, and existing subsystems' grains?
7. **ECA vocabulary.** What new tiles (events/conditions/actions/values) does this system contribute to the visual ECA language ([design-eca-visual-scripting.md](design-eca-visual-scripting.md))? Are they render-agnostic and genre-neutral? Do actions route through the agent tool registry?

---

## 7. Structural cleanup, worth doing alongside

Not features — but worth a small housekeeping sweep before the next big feature push.

- Many `openspec/specs/*/spec.md` files have stub Purpose lines: `TBD - created by archiving change …`. Fill these in; they are the entry point future proposals will read.
- Numerous top-level design/planning markdown files at repo root (`ORLEANS_IMPLEMENTATION_PLAN.md`, `IMPLEMENTATION_COMPLETE.md`, `FOV_*.md`, `MONITORING_*.md`, `SPECTRE_*.md`, `TOOL_SYSTEM_STATUS.md`, `TEST_STATUS.md`). Consolidate into `docs/` with a clear "living design" vs "historical record" split.
- The `Aetherium.Console` and `Aetherium.Server` `Enums.cs` files contain identical/parallel enums (`ActionType`, `SoundType`, `VisualType`, `FeelingType`). Fold these into a single `Aetherium.Model` catalog referenced by both — a good first render-contract deliverable.
- The `ActionType.Attack` / `SoundType.Attack` placeholders should be removed until §4.1/§4.2 land, or converted into semantic tags emitted by the new action pipeline.

---

## 8. Open questions for the maintainers

1. **Single-genre first or genre-agnostic from day one?** Design docs above assume the latter. If the first shipping *game* is a fantasy roguelike, we might allow a fantasy-flavored default content pack while keeping the engine data-driven.
2. **Live-ops ambition.** Does Aetherium want to run as a hosted live cluster (with §4.9, §4.12, telemetry, live events), or ship as a client-hosted engine users spin up themselves? The infrastructure to date implies the former; the answer shifts priorities.
3. **Modding trust model.** Sandboxed scripting is a large project. Are we willing to ship it, or should the SDK be data-only (no code) for v1?
4. **LLM-authored content as a first product.** Aetherium is unusually well-positioned to demo "Claude authors a dungeon / faction / quest bundle." Is this a marketing bet worth prioritizing?
5. **Client parity.** Do Unity and Unreal clients need to reach console-client feature parity before new gameplay ships? Or is console the reference for developers and Unity/Unreal the reference for players?

---

*End of document.*
