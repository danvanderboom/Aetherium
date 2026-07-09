# Party & Shared Play — Design Vision

**Status:** Living design. No implementation slice is in flight yet; the substrate (`PartyGrain`, instance entry, multi-session maps) is shipped and inert — see §8.
**Scope:** Why shared play is a design problem and not a networking problem, what the games players love teach about it, Aetherium's layered model, creative leaps the engine is uniquely positioned to make, and the maturity ladder.
**Audience:** Engine maintainers, OpenSpec proposal authors, and game/campaign designers building on Aetherium.

---

## 1. Framing

Aetherium is an engine, not a game. A four-dwarf mining crew, a two-player asymmetric puzzle duo, a 40-player battlefront, and a solo game with two LLM companions must be the same machinery with different data. Everything below is therefore expressed as **per-world declarative configuration** (`PartyConfig`, threaded through world creation like `DeathPolicy`/`AbilityConfig`/`ProgressionConfig`/`FactionConfig`) with an ECA graduation path for games that outgrow the declarative tier. The engine ships zero party rules — only the machinery of *credit, interdependence, and presence*.

The transport problem is already solved: multiple sessions share a map, each receiving FOV-filtered perception. The design problem is different — **making another player's presence a gift rather than a cost**. Every mechanic below is judged by that test: does this make me glad the other player is here?

A second framing point is unique to Aetherium: **LLM agents already play the game through the same tool API humans use through clients.** A party system designed only for humans would waste that. Every mechanic below must work identically whether the party member is a human, a heuristic bot, or an LLM companion — which falls out naturally if everything flows through the same perception/tool surfaces.

## 2. What the beloved systems teach

| System | What it does | The lesson |
|---|---|---|
| **Guild Wars 2** (open-world events) | Everyone who participates gets full credit and personal loot; resource nodes are per-player; no kill-stealing exists mechanically | **Strangers as a gift.** Shared credit and personal loot turn other players from competitors into reinforcements. The single most load-bearing multiplayer design decision of its decade |
| **Deep Rock Galactic** | Four asymmetric classes that *need* each other (Scout lights, Engineer platforms, Driller tunnels, Gunner ziplines); one salute button ("Rock and Stone") that bonds strangers; pinned/downed states only teammates can break | **Designed interdependence + cheap expressive rituals.** Interdependence comes from asymmetric *capabilities*, not stat roles; bonding comes from lightweight expressive actions, not voice chat |
| **Left 4 Dead** | Special infected pin a player until a teammate frees them; the AI Director paces intensity around the group's state | **Forced mutual rescue.** A state that only a teammate can undo is the strongest interdependence primitive there is |
| **Monster Hunter** | SOS flares for drop-in help mid-hunt; every player carves their own rewards | **Drop-in aid with zero loot friction.** Asking for help costs nothing and helping pays fully |
| **FF14** | Level-sync scales veterans down to the content; mentor bonuses reward playing with newcomers | **Scaling makes mixed-skill groups viable.** Without it, friends at different progression points cannot actually play together |
| **Sea of Thieves** | The ship is the party made physical: helm, sails, cannons, bailing are emergent roles on a shared object everyone can operate | **Shared stakes beat shared stats.** A party-owned *thing* whose fate everyone feels creates roles without classes |
| **Journey** | Anonymous pairing, no text, one chirp; among the most-loved co-op experiences ever shipped | **Minimal channels can carry deep connection.** Comms design is about expressiveness per unit of channel, not bandwidth |

Distilled, the five properties shared play needs — each mapped to the Aetherium asset it builds on:

| Property | In play | Engine asset |
|---|---|---|
| **Presence is a gift** | Shared kill/quest/faction credit; personal or rules-governed loot; no mechanical griefing incentives | Kill chokepoint (`AwardKillXp`/`ApplyFactionAction`, shipped); loot spawn helper (shipped) |
| **Interdependence** | Revivable downed states; capabilities that compose across members | `DeathPolicy` down-state (shipped), abilities (shipped) |
| **Expressive comms** | Location pings, semantic emotes — as perception events, not a chat sidecar | `PerceptionDto` stream (shipped) |
| **Shared senses** | Party members extend each other's perception within rules | Per-player semantic perception (shipped) — see §5 |
| **Shared stakes** | Party-scoped quests, shared objects, party standing | Quests, factions T2, entity system |

## 3. The layered, composable model

Five layers, each independently data-defined, communicating through the same tag/band vocabulary the rest of the engine uses. A party, to every other subsystem, is just **a credit-sharing scope and a perception scope** — subsystems never know about leaders, invites, or UI.

```
1. MEMBERSHIP   PartyGrain: roster, leader, lifecycle (shipped, inert)
                    │
2. CREDIT       PartyConfig policies applied at the existing chokepoints:
                XP split · kill/faction credit · quest progress · loot rules
                    │
3. PRESENCE     shared-perception scope: merged/annotated frames, pings,
                member vitals — all as perception events
                    │
4. INTERDEPENDENCE  revive windows (DeathPolicy bridge), assist mechanics,
                capability composition
                    │
5. SHARED STAKES    party-scoped quests, party-owned entities, party standing,
                instanced content entry (shipped)
```

Design rules that keep this composable:

- **Credit policies live at the chokepoints that already exist.** `AwardKillXp` and `ApplyFactionAction` are called from exactly two kill branches; party credit-sharing is a policy *inside* those calls (`who gets credit for this actor's act?`), never a parallel bookkeeping system. The same chokepoint discipline as factions.
- **Comms are perception events, not a chat protocol.** A ping is a `PerceptionDto` entry with a location, a semantic tag (`ping:danger`, `ping:loot`, `ping:go`), and a source member. That single decision buys: screen-reader accessibility for free (the accessibility contract already speaks perception), LLM party members receiving pings identically to humans, replayability, and renderer freedom (ASCII flash, 3D beacon, audio cue — all binding to the same tag).
- **Loot rules are a closed policy enum per party session, chosen from a world-allowed set** (`personal` default, `free_for_all`, `round_robin`, `need_greed`, `leader`). Personal-loot-by-default is the GW2/Monster Hunter lesson; worlds that *want* loot tension (hardcore extraction games) opt into it as data.
- **Nothing distinguishes human members from agent members.** The roster holds session ids; perception, pings, credit, and loot rules apply uniformly. This is a constraint on every future party feature, checked at design time.

## 4. Creative leaps

Where Aetherium can contribute something genuinely new, because of infrastructure other engines don't have:

1. **Shared senses as a real mechanic.** Because perception is server-side, semantic, and per-player, a party can *compose* senses: the scout's vision frames merge into yours annotated by source and staleness ("via Kira, 3s ago"); one member's infrared becomes the party's heat overlay; a deaf character borrows a hearing character's audio events as visual tags. No pixel-streaming engine can do this — it falls out of semantic perception. It is simultaneously a gameplay mechanic, an accessibility feature, and a reason parties feel *telepathic* in the way great co-op does.
2. **LLM companions as first-class party members.** The engine already runs LLM agents through the tool registry. A `PartyConfig` that admits agent members means every game built on Aetherium gets drop-in companions — who see pings, take shared credit, follow loot rules, and can be *asked things* — without the game authoring companion AI. Solo play becomes optional-party play everywhere.
3. **The party as a narrative unit.** The consequence engine and faction system judge actors; letting them judge *parties* (party-scoped standing credit is factions T2) makes reputation communal the way real adventuring bands are remembered — "the ones who burned the mill" — while personal grudges still attach to individuals via the personal layer.
4. **Rituals as data.** The "Rock and Stone" lesson: one cheap, expressive, party-branded gesture does more social bonding than a chat box. Emote tags are content-atlas entries; a world's YAML declares its salute. Trivial to build, disproportionate returns.

## 5. Maturity ladder

| Tier | Ships | Depends on |
|---|---|---|
| **T0 — Credit substrate** | `PartyConfig` per world (max size, allowed loot policies, XP split policy, credit radius); party membership read at the kill chokepoints: shared XP + shared faction credit within radius; personal loot default | `PartyGrain` (shipped), kill chokepoints (shipped) |
| **T1 — Presence** | Party vitals in perception frames; location pings as perception events (`ping:` tag family); member join/leave/down events pushed | T0; perception (shipped) |
| **T2 — Interdependence** | Revive interaction honoring `DeathPolicy` down-state/window; assist credit (`assist:` tags into the faction/XP chokepoints) | T0; death policy (shipped) |
| **T3 — Shared senses** | Merged perception frames (source-annotated, staleness-bounded, radius-gated per `PartyConfig`); shared map memory | T1 |
| **T4 — Shared stakes** | Party-scoped quest progress; party-owned entities; party standing credit (factions T2); level-sync/scaling policy | T0–T2; quests (shipped), factions T2 |
| **T5 — Scripted & living parties** | Party formation/rites as ECA graphs; LLM companion members with party-scoped tool access; AI-director pacing reading party state (live events bridge) | T0–T4; ECA runtime; [live-events.md](live-events.md) |

## 6. The ECA graduation path

A credit policy is already a degenerate ECA rule: `WHEN member_kills THEN split(xp, members_in_radius)`. The generalization:

1. **Conditions on credit** — `where: [member.distance < 30, member.contributed_damage]`.
2. **Party events as a tag family** — `party:member_down`, `party:wipe`, `party:objective_complete` flow through the same event bus doctrines and quests consume; a faction can honor a party's deed, a quest can require a full-party ritual.
3. **Party brains as ECA graphs** — formation rules, kick/invite policies, loot arbitration authored in the visual palette ([design-eca-visual-scripting.md](audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md)).
4. **LLM members** — companions read party state through the tool registry and act as members, rate-limited and auditable, exactly like LLM faction leaders.

## 7. Anti-goals

- **No engine-mandated trinity.** Tank/healer/DPS is one game's data (role affinity + abilities), never an engine concept.
- **No chat protocol in the engine.** Text/voice channels are client-side concerns; the engine carries only semantic comms (pings, emotes) as perception events.
- **No mechanical griefing surface by default.** Friendly fire, loot stealing, and kill stealing are opt-in world data, never defaults.
- **No human/agent distinction in any party mechanic.**

## 8. Current state

- **Shipped:** `PartyGrain`/`IPartyGrain` (roster, leader, max 5, lifecycle); `GameHub` party methods (`CreateParty`/`JoinParty`/`LeaveParty`/`GetParty`); party-aware instance entry (`EnterDungeon` → `InstanceAllocatorGrain` shares one instance across the party, with lockouts); `create_party`/`enter_dungeon` agent tools; `aetherctl party` commands; multi-session maps with per-session FOV-filtered perception (`GameSessionManager.NotifyMapMutationAsync`).
- **The gap:** nothing gameplay-side reads party membership. `AwardKillXp` credits only the killer; faction credit is individual; there are no loot rules, pings, revive interactions, or shared perception. T0 is therefore a pure wiring slice on existing chokepoints — the same shape as every `wire-X-live` change before it.
