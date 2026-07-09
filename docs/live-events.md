# Live Event Orchestrator — Design Vision

**Status:** Living design. The event grain family (scheduler, instance, spawn controller, handlers) is shipped and ticking — but orphaned: nothing schedules events in production, and area tracking/broadcast are stubs — see §8.
**Scope:** What makes live events beloved rather than noisy, Aetherium's director model (signals → rules → direction → performance → consequence), the LLM game-master leap, and the maturity ladder.
**Audience:** Engine maintainers, OpenSpec proposal authors, and game/campaign designers building on Aetherium.

---

## 1. Framing

Aetherium is an engine, not a game. A zombie-siege director, a seasonal festival calendar, a galactic-war meta-campaign, and a horror game's dread pacing must be the same machinery with different data. Everything below is **per-world (and per-cluster) declarative configuration** (`EventConfig` / director rules) with an ECA graduation path. The engine ships zero events.

The core insight from the games that do this well: a live event system is not a *spawner on a timer* — it is a **dramaturg**. Its inputs are what players are doing and feeling in aggregate; its outputs are paced, legible, consequential happenings. Three tests separate beloved events from ignorable ones:

1. **Did the world cause it?** Events triggered by simulation state (your prosperity, the faction you gutted, the shortage you caused) read as *the world responding to you*. Events on bare timers read as content on a conveyor belt.
2. **Could it be failed, and does the outcome stay?** An event whose failure leaves a burned village until players retake it matters. An event that despawns and resets taught players it never happened.
3. **Was it legible from afar?** The skull-cloud lesson: an event everyone can see, name, and travel toward becomes a social gathering point. Invisible events are private spawns.

## 2. What the beloved systems teach

| System | What it does | The lesson |
|---|---|---|
| **Left 4 Dead (AI Director)** | Reads the party's health, ammo, and stress; schedules hordes, lulls, and specials to shape an intensity curve | **Direction is pacing, not spawning.** The director's real output is the *curve* — pressure, relief, crescendo. Intensity budgets beat spawn tables |
| **Helldivers 2 (Galactic War, "Joel")** | A persistent community-wide war steered by a human game-master who reacts to player behavior with new fronts, twists, and narrative | **A dramaturg with agency.** Players fight harder in a war someone is *telling*. The GM's moves become community folklore — the strongest argument that direction should be an intelligence, not a cron job |
| **Guild Wars 2 (dynamic events)** | Event chains cascade; failure is a real branch that leaves persistent state (the village stays occupied until liberated) | **Failure is content and outcomes persist.** Chains + persistence turn events into short-form emergent stories rather than loops |
| **Fortnite (one-time live events)** | Scheduled communal spectacles (the black hole, the concerts) that happen once, live, for everyone | **"Were you there?" is a product.** Scheduled, unrepeatable, cluster-wide moments create shared memory worth more than any repeatable content |
| **Sea of Thieves (world events)** | A skull cloud or ship cloud visible across the entire map announces the event to every crew | **Legibility from anywhere.** The announcement *is* the invitation; events double as social magnets |
| **Dwarf Fortress (sieges)** | Your fortress's wealth and history attract sieges, megabeasts, and diplomacy — trouble is earned | **Player success is a trigger signal.** Prosperity-indexed pressure keeps difficulty narratively justified |
| **WoW (pre-expansion invasions)** | Time-boxed world-state disruptions that break the rules everyone knows | **Temporary rule-breaking is delight.** Events may violate the world's normal grammar precisely because they're bounded |

Distilled, the five properties a living event system needs — each mapped to the Aetherium asset it builds on:

| Property | In play | Engine asset |
|---|---|---|
| **World-caused** | Triggers read pressure signals: kills by type, deaths, faction standing shifts, market shortages, prosperity, online counts | Kill chokepoint, `market:` events ([economy](economy-simulation.md)), factions, telemetry |
| **Paced** | Intensity budgets per map/party; lulls are scheduled, not accidental | Director tier (this doc) |
| **Legible** | Far-visible perception cues (`event:` tags); the announcement is the invitation | `PerceptionDto` + content atlas (shipped) |
| **Failable & persistent** | Outcomes write world state: occupation, destruction, liberation | Entity/map state, consequence engine (shipped) |
| **Consequential** | Outcomes feed factions, markets, narrative — and future event weights | `NarrativeConsequenceEngine` (shipped), factions, economy |

