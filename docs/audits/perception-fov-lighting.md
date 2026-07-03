# Audit: Perception, FOV & Lighting

*Audit date: 2026-07-03 · Scope: `Aetherium.Server/Perception`, `Lighting`, `PerceptionService.cs`, and the client render consumers. Findings marked **Verified** or **Suspected**.*

> **Reconciliation — `develop` @ 2026-07-03.** The grain-authoritative delta model changed the multiplayer perception story. **PARTIAL FIX (the big one):** "observers never receive perception updates" is now fixed *for grain-hosted maps* — `GameMapGrain.FanOutAsync` broadcasts deltas to the map's SignalR group, `GameSessionManager.NotifyMapMutationAsync` applies each delta to every co-located session's local mirror and recomputes that session's FOV-filtered perception, so co-located players now see each other move. Legacy (non-`JoinWorld`) single-world sessions still get no observer updates. Heat handling also improved: it's now grain-authoritative (`GameMapGrain._heatTracker`, single-threaded by activation) and replayed to sessions via `HeatRecordedDelta`, so the `HeatTrailTracker` thread-safety finding is PARTIAL-fixed for the grain path. **STILL STANDS:** infrared still renders black (heat is still collapsed to a `ThingsSeen` count and `LightLevel` is 0 for infrared), lighting modes still mutually exclusive, NPCs/monsters still not drawn on the client map, the post-hoc sunlight no-op, and the absence of a FOV rotation-invariance regression test. The FOV rotation bug remains fixed-by-design. Detail in the Reconciliation section at the end.

## Headline: the FOV rotation bug is FIXED (by architectural elimination)

The long-running FOV/rotation bug (`docs/history/FOV_BUG_SUMMARY.md` → `FOV_FIX_SUMMARY.md`) **cannot occur in the active client-server pipeline**, because there is now exactly one coordinate frame on both sides:

1. Client sends rotation to the server and performs **no local rotation** (`ClientConsoleDungeonGameNew.cs:240-256`).
2. Server updates heading only (`GameSession.RotateView`, `GameSession.cs:267-272`).
3. Server computes FOV in world coordinates; heading enters only as the directional-cone filter, and only when directional mode is on (`PerceptionService.cs:104-110`, `DirectionalFovCalculator.cs:45-111`).
4. Server emits player-relative keys with **no rotation** (`relativeX = location.X - playerLocation.X`, `PerceptionService.cs:264-286`).
5. Client looks up the **same** unrotated relative keys; the map is always drawn north-up and heading drives only the compass/HUD (`ClientConsoleMapView.cs:98-110`). Unity is equivalent (`GameManager.cs:108`).

The world-vs-rotated-key mismatch the history docs describe belongs to `Aetherium.Console/Views/ConsoleMapView.cs` — which is **dead code** (only `ClientConsoleDungeonGameNew` is wired up, `Program.cs:77`). Worse, its historical "fix" is itself still subtly wrong: it gates visibility/lighting on the *unrotated* location (`:166,198`) while drawing the *rotated* tile (`:147-161,204`), so at any heading ≠ North with asymmetric occluders the visibility mask is misaligned by the rotation angle. Dead, but should be deleted rather than left as a trap.

**Regression risk:** no test computes perception before/after a rotation and asserts the visible set is stable, and nothing renders a map at heading ≠ North. `DirectionalVisionTests` would catch a heading-vector inversion in directional mode, but if someone reintroduced client-side rotation in `ClientConsoleMapView`, no test would catch it. A rotation-invariance regression test is the single highest-value addition here.

## Findings

| Severity | Finding |
|---|---|
| High | Infrared view renders black — heat level dropped in DTO conversion |
| Medium | Other players/observers never receive perception updates when someone else acts |
| Medium | Lighting modes are mutually exclusive, not additive (a placed lantern does nothing in default Torch mode) |
| Medium | NPCs/monsters are never rendered on the client map (drawn as underlying terrain) |
| Medium | `HeatTrailTracker` is not actually thread-safe |
| Medium | Post-hoc sunlight pass is a no-op; per-action recompute has heavy allocation churn |
| Low | Several correctness/consistency issues (sunlight opacity model, diagonal-gap leak, single-Z FOV, negative-coord region binning, weather inert, duplicated perception stack) |

**[High · Verified] Infrared view renders black.** In infrared mode `PerceptionService.cs:150-159` builds an **empty** `LightFrame`, then `:264-286` sets every `VisualDto.LightLevel` from it → 0.0. `InfraredVisionSystem.CreateHeatVisual` claims to repurpose `LightLevel` for heat but actually writes heat only into `ThingsSeen[...]["heat"]`, which `MappingExtensions.cs:85-87` collapses to a count. The client paints `lightLevel <= 0.05 => Black` (`ClientConsoleMapView.cs:344-345`). Result: infrared shows a black map regardless of heat. No test renders infrared visuals (only "mode was set" assertions).

**[Medium · Verified] Observers never get perception updates.** Every push targets only the actor — `Clients.Caller.SendAsync("ReceivePerceptionUpdate", …)` at all 17 `GameHub` sites and `Clients.Client(ownConnection)` in `GameManagementGrain.cs:452-566`. `GameSessionManager.GetSessionsInWorld` (`:53`) is never called. In shared-world multiplayer, a co-located player's screen stays stale until they themselves act, and NPC movement triggers no observer update. This is the multiplayer half of the "real-time multiplayer" promise being unwired.

**[Medium · Verified] Lighting modes are mutually exclusive.** `LightingSystem.ComputeLightingWithMode` (`:41-83`): Torch ignores all world `LightSource` entities; Ambient ignores the player torch; Sunlight ignores both. In the default Torch mode a placed lantern emits nothing. (Also: the enum defines **3 lighting × 2 vision** modes, `SharedEnums.cs:32-43` — not the 5×3 matrix implied by older docs; there is no "echolocation" mode.)

