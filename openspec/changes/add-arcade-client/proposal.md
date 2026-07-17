## Why

`add-game-definition-loader` made a *game* pure data — a YAML bundle under `Data/Games/` — and gave
operators an admin path to list definitions and spin up instances (`GET/POST /api/management/games`,
API-key-gated). But there is **no player-facing way to discover or enter those games.** A connecting
SignalR client lands in a private `FovDiagnosticWorldBuilder("open_space")` session and can only
`ListWorlds`/`JoinWorld` on instances an operator already created. To play `aphelion` today you POST an
instance over the management API and paste the returned world id into a client field. That is the
faucet with no handle a *player* can reach.

This change adds the handle: a **player-facing arcade surface** on `GameHub`. The client connects,
asks the server what games it offers, picks one, and is dropped into a running instance — creating one
on demand when the game's bundle permits it. The result is an arcade whose catalog is exactly the set
of YAML bundles on the server: drop a new bundle in `Data/Games/` and it appears on the shelf, playable,
with no client or engine release. This is engine gap **G6** (`docs/design/unity-sample/engine-gaps.md`)
and the A0 milestone of the arcade-client design (`docs/design/arcade-client/README.md`).

It also ships **Nightjar** (`Data/Games/nightjar/`), a hexagonal cat-burglar heist, as the arcade's
curated-hex flagship — chosen over bending Aphelion (which stays square) precisely to demonstrate the
thesis: a whole new game, on a different tile topology, is a bundle plus a theme.

## What Changes

- **Player-facing catalog on `GameHub`** (adaptive-auth hub, Player tool profile unaffected):
  - `ListGames()` → `List<GameDefinitionSummaryDto>` — the registry's definitions, player-visible.
  - `GetGameDetails(gameId)` → `GameShelfEntryDto` (summary + live occupancy: running instances,
    joinable instances, players in-game), sourced from the existing `ListGameInstancesAsync` filter.
  - `GameDefinitionSummaryDto` gains additive fields for the shelf: `Topology`, `MaxPlayers`,
    `Presentation` (open string dict for cosmetic hints, same shape as `TileTypeDto.Settings`).
- **One-call entry: `GameHub.PlayGame(gameId, PlayGameOptionsDto?)` → `JoinWorldResult`.** Resolves
  *join-or-create* through `GameManagementGrain`: reuse an `Active` instance of the game with player
  headroom, else create one (via the existing `CreateGameInstanceAsync` mapper path) and join it, else
  fail with a human-readable reason — then run the **existing** `JoinWorld` grain-binding flow.
- **Per-bundle instancing policy — data, not code.** A new optional `instancing:` section in the game
  bundle (`playerEntry: shared|private|disabled`, `allowPlayerInstances`, `maxInstances`,
  `idleShutdownMinutes`) governs whether/how `PlayGame` may create instances, honoring the
  per-world-data principle. Validated by `GameDefinitionValidator` (enum values, non-negative numbers,
  `allowPlayerInstances` required when `playerEntry: private`).
- **Idle instance reaping.** Game-instance worlds created on demand must be reclaimable: track
  last-player-activity, and a periodic sweep shuts down instances with zero joined sessions past
  `idleShutdownMinutes` through the existing `ShutdownWorldAsync` path (mirrors the
  `DungeonInstanceGrain` idle-deactivation shape). Persisted-state worlds survive; `PlayGame` recreates.
- **Server capability handshake: `GameHub.GetServerInfo()` → `ServerInfoDto`** (`ServerVersion`,
  `ProtocolCapabilities: string[]`). The wire has no protocol version; a generalized client that meets
  many servers feature-gates on capability strings (e.g. hides the vitals HUD when `"interoception"`
  is absent) rather than version arithmetic. Append-only forever.
- **Nightjar bundle** (`Data/Games/nightjar/game.yaml`): `topology: hex` on `hex-caves`, a five-rung
  guard ladder (`housecat`→`nightwatch`→`guard-hound`→`warden`→`clockwork-sentry`), heist kit
  (blackjack/nerve-tonic/lockpick/keys/the sapphire), co-op down/respawn-at-safehouse death with
  partial drop. Pure data — no engine or client code.

Client-side arcade work (shelf UI, three-tier theming, hex rendering/anchoring, discovery-driven
input/HUD) and the hex **rotation-preset** and **client anchor-advancement** fixes are the A1/A2
milestones in `docs/design/arcade-client/README.md`; they are **out of scope here** except where this
change's tests must drive them (the live hex-transit validation, below). This change is the A0
server+protocol surface.

## Impact

- **Affected specs:**
  - `client-server-communication` — ADDED: Player-Facing Game Catalog; One-Call Game Entry;
    Server Capability Handshake.
  - `game-management-grain` — ADDED: Player-Initiated Instance Lifecycle (join-or-create + policy);
    Idle Instance Reaping.
  - (`game-definitions` — the `instancing:` schema + validation extends this capability; its spec
    lands when `add-game-definition-loader` archives. Noted here; captured in tasks.)
- **Affected code:** `Aetherium.Server/GameHub.cs` (`ListGames`/`GetGameDetails`/`PlayGame`/
  `GetServerInfo`); `Aetherium.Server/Management/GameManagementGrain.cs` + `IGameManagementGrain.cs`
  (join-or-create resolution, reaping sweep + activation timer, last-activity tracking);
  `Aetherium.Model/Games/GameDefinition.cs` (additive summary fields; `InstancingPolicy` +
  `GameShelfEntryDto`/`PlayGameOptionsDto`/`ServerInfoDto` DTOs); `GameDefinitionValidator.cs`
  (instancing rules); `Aetherium.Server/MultiWorld/WorldGrain.cs` (activity timestamp on join/leave);
  `Aetherium.Client/LobbyClient.cs` (+ mirror DTOs, drift-test entries) for the four new methods;
  `Data/Games/nightjar/`.
- **New tests:** `Aetherium.Test/Games/ArcadeCatalogTests.cs`,
  `Aetherium.Test/Games/PlayGameLifecycleTests.cs`, `Aetherium.Test/Games/InstanceReapingTests.cs`,
  `Aetherium.Test/Games/GameDefinitionValidatorTests.cs` (instancing cases),
  `Aetherium.Test/Games/GameDefinitionRegistryTests.cs` (nightjar canary: hex + content);
  `Aetherium.Client.Tests/InProcServerIntegrationTests.cs` (extend: `PlayGame` round-trip and the
  **first live hex transit** on a `hexhaven`/`nightjar` instance); `Aetherium.Client.Tests/ProtocolDriftTests.cs`
  (new DTOs).
- **Non-breaking:** all DTO fields additive/nullable; `instancing:` optional (absent → `shared`,
  `allowPlayerInstances: false`, i.e. player entry reuses operator-created instances only, never
  creates); existing `JoinWorld`/management paths and every current bundle behave exactly as today.

## Status

Proposed — not yet implemented. Design: `docs/design/arcade-client/README.md` (A0 scope). The Nightjar
bundle is authored and expected to validate against the shipped loader (`hex-caves` + `hex` is a
proven-supported combination via `hexhaven`); everything else in this change is unbuilt.
