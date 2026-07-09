## Context

Design pass for player death/respawn — the one Phase-2 item that is *not* a mechanical reroute of an
existing call site (the prior three slices each rerouted one), because the behavior it wires does not
exist at all today. Two research passes established the ground truth:

- **Death is a no-op for players today.** Nothing checks a player's Health; a 0-HP player acts
  normally. The `Dying`/`Corpse` lifecycle is wired for monsters only.
- **`EntityId == SessionId` is load-bearing.** A session syncs its own view-location and Health mirror
  only for deltas where `delta.EntityId == Player.EntityId`. There is no "my entity id" field separate
  from the session id, and no "your entity changed" channel.
- **Health is invisible on the wire.** `PerceptionDto` has no Health field; there is no death/respawn
  signal. A client cannot learn its own HP today.
- **No YAML/game-asset pipeline exists.** Content is code-defined; the only per-world config channel is
  `WorldConfig → WorldRecipe/MapState`, flowing as an Orleans object graph from world creation.

User direction (this session): death behavior must be **configurable per game** (all four models
selectable as data), and the **client surface is in scope now** (not deferred). A follow-up round
extended this to the three open questions below (OQ1–OQ3): permadeath session behavior, respawn
invulnerability, and respawn-location precision should *all* be per-policy configuration, not engine
defaults — resolved in Slice A by enriching the `DeathPolicy` schema itself (see D1, updated).

## Goals / Non-Goals

**Goals:** a per-world, policy-driven player death loop covering all four outcome models from the
existing `DeathPolicy` schema, with a client-facing death/respawn surface, and zero behavior change for
any world that doesn't specify a policy (`DeathPolicy.Default` preserves today's "0 HP does nothing"…
except that Slice B, by definition, makes 0 HP *do something* — see the compatibility note below).

**Non-Goals (later slices):** revive mechanic; `PartyLeader` respawn points; actual `Disconnect`
session UX; `DropOnDeath`/`XpLoss` (dependencies not live); a named/tagged location registry (needed to
fully resolve `NamedLocation`/`OffsetFromNamedLocation`); a YAML asset pipeline (death config rides the
existing `WorldConfig` graph and is forward-compatible with YAML populating it later).

## Decisions

### D1 — All four models from the existing schema; `DeathPolicy` enriched, not just consumed

`Permadeath × DownStateEnabled` yields the four outcomes (see proposal table); this needed no schema
change. Revive-centric is `DownStateEnabled = true` + a future teammate-revive command that interrupts
the window — also no schema change (a later `ReviveEnabled` bool could disambiguate "down window is
just a delay" from "down window a teammate can act in," but isn't needed until the revive slice
exists).

**Revised during Slice A** in response to a follow-up round on OQ1–OQ3: the original `RespawnPoint`
(a bare `RespawnPointPolicy` enum: `LastSafeLocation`/`WorldSpawn`/`PartyLeader`) was too coarse to
express "same place," "entry location," "fixed coordinates," "an offset from coordinates," or "a
named/tagged location" — all explicitly wanted, as per-policy *configuration*, not hardcoded engine
choices. Replaced with `RespawnLocationPolicy` (`Mode` + `LocationTag`/coordinates/offset — see
`Aetherium.Model/Combat/DeathPolicy.cs`), covering nine modes: `DeathLocation`, `EntryLocation`,
`WorldSpawn`, `NamedLocation`, `FixedCoordinates`, `OffsetFromCoordinates`, `OffsetFromNamedLocation`,
`LastSafeLocation`, `PartyLeader`. Similarly added `RespawnInvulnerabilityTicks` (OQ2) and
`PermadeathBehavior : PermadeathSessionPolicy { Spectate, Disconnect }` (OQ1) as first-class fields.
**Net: every one of OQ1–OQ3 is now expressible as configuration; what's deferred to Slice B/L is
*resolving* the modes/behaviors that need a subsystem which doesn't exist yet** (a location-tag
registry for `NamedLocation`; real disconnect plumbing for `Disconnect`; a party system for
`PartyLeader`) — see tasks.md L.2–L.4.

### D2 — Per-world policy rides `WorldConfig → MapState`, not YAML, not appsettings; and now lives in `Aetherium.Model`

The only per-world config channel that already reaches `GameMapGrain` is `WorldConfig` (world creation)
→ `MapState` (persisted). `SimulationOptions` (appsettings `IOptions<T>`) is deliberately wrong — it's
engine-global, but death must be per-world (a hardcore world and a casual world coexisting in one
cluster). Slice A: added `DeathPolicy?` to `WorldConfig` + `CreateWorldRequest`, persisted on
`MapState [Id(11)]`, and replaced `GameMapGrain`'s hardcoded `_deathPolicy = DeathPolicy.Default` with
a state-sourced value (null → `Default`). Semantically the policy is a *rule*, not a
terrain-generation input, so it lives on `MapState`, not `WorldRecipe`.

