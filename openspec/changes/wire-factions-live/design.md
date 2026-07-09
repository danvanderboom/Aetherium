## Context

`add-factions` stopped at primitives with the ownership question ("grain vs plain class") explicitly unresolved, because no live caller existed to force it. Three per-world-data wirings later (death, abilities, progression), the pattern is settled and answers it: factions are per-world declarative content compiled into plain runtime classes per map; only the config persists. The kill chokepoint the last two slices built (`AwardKillXp` at `TargetEnteredDying && targetWasMonster`, shared by melee and ability kills) is the natural first standing-change source.

This slice is **T0 of the full faction vision** documented in [docs/factions-reputation.md](../../../docs/factions-reputation.md) — the substrate (tags, doctrines, bands, ranks, relations-as-data) that tiers T1–T5 (visible NPC reaction, more judged actions, witness/memory, politics/agency, ECA & LLM faction brains) stack on additively. Two elements exist here *because* of that ladder: standing **bands** (the public vocabulary future consumers bind to) and the single **`ApplyFactionAction`** emission chokepoint (the seam ECA later replaces the inside of).

Vision constraint (unchanged): **Aetherium is an engine, not a game.** A fantasy town/necromancer-cult pair and a sci-fi megacorp/hacker-collective pair are the same machinery with different `FactionConfig` rows. The engine ships zero factions.

## Goals / Non-Goals

- Goals:
  - The core reputation loop is live and per-world data-driven: kill a creature → every faction's doctrine judges the act → standings move independently (including oppositely) → declarative rank thresholds grant ranks → all observable via read accessor.
  - Resolve `add-factions`' two open items with the established pattern: ownership (plain classes per map, config persisted — no `IFactionGrain`) and rank rules (declarative thresholds in the data tier, monotonic grants).
  - Preserve spawn-time creature identity (`CreatureTypeTag`) so doctrine rules can distinguish "wolf" from "bandit" — a general fix the standing loop needs but that also benefits any future system keying on creature kind.
  - Establish the two ceiling-compatible seams from docs/factions-reputation.md §3: declarative **standing bands** (named ranges every future consumer binds to instead of raw numbers — the content-atlas move for reputation) and the single **`ApplyFactionAction(actor, actionTag)`** chokepoint (kill-site code is just its first caller; quest/trade/trespass emitters are later one-line callers; the ECA generalization replaces its inside, never its call sites).
- Non-Goals (deferred; see tasks.md "Later slices"):
  - **Reputation-aware NPC behavior** (monsters ignoring liked players, attacking hated ones). Today's behavior trees attack any adjacent player; making aggression standing/disposition-aware is a real AI-behavior change deserving its own slice.
  - **`WorldAcl` faction principals** (task 2.4 of add-factions) — a breaking-ish change to a serialized ACL DTO, coupled to access-control semantics, not to this loop.
  - **`NarrativeConsequenceEngine` mutating `FactionRelations`** (task 2.3) — relations ship as threaded, observable data this slice with no live mutator; the consequence-engine hook is additive later.
  - **`RelationshipMatrix` stacking** (task 2.6), **ranks granting items/titles/abilities** (2.5 — ranks are now *earned* but unlock nothing yet), **player membership/rites** (`Faction.MemberIds` stays unused), non-kill standing sources (quests, trade, dialogue), cluster-scoped factions, and a client push signal (read accessors only, like progression).

## Decisions

