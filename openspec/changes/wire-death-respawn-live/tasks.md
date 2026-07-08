## Slice A — Per-world DeathPolicy plumbing (foundational, zero behavior change)

- [x] A.1 `DeathPolicy` (+ `DropOnDeathPolicy`/`XpLossPolicy`/`RespawnLocationMode`/`RespawnLocationPolicy`/`PermadeathSessionPolicy`) made `[GenerateSerializer]` and **moved from `Aetherium.Server.Combat` to `Aetherium.Model.Combat`** (needed to be reachable from both `WorldConfig` (Server) and `WorldTemplate` (Model), which sit on opposite sides of the Server→Model reference direction — mirrors the ContentAtlas data/seeding split). Also enriched per the session's design discussion: `RespawnPointPolicy` (a bare 3-value enum) replaced by `RespawnLocationPolicy` (`Mode` + coordinates/offset/tag, covering `DeathLocation`/`EntryLocation`/`WorldSpawn`/`NamedLocation`/`FixedCoordinates`/`OffsetFromCoordinates`/`OffsetFromNamedLocation`/`LastSafeLocation`/`PartyLeader`); added `RespawnInvulnerabilityTicks` and `PermadeathBehavior` (`Spectate`/`Disconnect`) fields — resolving OQ1–OQ3 as **configuration**, not hardcoded engine choices. `CorpseExpirySystem`/`CorpseAge`/`GameMapGrain` updated to reference the new namespace.
- [x] A.2 `DeathPolicy?` added to `WorldConfig`, `WorldTemplate`, and `CreateWorldRequest`; mapped in `GameManagementGrain.CreateWorldAsync` on both the `IWorldHost` ("new system") and fallback paths, and in `OrleansWorldHost.CreateWorldAsync`'s template→config conversion
- [x] A.3 Threaded to every map a world creates: `WorldGrainState` now persists the world's `DeathPolicy` (set once in `WorldGrain.InitializeAsync`); `WorldGrain.AddMapAsync` reads its own grain state and passes it to `IGameMapGrain.InitializeAsync`'s new optional `deathPolicy` parameter — no signature change needed on `AddMapAsync` itself, and all 13 existing test call sites of the 5-arg `InitializeAsync` compile unchanged since the new parameter defaults to `null`
- [x] A.4 Persisted on `MapState [Id(11)] public DeathPolicy? DeathPolicy`
- [x] A.5 `GameMapGrain._deathPolicy` is no longer a hardcoded `readonly` field — set from `InitializeAsync`'s `deathPolicy` argument (`?? DeathPolicy.Default`), rehydrated in `OnActivateAsync` from `_mapState.State.DeathPolicy` on reactivation
- [x] A.6 New `IGameMapGrain.GetDeathPolicyAsync()` read accessor (mirrors the existing `GetCombatStatsAsync()` pattern — genuinely useful for future tooling, not test-only scaffolding) makes the active policy observable; tests: a world's custom policy reaches both its initial and a later-added map (`WorldGrainTests`), a `CreateWorldRequest`-supplied policy reaches the created map (`GameManagementGrainTests`), an unconfigured world falls back to `Default`
- [x] A.7 OpenSpec spec delta (MODIFIED "Death Policy Schema" for the enriched fields; ADDED "Per-World Death Policy") + `**Verified by:**` lines
- [ ] A.8 Full regression suite green (Default preserves all current behavior)

## Slice B — Configurable player death loop + client surface

- [ ] B.1 `Downed` component (player down-state with its own tick countdown), distinct from monster `Dying`
- [ ] B.2 A player-death system: on a lethal monster→player hit, apply the policy outcome — instant respawn / enter `Downed` / → `Corpse` (permadeath) per `Permadeath × DownStateEnabled`
- [ ] B.3 Route `StepNpcsAsync`'s monster→player retaliation so a lethal hit triggers B.2 (rather than the current inert 0-HP)
- [ ] B.4 Respawn routine implementing `RespawnLocationPolicy`: `DeathLocation`/`EntryLocation`/`WorldSpawn`/`FixedCoordinates`/`OffsetFromCoordinates` resolve directly; `NamedLocation`/`OffsetFromNamedLocation` fall back to `WorldSpawn` until a location-tag registry exists (L.6); reset Health to max, clear `Downed`, **reuse the same entity id**, reconcile `_spawnsInUse`/`_playerSpawns`, emit `EntityMovedDelta` + Health delta, apply `RespawnInvulnerabilityTicks`
- [ ] B.5 `Downed` gating on every player command (`Move/Attack/Rotate/ChangeLevel/Pickup/Drop/Use/Open/Close`) via one `IsActionable` guard
- [ ] B.6 Permadeath entity outcome (→ `Corpse`); session behavior per `PermadeathBehavior` — `Spectate` (session stays connected, read-only) is the achievable slice; `Disconnect` UX (actually dropping the connection cleanly) may need hub-layer work, see L.3
- [ ] B.7 Client surface: `PlayerVitalsDto` (own Health/max/downed state) + hub signals `ReceiveDowned`/`ReceiveRespawn`/`ReceiveDied`; emit from the death/respawn transitions
- [ ] B.8 Tests: each of the four outcome models end-to-end at the grain level (instant-respawn, down-then-respawn, instant-permadeath→Corpse, down-then-permadeath); a downed player's commands are rejected; respawn reuses the entity id and resets HP/location per the configured `RespawnLocationPolicy` mode; invulnerability window holds; the client-facing signals fire
- [ ] B.9 Extend `Tick_MonsterAdjacentToPlayer_Retaliates...` for the new 0-HP path (see design compatibility note)
- [ ] B.10 OpenSpec spec delta (ADDED: "Player Death Outcomes", "Downed Action Gating", "Player Vitals Wire Surface") + `**Verified by:**` lines
- [ ] B.11 Full regression suite green

## Later slices (scoped, not built here)

- [ ] L.1 Revive mechanic: a teammate command that interrupts the `Downed` window (needs proximity/party)
- [ ] L.2 `RespawnLocationMode.PartyLeader` resolution (needs a party system)
- [ ] L.3 `PermadeathSessionPolicy.Disconnect` — actually force-disconnecting a session cleanly (hub-layer work); `Spectate`'s real UX beyond "keep receiving perception updates"
- [ ] L.4 A named/tagged world-location registry (confirmed via research: nothing like it exists anywhere in the engine today — closest prior art is `HybridAnchor.Tags`, a design-time-only PCG constraint, not a runtime lookup) — needed to resolve `RespawnLocationMode.NamedLocation`/`OffsetFromNamedLocation` and `LastSafeLocation` beyond their `WorldSpawn` fallback
- [ ] L.5 `DropOnDeath` (needs live loot/inventory-drop) and `XpLossPolicy` (needs live progression pools) consumption
- [ ] L.6 When a YAML game-asset pipeline exists, populate `WorldConfig.DeathPolicy` from it (no downstream change)