**[Medium · Verified] NPCs/monsters are never drawn.** The server encodes character presence only as `ThingsSeen` counts (`VisionSystem.cs:103-116`); `ClientConsoleMapView.DrawContents` renders player, items, and terrain only (`:136-182`) and never reads `ThingsSeen`. A visible monster shows as its underlying terrain tile.

**[Medium · Verified] `HeatTrailTracker` is not thread-safe.** A `ConcurrentDictionary<WorldLocation, List<HeatTrail>>` whose mutable `List` is mutated inside the `AddOrUpdate` factory (`:49-58`) and by `CleanupOldTrails.RemoveAll` (`:103`) while `GetHeatAtLocation` enumerates un-locked (`:71`). Concurrent hub/grain access to the same session can throw "collection was modified." Memory is bounded (cleanup each `GetPerception`).

**[Medium · Verified] Wasted work per perception.** A post-hoc `ComputeSunlight` pass runs *after* the visuals are built from the light frame and is never read again (`PerceptionService.cs:420-430`), duplicating the sunlight already computed in `ComputeLightingWithMode`. More broadly, every action recomputes perception from scratch with full-entity scans (`UpdateHeatTracker` even in Normal vision, `FindLightSources`), re-serializes the full `TileTypes` dictionary each update, and allocates a fresh `SunlightCalculator` per call. The "shadow casting" FOV is actually per-target Bresenham raycasting with a shadow-region skip — correct, but not the O(cells) algorithm the name implies (~O(R³) worst case).

**Low-severity (Verified unless noted):** sunlight uses a different opacity model than FOV/torch (`BlocksLight` vs `ObstructsView`) and leaks light through walls at `stepSize=0.5` (`SunlightCalculator.cs:154-213`); diagonal-gap vision leak between two flanking walls (*Suspected*, `FovCalculator.EnumerateLine:379-416`); FOV/lighting are single-Z-plane while sunlight uniquely traces 3D Z (asymmetric); `GetRegionIdForLocation` misbins negative coordinates so weather lookups are offset (`:435-441`); weather has **zero** gameplay effect (only stringified into the DTO — any "fog reduces FOV" claim is unimplemented); infrared ignores FOV/occlusion (heat through walls — possibly intended but unspecified); `AmbientTint` ValueTuple likely doesn't survive SignalR JSON (*Suspected*; also flagged in [client-server-protocol.md](client-server-protocol.md)); the whole perception stack is **byte-duplicated** into `Aetherium.Console` and the Console `LightingSystem` copy has already diverged (lacks modes); per-frame `UI_SELFTEST_MODE` filesystem probes in the hot path.

## Verified leads (from the audit brief)

1. **Confirmed** — `FovCalculator.useShadowCasting = true` is never reassigned; `ComputeVisibleRayCasting` (`:53`) is unreachable dead code in both the Server and Console copies.
2. **Refuted (partial kernel)** — `PerceptionDto.Audio` *is* always populated (`ComputeAudioPerception`, `PerceptionService.cs:443-484`, tested). But `Sound`/`HearingFrame` (the discrete sound-event model) are never instantiated — dead code; `AudioPerceptionDto` carries only ambience metadata, no propagated sound events.
3. **Refuted as stated** — default cone is **120°** (`HasHeading.cs:27`), not 60°; validation range is **1–360**, consistently enforced at all three entry points (`SetFieldOfViewTool.cs:51`, `GameManagementGrain.cs:168`, `VisionCommands.cs:101`). `DirectionalFovCalculator` itself does no validation, but every public surface guards it.

## Strengths

- Server-authoritative, relative-coordinate perception eliminates both the rotation-bug class and a cheating vector.
- Cumulative-opacity FOV with partial transparency (forest/water/doors) matches the perception-vision spec, and the same opacity function backs torch/ambient lighting — vision and light agree on occluders.
- Heading conventions (N=0°, E=90°) are consistent across `HasHeading`, `GameSession`, `DirectionalFovCalculator`, `PerceptionService`; cone math (unit-vector dot vs `cos(fov/2)`) is correct for arbitrary headings.
- Light levels are clamped at every layer (`LightFrame.SetLightLevel`, `LightingSystem.ClampLightLevels`); multi-source addition cannot overflow (tested).
- Excellent progressive FOV suite: `FovBasicTests` Levels 1–9 plus `FovMazeTests`, `VisionTests`, `FovNegativeCoordinatesTests`, `LightingVisionIntegrationTests`.

## Spec alignment (`perception`, `perception-vision`)

Core requirements are met and well-tested: origin visible, cumulative-opacity blocking, open doors/water transparent, corner occlusion, VisionFrame composition, render-only-visible-cells, refresh on movement, and the Audio Perception Data requirement. **Drift**: the perception-vision spec's rendering requirement still names `ConsoleMapView.DrawContents()` — the dead legacy view — and says nothing about the relative-coordinate contract that now actually guarantees correctness; directional vision, lighting modes, infrared, and heat trails are implemented beyond any spec. Directional-vision FOV setting/toggling matches `game-management-grain/spec.md:74-114`, including the 1–360 error message.

## Test coverage & gaps

Strong at the FOV-geometry and lighting-propagation level (see Strengths). Gaps: **no FOV-under-rotation regression test** (the priority scenario); no infrared *visuals* test (would have caught the black-screen bug); no `HeatTrailTracker` decay/concurrency test; no sunlight-shadow test; no diagonal-gap leak test; no multi-Z FOV test; no test that a second session in a shared world receives updated perception after another actor moves; the duplicated `Aetherium.Console` perception stack is effectively untested (tests alias the Server copy).