- **Ownership: plain classes compiled per map from per-world data — no `IFactionGrain`.** Resolves add-factions task 2.1. Per-faction grains buy Orleans persistence/concurrency at the cost of a cross-grain call on every reputation check from the hot kill path; but faction *state* this slice is (a) the config — persisted on `MapState` like its three predecessors — and (b) per-player standings, which live on the in-world `Character` with the same within-run lifetime as XP/skills (the same call the user approved for progression). When cluster-scoped politics or cross-world factions arrive, an `IFactionGrain` can be introduced *behind* the same config seam without reopening this slice.
- **Kills emit a doctrine action tag, `kill:<creature-type>` — no victim-membership resolution.** The shipped `FactionDoctrine.DeltaFor(actionTag)` already models "each faction judges an act by its own values, unknown tags are ignored." Feeding it a tag derived from what was killed means the entire mechanic is the primitives working as designed: the town sets `kill:zombie → +5`, the cult sets `kill:zombie → -10`, a merchant guild says nothing and is unaffected. Resolving the victim's own faction membership (and allies-of-my-enemy chains through `FactionRelations`) is richer but needs membership rules that don't exist yet — deferred, and additive when it comes (a second emitted tag, not a redesign).
- **`CreatureTypeTag` component preserves the spawn `CreatureType` string.** Today `SpawnEntityAsync` maps "wolf"/"bear"/"bandit" onto the same `Monster` class and discards the string, so kill-site identity collapses to "Monster". A one-string component stamped at spawn (kill site prefers it, falls back to the lowercased C# type name) keeps doctrine data meaningful. Deliberately a general-purpose identity tag, not a faction-specific field.
- **Rank rules are declarative thresholds with monotonic grants.** Resolves add-factions' open question. `RankRule {MinStanding, RankId}` per faction definition; after each standing change, any rule whose threshold is met grants its rank if absent. Once earned, a rank is kept even if standing later falls — the common reputation-system convention, and it keeps `Reputation.Ranks` (a bare list) exactly as shipped. Revocation semantics can be a config flag later if a game wants them.
- **`FactionRelations` ships threaded and observable but unconsumed.** The disposition matrix rides the config (directed pairs + a `Mutual` flag mapping to the shipped `SetMutual`), compiles per map, and is exposed via `GetFactionsAsync` — so the data model is complete and the consequence-engine/NPC-hostility slices have their input waiting — but nothing reads dispositions in gameplay yet. Called out so the inert surface isn't mistaken for wired behavior.
- **Standing changes apply to the killer only, this slice.** Party/group standing credit follows the same deferral as threat-sharing (§4.7 party slice).
- **Standing bands are world-level named ranges, reported per reputation.** `StandingBand {Id, MinStanding}` entries on `FactionConfig` (shared across the world's factions — one vocabulary, not per-faction dialects); a reputation's current band = the highest-`MinStanding` band at or below its standing. Pure interpretation — bands gate nothing this slice, but every future consumer (NPC aggression, prices, ACLs, dialogue) binds to band ids, so a campaign retunes thresholds without touching consumers. A world declaring no bands simply gets no band field on its reputation DTOs.
- **All standing mutation flows through `ApplyFactionAction(actor, actionTag)`.** One grain-side method: doctrine application across the registry, rank re-evaluation, (future) band-crossing signals. The kill branch calls it with `kill:<creature-tag>`; nothing else about the method knows about kills.

## Risks / Trade-offs

- **Type-name fallback granularity.** Entities spawned outside `SpawnEntityAsync` (world-gen seeded monsters) won't carry `CreatureTypeTag` and fall back to their C# type name ("monster"/"snake"/"zombie") — coarser but correct. Config authors can target either level; worth revisiting when world-gen spawning gets creature variety.
- **Earned standing is ephemeral within-run** (component on the in-world `Character`), identical to the approved progression call; config persists, standings don't survive silo restart. Permanent reputation joins the same future per-player persistence story as XP.
- **`kill:` tag namespace is a convention, not a registry.** Doctrine tags are free strings by design (add-factions decision); this slice establishes `kill:<type>` as the first engine-emitted tag family. Documented in the spec so future emitters (quest:, trade:) follow the pattern instead of colliding.

## Migration Plan

Additive only. `FactionConfig` is nullable everywhere (null → no factions, no ledger stamped, kill hook no-ops — the pre-change state). `CreatureTypeTag` is a new component no existing code reads. No existing test or behavior depends on any of it.

## Open Questions

Resolved during scoping (recorded for traceability):

- **Ownership (add-factions 2.1):** plain classes per map from per-world config; no `IFactionGrain` until cross-world factions need one.
- **Standing mechanic this slice:** doctrine tags `kill:<creature-type>` through every faction's doctrine; victim-membership/ally-chain reactions deferred as additive tags.
- **Rank rules (add-factions open question):** declarative thresholds in the data tier, monotonic grants, no unlock effects yet.
- **Relations:** threaded + observable data only; first live consumer (consequence engine or NPC hostility) is a later slice.
- **Persistence:** within-run, matching progression's approved model.
