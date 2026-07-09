# Factions & Reputation — Design Vision

**Status:** Living design. The first implementation slice is `openspec/changes/wire-factions-live`; this document is the destination it climbs toward.
**Scope:** Why factions matter, what makes them lifelike in the games players love, Aetherium's layered/composable model, the ECA scripting graduation path, and the maturity ladder mapping each tier to engine assets.
**Audience:** Engine maintainers, OpenSpec proposal authors, and game/campaign designers building on Aetherium.

---

## 1. Framing

Aetherium is an engine, not a game. A fantasy town/necromancer-cult pair and a sci-fi megacorp/hacker-collective pair must be the same machinery with different data. Everything below is therefore expressed as **per-world declarative configuration** (`FactionConfig`, threaded through world creation like `DeathPolicy`/`AbilityConfig`/`ProgressionConfig`) with an explicit graduation path to **ECA-style scripting** for games that outgrow the declarative tier. The engine ships zero factions.

The purpose of factions is not a standing number — it's **consequence**. A faction system earns its complexity only when players change their behavior because of it: sneaking instead of fighting, sparing instead of looting, courting one group knowing it costs another. Every tier below is judged by that test.

## 2. What the beloved systems teach

A survey of the faction/reputation systems players actually remember, and the one lesson each contributes:

| System | What it does | The lesson |
|---|---|---|
| **Fallout: New Vegas** (faction reputation) | Independent per-faction standing with named bands (Idolized/Accepted/Shunned/Vilified); factions react oppositely to the same act; disguises confuse attribution | Standings must be **independent and often opposed** — a single global "karma" number is the anti-pattern. Named **bands**, not raw numbers, are what players and systems reason about |
| **Middle-earth: Shadow of Mordor** (Nemesis) | Individual enemies remember specific encounters, survive defeats, hold grudges, get promoted, ambush you later | **Specific memory beats aggregate standing** for delight. The unforgettable moments are personal ("*you* burned my face"), which means a personal layer must coexist with the group layer |
| **Kenshi** | Factions own territory; patrols, prices, gate access, and enslavement all read your standing; reputation changes only when someone survives to report it | **Witnesses and information propagation.** A crime nobody saw never happened. Reaction must be *ambient* (patrols, gates, prices) — not just dialogue flags |
| **Dwarf Fortress / Crusader Kings** | Factions have internal structure (leaders, succession, values) and act on their own agendas; wars and alliances emerge from simulation pressure | Factions must **want things and act**, not just keep score. Politics as simulation output, not scripted events |
| **EVE Online** | Player-run factions with real economics; standing gates market access, taxes, and territory | Reputation is most meaningful when it gates **economic and spatial access**, not just flavor text |
| **Gothic / Morrowind guild ladders** | Joinable factions with rites, mutually exclusive advancement, rank-gated abilities and gear | **Membership and rites** give reputation a destination; mutual exclusivity makes choices real |

Distilled, the five properties a lifelike faction system needs — each mapped to the Aetherium asset it builds on:

| Property | In play | Engine asset |
|---|---|---|
| **Factions notice** | Every meaningful act shifts standings, differently per faction's values | Action-tag vocabulary + `FactionDoctrine` (wire-factions-live) |
| **Factions react visibly** | Guards greet or attack; prices shift; gates open; patrols reroute | Behavior trees (§4.5), economy markets (§4.8), `WorldAcl`, perception |
| **Factions remember specifically** | Witnessed vs. unwitnessed acts; named survivors hold grudges; memories decay | `Memory`/`Perception` components; `RelationshipMatrix` as the personal layer |
| **Factions act on their own** | Wars start from pressure; territory shifts; invasions launch | `NarrativeConsequenceEngine`, live event orchestrator (§4.9) |
| **Factions have insides** | Leaders, ranks, rites, succession; join, rise, betray | Rites/ranks; LLM agents as faction leaders (§ 6) |

The delight lives in **combinations**: kill a cult patrol with no witnesses → nothing happens; one survivor escapes → that cell specifically hunts you; the cult weakens → the consequence engine flips a border town's disposition → new trade routes open. No single system produces that — five thin layers composed do.

## 3. The layered, composable model

Five layers, each independently data-defined and swappable, communicating only through **tags** and **bands**. This is the same contract discipline as the content atlas (renderers bind to tags, never glyphs): faction consumers bind to bands and tags, never to raw numbers or engine types.

```
1. EVENTS      engine systems emit namespaced action tags
               kill:zombie · quest:completed:rescue · trade:fair · trespass:inner_sanctum · witnessed:theft
                    │
2. JUDGMENT    each faction's doctrine maps tags → standing deltas (pure data; unknown tags ignored)
                    │
3. INTERPRETATION  declarative standing BANDS name the ranges systems reason about
               hostile < -500 ≤ unfriendly < -100 ≤ neutral < +100 ≤ friendly < +500 ≤ honored
                    │
4. REACTION    consumers read bands & dispositions: behavior-tree conditions, price modifiers,
               ACL rules, dialogue selectors, patrol density
                    │
5. POLITICS    FactionRelations (directed disposition matrix) mutated over time by the
               consequence engine and live events; feeds back into layer 4
```

