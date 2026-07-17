## 1. Game bundle: Nightjar (data only — shipped with this proposal)

- [x] 1.1 `Data/Games/nightjar/game.yaml`: `topology: hex` on `hex-caves`; guard ladder + heist kit +
      co-op down/respawn death (validated shape mirrors `hexhaven` + `aphelion`)
- [ ] 1.2 `GameDefinitionRegistryTests`: nightjar loads → topology `hex`, five creatures, six items,
      spawn table sums; validator reports zero errors

## 2. Bundle schema: instancing policy (game-definitions)

- [ ] 2.1 `InstancingPolicy` POCO (`Aetherium.Model/Games`): `playerEntry` (enum shared|private|disabled,
      default shared), `allowPlayerInstances` (bool, default false), `maxInstances` (int, default 1),
      `idleShutdownMinutes` (int, default 30); optional `Instancing` section on `GameDefinition`
- [ ] 2.2 Loader binds the `instancing:` section (inline or `instancing.yaml` sibling), strict keys
- [ ] 2.3 `GameDefinitionValidator`: enum membership, non-negative `maxInstances`/`idleShutdownMinutes`,
      `allowPlayerInstances: true` required when `playerEntry: private`
- [ ] 2.4 `GameDefinitionValidatorTests`: each rule (valid + each rejection)

## 3. Player-facing catalog on GameHub

- [ ] 3.1 Extend `GameDefinitionSummaryDto` (additive): `Topology`, `MaxPlayers`, `Presentation` dict
- [ ] 3.2 `GameShelfEntryDto` (summary + `RunningInstances`/`JoinableInstances`/`PlayersInGame`)
- [ ] 3.3 `GameHub.ListGames()` → summaries (registry, player-visible, no API key)
- [ ] 3.4 `GameHub.GetGameDetails(gameId)` → shelf entry, occupancy from `ListGameInstancesAsync`
- [ ] 3.5 `ArcadeCatalogTests`: list returns all registered games; details report live occupancy

## 4. One-call entry: PlayGame

- [ ] 4.1 `PlayGameOptionsDto` (`PreferPrivate`, `InstanceName?`)
- [ ] 4.2 `GameManagementGrain.ResolvePlayGameAsync(gameId, options)`: join-or-create per policy —
      reuse `Active` instance with headroom (`joined < MaxPlayers`); else create up to `maxInstances`
      if `allowPlayerInstances`; else fail. `disabled` never reuses/creates for players.
- [ ] 4.3 `GameHub.PlayGame(gameId, options?)` → calls resolver, then the existing `JoinWorld` flow on
      the resolved world/map; returns `JoinWorldResult`
- [ ] 4.4 `PlayGameLifecycleTests`: shared-reuse-below-capacity; shared-at-capacity-creates-#2-up-to-
      max-then-full; private-always-creates; disabled-refuses; `PreferPrivate` forces create

## 5. Idle instance reaping

- [ ] 5.1 Track `LastPlayerActivityAt` on the world/map (update on join/leave/tool execution)
- [ ] 5.2 `GameManagementGrain.SweepIdleInstancesAsync()`: shut down game-instance worlds with zero
      joined sessions past the game's `idleShutdownMinutes` (0 = never) via `ShutdownWorldAsync`;
      return count. Register a periodic activation timer to drive it.
- [ ] 5.3 `InstanceReapingTests`: idle instance reaped + stops ticking; active/occupied instance
      untouched; `idleShutdownMinutes: 0` never reaped; reaped instance recreatable via `PlayGame`

## 6. Server capability handshake

- [ ] 6.1 `ServerInfoDto` (`ServerVersion`, `ProtocolCapabilities: List<string>`)
- [ ] 6.2 `GameHub.GetServerInfo()` — capability strings assembled from build-time feature set
      (e.g. `"topology"`, `"interoception"`, `"arcade"`)
- [ ] 6.3 Test: handshake advertises the expected capability set

## 7. Client library (Aetherium.Client) wrappers

- [ ] 7.1 `LobbyClient`: `ListGamesAsync`/`GetGameDetailsAsync`/`PlayGameAsync`/`GetServerInfoAsync`
- [ ] 7.2 Mirror DTOs in `Contracts/` for the new/extended shapes; add to `ProtocolDriftTests`
- [ ] 7.3 `InProcServerIntegrationTests`: `PlayGame` join-or-create round-trip against a live server

## 8. First live hex transit (the "needs to be tested" gate)

- [ ] 8.1 `InProcServerIntegrationTests`: `PlayGame("hexhaven")` (and `"nightjar"`) → assert live frame
      `Topology == "hex"`; walk edges via rotate+forward; assert perception keys/FOV shape hold
      through the full grain→SignalR→client path (first time hex crosses the live stack)
- [ ] 8.2 Note in the test any anchor/rotation gaps surfaced — those are the A1 fixes
      (`ToolClient.AnchoringIsExact`, `RotateTool` `TurnStepDegrees`), tracked in the design doc, not
      fixed here

## 9. Traceability & close-out

- [ ] 9.1 Cross-link every requirement in `specs/**/spec.md` with a `**Verified by:**` line naming its
      test (per `openspec/project.md`)
- [ ] 9.2 Full solution build + suite green
- [ ] 9.3 Manual acceptance: arcade lists six games; a square game (Aphelion/Emberfall) and a hex game
      (hexhaven/Nightjar) each entered via `PlayGame` and played to death/respawn

## Deferred (A1/A2 — design doc, not this change)

- Client arcade app: shelf UI, server picker, three-tier theming (`ThemeCatalog` + procedural Tier 0),
  per-topology cell footprints.
- Hex feel fixes: client hex/tri anchor advancement + drift tests; server `TurnStepDegrees` rotate
  presets. (Nightjar is *playable* on hex without them; it *feels right* with them.)
- Discovery-driven input, generic action/option UI (port from legacy `Aetherium.Unity`), interoception
  HUD.
- Nightjar polish: bespoke hexagonal-gallery generator (replacing `hex-caves`); `patrol`/alarm
  behavior preset (replacing `wander-melee`).
