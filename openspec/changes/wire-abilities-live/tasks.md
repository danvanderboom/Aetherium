## Slice — Data-driven abilities, live cast path

### Data tier (Aetherium.Model.Abilities)
- [x] 1.1 `ResourceRegenPolicyKind` (Model mirror of Server's `ResourceRegenPolicy`), `ResourcePoolDefinition`
- [x] 1.2 `AbilityEffectKind` (`DealDamage`/`ApplyStatus`/`ModifyResource`), `AbilityEffectTarget` (`Caster`/`Target`), `AbilityEffectDescriptor` (Kind + per-kind fields, `RespawnLocationPolicy`-style)
- [x] 1.3 `AbilityDefinition` (id, resource cost, timing fields, range, target shape, effect descriptors, tags) — timing fields present but unconsumed this slice
- [x] 1.4 `AbilityConfig` bundle (`Abilities` + `CharacterResourcePools`), nullable everywhere it's threaded
- [x] 1.5 `AbilityResultDto` (success/reason/abilityId/target/defeated/damage), `ResourcePoolDto`/`ResourcePoolsDto` (tag/current/max) read DTOs

### Runtime tier (Aetherium.Server.Abilities)
- [x] 2.1 `AbilityCompiler(DamagePipeline, IHitResolver)`: `CompileCatalog(defs) -> AbilityCatalog` (descriptor → `DealDamageEffect`/`ApplyStatusEffect`/`ModifyResourceEffect`, binding pipeline/resolver), `BuildResourcePools(defs) -> ResourcePools` (fresh instance; maps `ResourceRegenPolicyKind` → `ResourceRegenPolicy`)
- [x] 2.2 `AbilityCooldowns : Component` (`Dictionary<string,int>`, `SetCooldown`/`IsOnCooldown`/`Tick`/`Snapshot`)
- [x] 2.3 `ResourcePools` gains `All` pool enumeration (for per-tick regen) — additive accessor on the `add-abilities` primitive

### Per-world threading
- [x] 3.1 `AbilityConfig?` added to `WorldConfig`, `WorldTemplate`, `CreateWorldRequest`; mapped in `GameManagementGrain.CreateWorldAsync` (both `IWorldHost` and fallback paths) and `OrleansWorldHost.CreateWorldAsync`
- [x] 3.2 `WorldGrainState.AbilityConfig` (set in `WorldGrain.InitializeAsync`); `AddMapAsync` passes it to `IGameMapGrain.InitializeAsync`'s new optional `abilityConfig` param
- [x] 3.3 Persisted on `MapState [Id(12)] AbilityConfig?`; `GameMapGrain.InitializeAsync` compiles `_abilityCatalog` + stores pool defs + persists; `OnActivateAsync` rehydrates + recompiles (via shared `ApplyAbilityConfig`)
- [x] 3.4 `Character` constructor gains default `ActionSpeed(1.0, 1.0)`; `JoinPlayerAsync` stamps `AbilityCompiler.BuildResourcePools(configuredPools)` onto the joining character when the world declares any

### Live cast path + observability (GameMapGrain)
- [x] 4.1 `UseAbilityAsync(sessionId, abilityId, targetEntityId?)`: `IsActionable` → catalog lookup → cooldown gate → resource `CanAfford` gate → target resolution + reach gate (when targeted) → `ActionSpeed.TrySpend(AbilityActionCost)` commit → resource `TrySpend` commit → snapshot target Health/Dying → apply effects in order → set cooldown → derive+fan-out deltas (Health change; shared `SpawnMonsterLoot` monster-defeat helper with `AttackAsync`) → return `AbilityResultDto`
- [x] 4.2 `TickAsync`: tick every `AbilityCooldowns` down; regen every `ResourcePools` (`ThreatTable` presence = in-combat)
- [x] 4.3 `GetResourcePoolsAsync(sessionId)`/`GetAbilityCooldownsAsync(sessionId)` accessors on `IGameMapGrain`

### Tests + spec
- [x] 5.1 `AbilityCompiler` unit tests (7): each descriptor kind compiles to the right effect; timing/cost carry-through; pool defs build a working `ResourcePools`; fresh-instance-per-call; regen-policy mapping; null → empty catalog
- [x] 5.2 Grain integration tests (12): damaging cast reduces target Health via `DamagePipeline` (and defeats/loots a monster consistently with melee); resource-modify cast changes caster pool; rejections for downed, on-cooldown, unaffordable resource, out-of-reach, unknown ability; no-config → all abilities unknown + no pools; cast puts ability on cooldown; cooldown ticks down over `TickAsync`; resource pool regenerates over ticks
- [x] 5.3 Per-world threading tests (2): a world's `AbilityConfig` reaches every map it creates (initial + later `AddMapAsync`); a `CreateWorldRequest.AbilityConfig` reaches the created map. (Reactivation is covered by the same `ApplyAbilityConfig`-from-`MapState` mechanism as `DeathPolicy`; not given a dedicated test, matching the wire-death-respawn-live precedent.)
- [x] 5.4 `specs/abilities/spec.md` delta: ADDED "Per-World Ability Config", "Live Ability Cast Path", "Ability Resource & Cooldown Gating", "Ability Tick Upkeep" + `**Verified by:**` lines
- [x] 5.5 Full build + regression suite green (1221 passed, 1 pre-existing skip, 0 failed)

## Later slices (scoped, not built here)

- [ ] L.1 Phased charge/cast/recover execution: a `CastInProgress` component + tick system consuming the timing fields, interruptible; per-ability AP cost (needs `add-continuous-action-pipeline` Phase 2's player-side `ActionQueue` wiring)
- [ ] L.2 NPC/monster ability use (behavior-tree `UseAbility` leaf)
- [ ] L.3 AOE/shape targeting (`TargetShape` becomes more than a renderer tag)
- [ ] L.4 `SkillDefinition.UnlocksAbilityId` → catalog grant (bridges to character progression)
- [ ] L.5 `Teleport`/`Spawn`/`Summon`/`TriggerNarrativeEvent` effect kinds
- [ ] L.6 Client-facing cast/cooldown/resource push signal (mirrors `PlayerVitalsDto`/`ReceiveDowned`); this slice exposes state via read accessors only
- [ ] L.7 YAML/content-pack pipeline populates `AbilityConfig` (no downstream change — the data tier is already the seam)