Design rules that keep this composable:

- **Tags are namespaced families, not a registry.** `kill:` is the first engine-emitted family (wire-factions-live); `quest:`, `trade:`, `trespass:`, `witnessed:` follow the same convention. Doctrine authors describe only the tags they care about; unknown tags mean "no rule, no effect" (shipped `DeltaFor` semantics). Mods add tag families without engine changes.
- **Bands are the public vocabulary of reputation.** NPC AI, economy, ACLs, and dialogue bind to band names, never thresholds — so a campaign retunes thresholds without touching a single consumer, and consumers written for one game work in another.
- **The group layer and the personal layer stack, not merge.** `FactionRelations`/`ReputationLedger` (group) and `RelationshipMatrix` (NPC-personal, Nemesis-style) stay separate; a future stacking rule composes them at read time (an NPC's effective disposition = faction band ⊕ personal modifier). Collapsing them would forfeit the Shadow-of-Mordor lesson.
- **One emission chokepoint.** All standing changes flow through a single grain-side `ApplyFactionAction(actor, actionTag)`; new sources (quests, trade, dialogue) are one-line callers, and upgrading the *inside* of that method (declarative → ECA) never touches call sites.

## 4. Maturity ladder

Each tier is one or two OpenSpec changes; every tier is additive on the ones below it.

| Tier | Ships | Depends on |
|---|---|---|
| **T0 — Substrate** (`wire-factions-live`, current) | Per-world `FactionConfig` (factions, doctrines, rank rules, bands, relations); `ReputationLedger` stamped at join; `kill:` tags through `ApplyFactionAction`; monotonic rank grants; read accessors | Kill chokepoint (shipped) |
| **T1 — Visible reaction** | Reputation-aware NPC behavior (behavior-tree condition nodes reading bands/dispositions: hostile bands attacked, honored ignored); standing/rank client push signal | T0 bands; behavior trees (shipped) |
| **T2 — More of life is judged** | `quest:`/`trade:`/`trespass:` tag families from their systems; standing decay toward doctrine baseline; party-shared standing credit | T0; quests (shipped), economy (§4.8), party (§4.7) |
| **T3 — Information & memory** | Witness model (only perceived acts emit `witnessed:` variants; unseen crimes don't propagate); rumor spread on movement graphs; personal-layer stacking rule (`RelationshipMatrix` grudges atop faction bands) | T1; perception (shipped) |
| **T4 — Politics & agency** | Consequence engine mutates `FactionRelations` from world events; live-event invasions keyed to faction defeats; membership/rites (join, rank-gated ability/item grants via the progression bridge); `WorldAcl` faction principals; territory/market ownership | T2; §4.8/§4.9; `IFactionGrain` appears here if cross-world factions need it, behind the same config seam |
| **T5 — Scripted & living brains** | Doctrine graduates to full ECA rules (below); LLM-agent faction leaders for hero factions | T0–T4; ECA runtime (design-eca-visual-scripting.md) |

## 5. The ECA graduation path

A doctrine entry is already a **degenerate ECA rule**: `WHEN <tag> THEN standing += δ`, with no conditions. The generalization is stepwise, and each step keeps prior config valid:

1. **Conditions on rules** — band, location tag, time, witness state:
   ```yaml
   when: kill:townsfolk
   where: [witnessed, location.tag == "town_district", actor.band(town) != hostile]
   then: [standing(town, -50)]
   ```
2. **Action lists beyond deltas** — spawn a patrol, post a bounty, mint a quest, shift a disposition, modify a market price.
3. **Faction brains as ECA tile graphs** — factions authored in the visual ECA palette ([design-eca-visual-scripting.md](audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md)), sharing the behavior-tree tile vocabulary per §4.5; mods register new events/conditions/actions via the `[Eca*]` attribute path (§4.15), under the same signing/capability model as content packs.
4. **LLM faction leaders** — the tier no other engine offers: an LLM agent (existing agent/tool infrastructure) *is* the leader of a hero faction, reading faction state through the tool registry and emitting decisions **as authored ECA rules and quests** — auditable, rate-limited, deterministic to replay — while ECA handles the thousand cheap factions. Each game chooses per faction: dictionary, ECA, or LLM.

The load-bearing property: because every tier consumes and produces the **same tag/band/disposition vocabulary**, a campaign can mix tiers freely, and upgrading one faction's brain never touches another system.

## 6. Anti-goals

- **No global karma.** Standing is always per-faction; "the world likes you" is not a concept the engine offers.
- **No hardcoded moral valence.** The engine never decides that killing is bad — doctrines do. Tags describe *what happened*, never *whether it was good*.
- **No faction content in the engine.** Names, doctrines, bands, relations are campaign data (YAML/content-pack pipeline populates `FactionConfig` with no downstream change).
- **No merged personal/group layer** — see §3.

## 7. Current state

- **Shipped:** `add-factions` primitives (`Faction`/`FactionDoctrine`/`Reputation`/`ReputationLedger`/`FactionRelations`).
- **In flight:** `openspec/changes/wire-factions-live` — T0 as specified there (per-world config, bands, `kill:` tags, rank grants, read accessors), with T1–T5 recorded as its "Later slices."
