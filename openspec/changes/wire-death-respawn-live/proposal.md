## Why

Player death currently has **zero consequence** in the live engine: a monster's retaliation can drive
a player's `Health` to 0, but nothing anywhere checks a player's health — a downed player keeps
moving, attacking, and interacting exactly as if unhurt (research confirmed: no respawn/revive/downed/
game-over code exists anywhere in server, client, or DTOs). Meanwhile the fourth Phase-2 spine slice
(`wire-combat-pipeline-live`) gave *monsters* a real death lifecycle (Dying → Corpse, persists,
lootable), leaving a stark asymmetry: monsters die meaningfully, players cannot die at all.

This change closes that gap. Because Aetherium is a game **engine**, not a single game, death behavior
MUST be **policy-driven and per-world configurable** — every game built on the engine picks its own
model (permadeath, auto-respawn, revive-centric co-op, instant respawn) as data, not as a hardcoded
engine choice. The `DeathPolicy` schema for exactly this already shipped (`add-death-respawn-policy`,
Wave 1) but is consumed today only as a hardcoded `DeathPolicy.Default` constant.

## Key finding: all four death models are already expressible, with no schema change

The existing `DeathPolicy` fields (`Permadeath`, `DownStateEnabled`, `ReviveWindowTicks`, `RespawnPoint`)
cover every model as pure configuration:

| Model | `Permadeath` | `DownStateEnabled` | Notes |
|---|---|---|---|
| Instant respawn | false | false | 0 HP → immediately respawn at full HP |
| Auto-respawn after down timer | false | true | 0 HP → downed for `ReviveWindowTicks`, then respawn |
| Instant permadeath | true | false | 0 HP → corpse, session ends |
| Down-then-permadeath | true | true | 0 HP → downed for `ReviveWindowTicks`, then corpse |

Revive-centric co-op is any `DownStateEnabled = true` config **plus the revive mechanic** (a teammate
command that interrupts the down window) — a later slice, orthogonal to the outcome models above. So
this work is **plumbing + wiring + client surface**, not schema design.

## What changes (decomposed into slices)

- **Slice A — Per-world `DeathPolicy` plumbing (foundational, zero behavior change).** Make
  `DeathPolicy` Orleans-serializable; add it to `WorldConfig`/`CreateWorldRequest`; thread it through
  `WorldGrain.AddMapAsync` → `GameMapGrain.InitializeAsync`; persist it on `MapState`; source
  `GameMapGrain._deathPolicy` from state (falling back to `DeathPolicy.Default` when unspecified, so
  every existing world behaves byte-for-byte as today). This bootstraps the engine's first
  rules-as-data channel; a future YAML asset pipeline would populate `WorldConfig.DeathPolicy`
  upstream with no downstream change.

- **Slice B — Configurable player death loop + client surface.** Route monster→player lethal hits into
  the policy-driven outcome (instant-respawn / downed-then-respawn / permadeath); add a player `Downed`
  component and gate every player command while downed; respawn in place (reuse the same entity id —
  see Design — reset HP, teleport per `RespawnPoint`); and surface it to the client (per the user's
  decision to include the client surface now): player Health on the wire plus a typed "you are downed
  / respawned / died" signal so a client can render a death screen and respawn timer.

- **Later slices (scoped here, not built yet):** revive mechanic (teammate interrupts the down window);
  `RespawnPoint = LastSafeLocation`/`PartyLeader` (need last-safe tracking / party system);
  permadeath spectator/session-termination UX; `DropOnDeath`/`XpLossPolicy` consumption (need live
  loot-drop and live progression pools — both still Phase-1 schema only).

## Impact

- **Slice A:** `DeathPolicy.cs` (serialization attrs), `WorldModels.cs` (`WorldConfig` field),
  `CreateWorldRequest`/`GameManagementGrain.CreateWorldAsync`, `IGameMapGrain.InitializeAsync`
  signature, `MapState` (new `[Id(11)]` field), `GameMapGrain` (source `_deathPolicy` from state).
- **Slice B:** `GameMapGrain` (`StepNpcsAsync` retaliation → policy outcome, new `Downed` gating on
  every command path, a respawn routine), a new `Downed` component + a `PlayerDeathSystem`/respawn
  system, `PerceptionDto` or a new `PlayerVitalsDto` + a client-facing death/respawn signal.
- **Contract change (Slice B):** first time player Health and a death/respawn event cross the wire —
  a genuinely new client-facing surface, not a reroute.
- `LocalMutationGateway` (legacy path) stays untouched, consistent with every prior slice.