**Threading mechanism, refined from the original plan:** rather than adding a `deathPolicy` parameter
to `WorldGrain.AddMapAsync` (which would also need to reach the *later*-map-creation call site in
`InstanceAllocatorGrain`), `WorldGrainState` itself now persists the world's `DeathPolicy` (set once
in `InitializeAsync`); `AddMapAsync` reads its own grain state and passes it to every map it creates,
initial or later, with zero extra plumbing at any call site.

**Project placement, revised from the original plan:** the world-creation path has two parallel front
doors — a direct `WorldConfig` (Server-only) and an `IWorldHost`/`WorldTemplate` path
(`Aetherium.Model.Worlds`, since `WorldTemplate` must be constructible by callers that don't reference
`Aetherium.Server`). For a `DeathPolicy` set via a `WorldTemplate` to reach `WorldConfig`, the type
needs to be visible from `Aetherium.Model` — but `Aetherium.Server.Combat` (where `DeathPolicy`
originally shipped) cannot be referenced from `Aetherium.Model` (`Server → Model` is the only allowed
direction). Rather than leave the `IWorldHost` path unable to configure death policy (it's the primary
"new system" path, not a legacy fallback), `DeathPolicy` and its enums moved to
`Aetherium.Model.Combat` — mirroring the `ContentAtlas` split (data schema in `Model`, server-side
seeding/consumption in `Server`). `Dying`/`Corpse`/`DamagePipeline`/`DeathSystem`/`CorpseExpirySystem`
stay in `Aetherium.Server.Combat` (they're ECS components/stateful systems tied to `Aetherium.Core`,
which only `Aetherium.Server` references) — only the plain-data policy schema moved.

This is the engine's first rules-as-data channel; a future YAML pipeline populates
`WorldConfig.DeathPolicy`/`WorldTemplate.DeathPolicy` upstream with nothing downstream changing.

### D3 — Respawn reuses the same entity id (== SessionId); it is a reset-in-place, not a new entity

The session↔entity link is pure id equality with no rebind channel. A respawn that allocated a fresh
entity id would silently break owner-sync (view-location, Health mirror) and require inventing a
"your entity is now X" protocol. Reusing the id sidesteps all of it: respawn = mutate the *existing*
entity (Health → max, WorldLocation → respawn point, clear `Downed`), emit the ordinary
`EntityMovedDelta` + Health `ComponentFieldChangedDelta` the session already knows how to apply. This
makes `JoinPlayerAsync` unusable for respawn (it rejects duplicate ids and picks a *random* spawn) — a
dedicated in-place respawn routine is needed, reconciling `_spawnsInUse`/`_playerSpawns`.

### D4 — A distinct `Downed` component for players, not the monster `Dying`