## 3. The layered, composable model

Five layers. The contract discipline: events consume and emit **the same tag vocabulary as everything else** — pressure signals in, `event:` perception cues and outcome tags out. No subsystem knows the director exists; it only sees tags.

```
1. SIGNALS      WorldPressure aggregates, engine-computed from existing streams:
                kills-by-type/min · deaths · standing shifts · market: events ·
                prosperity · online counts · real-world clock
                    │
2. RULES        event definitions as pure data:
                {id, triggers, weight, cooldown, duration, worldFilter,
                 intensityCost, spawnPass, perceptionCue, outcomes}
                    │
3. DIRECTION    the director evaluates rules against signals under an
                intensity budget — crescendo/lull pacing, fairness across maps
                    │
4. PERFORMANCE  instance lifecycle (Scheduled→Active→Resolved):
                spawn passes, far-visible event: perception cues, AOI tracking,
                quest hooks, participation credit
                    │
5. CONSEQUENCE  outcome tags on success/failure/expiry write back:
                map state deltas, faction relation shifts, market shocks,
                narrative consequences, future event-weight modifiers
```

Design rules that keep this composable:

- **Signals are computed by the engine; meanings are assigned by data.** The engine counts `kill:undead` per minute; only a world's event rules decide that this raises "necromancer response" weight. Same tags-not-judgments split as faction doctrines.
- **The director spends an intensity budget, not a spawn quota.** Every event declares an `intensityCost`; the budget per map/party rises and falls on a designed curve (the L4D lesson). This is what makes the difference between pacing and pestering, and it's one number designers tune per world.
- **Events announce themselves through perception.** An active event registers a far-visible `event:` cue (skull cloud, red sky, tolling bell — renderers bind the tag). Legibility is a contract requirement, not a nice-to-have; it's also what makes events speakable by the screen-reader client.
- **Every event declares its outcomes — including failure.** `outcomes: {success: [...], failure: [...], expired: [...]}` are lists of the same action vocabulary ECA uses (state deltas, tag emissions, weight modifiers). An event with no declared outcomes is invalid data: despawn-and-reset is the anti-pattern, enforced at the schema level.
- **One performance chokepoint.** All event lifecycles run through the instance grain family (scheduler → instance → spawn controller). Director tiers only change *who decides what to schedule* — cron, rules, ECA, or an LLM — never how events execute.

## 4. Creative leaps

1. **The LLM game-master, productized.** Helldivers proved a human "Joel" makes a war feel alive; no engine ships one. Aetherium's agent infrastructure makes it a feature: an LLM agent reads the signal dashboard through the tool registry and **emits its moves as authored event definitions and ECA rules** — auditable, rate-limited, replayable, bounded by the same intensity budget as any rule. The thousand cheap events run on data; the campaign's *story-shaped* moves come from a dramaturg. Every game built on Aetherium can have a Joel.
2. **Cascades as authored data.** Because factions, markets, narrative, and events all speak tags, a cascade — undead over-farmed → necromancer response event → town liberated → faction relation flips → trade route reopens → festival event — is *authorable as data* across systems that never reference each other. The engine's whole compositional bet pays off here; events are where players finally *see* it.
3. **"Were you there" as infrastructure.** Cluster-wide, one-time, real-clock events (Fortnite lesson) fall out of the existing multi-world plumbing plus a `once: true` flag — and the world-snapshot design (§4.11) means the moment can be *archived and replayed* as a museum piece. Shared memory becomes an engine feature.
4. **Pressure-valve difficulty.** Dwarf Fortress's earned sieges, generalized: prosperity/telemetry signals as first-class trigger inputs mean any world can declare "success attracts trouble" in YAML — difficulty that players narrate as consequence instead of scaling.
5. **The director as accessibility ally.** Because pacing is centralized and data-driven, a player's accessibility profile can modulate the intensity curve (longer lulls, capped simultaneity) *without changing content* — pacing accommodations no per-encounter design could offer.

## 5. Maturity ladder

| Tier | Ships | Depends on |
|---|---|---|
| **T0 — Close the orphan gap** | `EventConfig` per world; worldgen event seeds actually consumed at map init; real AOI tracking; real perception-cue broadcast (`event:` tag family); outcome declarations honored (success/failure/expired write declared state) | Event grain family (shipped, stubbed) |
| **T1 — World-caused triggers** | `WorldPressure` signal aggregation (kills, deaths, standing shifts, `market:` events); rule triggers read signals; cooldowns/weights; real-clock cron triggers | T0; telemetry (shipped) |
| **T2 — Direction** | Intensity budget per map; crescendo/lull curves; participation credit into the XP/faction chokepoints; failable chains (event outcome schedules successor) | T1 |
| **T3 — Consequence coupling** | Outcomes emit faction-relation shifts, market shocks, and narrative-consequence inputs; event-weight feedback (outcomes modify future weights) | T2; factions T4, economy T3 |
| **T4 — Cluster dramaturgy** | Cross-world campaigns; cluster-wide one-time events; event archival/replay via world snapshots | T2; multiworld (shipped) |
| **T5 — Scripted & living directors** | Event rules graduate to full ECA graphs; the LLM game-master emitting authored events/rules under budget | T0–T4; ECA runtime |

## 6. The ECA graduation path

An event rule is already a degenerate ECA rule: `WHEN pressure(undead_kills) > x THEN schedule(necromancer_response)`. The generalization:

1. **Conditions on triggers** — `where: [world.prosperity > 0.7, faction(cult).band != defeated, no_active_event_within(2km)]`.
2. **Outcome action lists** — the same vocabulary as faction/market ECA actions: state deltas, tag emissions, quest minting, relation shifts, weight modifiers.
3. **Director brains as ECA graphs** — a world's dramatic personality (relentless, mercurial, seasonal) authored in the visual palette ([design-eca-visual-scripting.md](audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md)).
4. **The LLM game-master** — reads signals via tools, writes events/rules as auditable artifacts; per world, the director is a cron table, a rule set, an ECA graph, or a Joel.

## 7. Anti-goals

- **No bare-timer content conveyor.** Real-clock triggers exist (festivals), but a world whose events *only* run on timers is using the system against its design.
- **No despawn-and-reset.** Undeclared outcomes are schema-invalid; events must leave the world different (even if the difference is "the village is safe *because* you won").
- **No director omnipotence.** The director schedules and paces; it never mutates world state directly — all effects flow through declared outcomes on the same buses every system uses.
- **No invisible events.** A perception cue is a required field.
- **No event content in the engine.** Event definitions, curves, and campaign scripts are world/cluster data.

## 8. Current state

- **Shipped and ticking:** `EventScheduler` (DI singleton, gated by `SimulationOptions.EnableProceduralEvents`, on in `appsettings.json`) and per-world `EventSchedulerGrain`; `EventInstanceGrain` (Scheduled→Active→Completed lifecycle, location + AOI radius); `SpawnControllerGrain` (spawns via `IGameMapGrain.SpawnEntityAsync`, tracks ids); `MerchantCaravanHandler`/`MonsterInvasionHandler`; `MapRegionGrain` calls `ProcessScheduledEventsAsync` every region tick. Weather/seasons tick per region behind their own flags.
- **Orphaned/stubbed:** `EventSeedPass` writes caravan/invasion seeds to worldgen shared data that **no code reads** — so the production scheduler ticks empty; `EventInstanceGrain` AOI adds *all* map players (TODO in code); `BroadcastToAreaAsync` never touches SignalR/perception; `SpawnControllerGrain.DespawnEntitiesAsync` only untracks. No openspec live-events spec exists.
- **Adjacent and live:** `NarrativeConsequenceEngine` already mints follow-up quests from gameplay events via `GameHub.ProcessNarrativeEventAsync` — the natural consumer for T3 outcome coupling.
- **The gap in one sentence:** the performance layer exists but nobody writes it material and its stage effects are stubs; T0 is a wiring slice (consume seeds, real AOI, real cues, honored outcomes), after which every higher tier is data and direction on a working stage.