Monsters use `Dying` → (via `DeathSystem`) → `Corpse`. A player with a respawn outcome must NOT follow
that path (they respawn, they don't become a monster-style corpse). Reusing `Dying` would entangle
player-respawn with `DeathSystem`'s Dying→Corpse conversion and force interception. A dedicated
`Downed` component (its own countdown), handled by a new player-death system that reads `DeathPolicy`,
keeps the two lifecycles cleanly separate. A permadeath player *does* end as a `Corpse` (reusing the
monster terminal state is correct there — a dead-for-good body is a corpse regardless of who it was).

### D5 — Action-gating: every player command checks `Downed`

No command path gates on Health today. A downed player must be frozen: add a `Has<Downed>()` guard to
`MoveAsync`/`AttackAsync`/`RotateAsync`/`ChangeLevelAsync`/`PickupAsync`/`DropAsync`/`UseAsync`/
`OpenAsync`/`CloseAsync`, returning a clear "you are downed" failure. This mirrors the existing
monster Dying/Corpse skip in `StepNpcsAsync` and the attack-target Dying/Corpse rejection in
`DamagePipeline`. A single private `IsActionable(player)` helper keeps it one check per method.

### D6 — Client surface: a dedicated `PlayerVitalsDto` + typed death/respawn signal, not Health-in-perception

Two ways to surface own-Health: (a) add a Health field to `PerceptionDto` (rides the existing
per-tick perception push), or (b) a dedicated `PlayerVitalsDto` + explicit `ReceiveDeath`/
`ReceiveRespawn`/`ReceiveDowned` hub signals. Recommend **(b)**: perception is FOV-filtered
world-state (what you *see*), whereas own-vitals/death is player-state (what's happening *to you*) —
conflating them bloats every perception frame with self-state and still can't express the *event*
("you just died") a death screen needs, only the *level* (HP=0). A dedicated vitals DTO + explicit
transition signals is the honest shape and matches how `GameStateDto` already carries player-scoped
state (PlayerId/Heading) separately from perception. This is a new hub contract surface — the first
time player death crosses the wire.

## Compatibility note (important)

Slice A is byte-for-byte behavior-preserving (Default policy = today). **Slice B is not, by design:**
it makes 0 HP *do something* for the first time, which changes
`Tick_MonsterAdjacentToPlayer_Retaliates_DamagingButNotRemovingPlayer`'s world (that test drives a
player to partial HP and asserts survival — still true under any non-instant model, but a test that
drove a player to 0 would now see downed/respawn). The retaliation test asserts the player is *still
present* after a sub-lethal hit; under `DeathPolicy.Default` (down-then-respawn) a player reaching 0
enters `Downed` and later respawns — still present throughout — so the existing assertion holds, but
the test should be extended to cover the new 0-HP path explicitly rather than relying on sub-lethal
damage. Any world wanting the old "0 HP is inert" behavior can set `DownStateEnabled=false,
Permadeath=false` with an instant respawn at the same spot… which is still a behavior change (respawn
resets HP). There is no policy that reproduces "0 HP does literally nothing" — that was an unfinished
state, not a supported model, and Slice B intentionally ends it.

## Open questions — resolved as configuration in Slice A

All three were resolved the same way: not by picking an engine default, but by adding the field(s) so
each game built on the engine picks its own answer as data.

- **OQ1 — What does a permadeath player's *session* do?** → `DeathPolicy.PermadeathBehavior`
  (`PermadeathSessionPolicy.Spectate` or `.Disconnect`). Schema-complete; *resolving* `Spectate` into
  real spectator UX and `Disconnect` into clean connection teardown is Slice B/L.3 — the entity-side
  outcome (→ `Corpse`) is unconditional regardless of which session behavior is configured.
- **OQ2 — Respawn invulnerability window?** → `DeathPolicy.RespawnInvulnerabilityTicks` (int; `Default`
  is `3`, `0` disables it). Schema-complete; Slice B applies it when it implements the respawn routine.
- **OQ3 — Does `WorldSpawn` mean the player's original entry spawn, or a world-level spawn point?** →
  Reframed as "which precision does a game need," not an either/or: `RespawnLocationPolicy.Mode`
  distinguishes `EntryLocation` (the old OQ3 "entry spawn" answer) from `WorldSpawn` (re-run spawn
  selection — today's `SelectUnusedSpawn`, i.e. a *fresh* passable cell, not necessarily the same one)
  from `DeathLocation`/`FixedCoordinates`/`OffsetFromCoordinates`/`NamedLocation`/
  `OffsetFromNamedLocation`. `EntryLocation` still degrades to a fresh cell after a grain reactivation
  (`_playerSpawns` is ephemeral — unchanged limitation, see Risks). `NamedLocation`-family modes need
  a location-tag registry that doesn't exist (L.4) and fall back to `WorldSpawn` until it does.

## Risks

- **Slice B changes the client contract.** First player-death wire surface; any client must handle the
  new DTO/signals. Since the client is out of this repo's scope for these slices, the server ships the
  surface and documents it; a client that ignores the new signals still functions (it just won't show
  a death screen — the player still visibly respawns via the existing perception push).
- **`_playerSpawns` is ephemeral.** Respawn-to-entry-spawn degrades to a random passable cell after a
  grain reactivation. Acceptable; a persisted spawn (or world-level spawn point) is a follow-up.
- **Serializer id bump on `MapState [Id(11)]`** — additive, matches the existing versioning discipline
  (`PersistenceVersioningTests`); a null policy on old persisted state hydrates to `Default`.
- **`DeathPolicy`'s project move (`Aetherium.Server.Combat` → `Aetherium.Model.Combat`) touches every
  file that referenced it** (`CorpseExpirySystem`, `CorpseAge`, `GameMapGrain`, `DeathPolicyTests`,
  `CorpseExpirySystemTests`, `WorldModels.cs`) — a using-directive change in each, zero behavior
  change, confirmed by a full solution build (0 errors) before running tests. `Dying`/`Corpse`/the
  actual combat systems are untouched and stay in `Aetherium.Server.Combat`.
- **`IGameMapGrain.GetDeathPolicyAsync()` is a new public grain method**, not test-only scaffolding —
  mirrors the already-shipped `GetCombatStatsAsync()` read-accessor pattern and is genuinely useful for
  future tooling (an admin/client surface showing "this world's death rules are X").
