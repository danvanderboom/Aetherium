## Slice 1 — parameters drive live generation + difficulty introspection (this pass)

### 1. Parameter accessors
- [x] 1.1 Add typed param accessors to `GeneratorContext` (`HasParam`/`GetIntParam`/`GetDoubleParam`, parse + clamp) reading `GeneratorParams`

### 2. AdvancedDungeonGenerator honors params
- [x] 2.1 Room-count range from `minRooms`/`maxRooms` (defaults 6/9 preserve `rng.Next(6, 10)`)
- [x] 2.3 Trap count from `trapDensity` scaled by eligible-room count (default = single boss-room trap; trap placement draws no RNG)
- [x] 2.5 Verify default (no-param) path preserves exact output + RNG draw order (existing `DeterminismTests` green)

### 3. Population pass honors params
- [x] 3.1 `DungeonPopulationPass` monster count from `enemyCount` (exact); treasure from `resourceAvailability` (baseline pair scaled)

### 4. Difficulty introspection
- [x] 4.1 Invoke `GenerationMetrics.CalculateDifficultyProfile` in the orchestrator from effective params + measured layout metrics (best-effort)
- [x] 4.2 Expose the computed `DifficultyProfile` (+ `PredictedAgentSuccessRate`) on `GenerationMetrics`

### 5. Tests
- [x] 5.1 Parameter-effect tests: `maxRooms` → more rooms; `trapDensity` → more traps; `enemyCount` → monster count; `resourceAvailability` → more treasure
- [x] 5.2 Difficulty-profile computed with and without params
- [x] 5.3 Determinism preserved: existing `DeterminismTests` green; same seed+params → same world
- [x] 5.4 Server + Test build green; broader worldgen/training/benchmark suite green (141 tests, 0 regressions)

## Slice 2 — deeper parameterization + dead-stub resolution (follow-up, not this pass)
- [ ] 2.2 Key/lock chain depth from `keyLockChainDepth` (generalize the single gated pair to N pairs while preserving the access-proof invariant)
- [ ] 2.4 Secret-room count from `secretRoomDensity`
- [ ] 3.2 `OutdoorPopulationPass` honors `enemyCount`/`resourceAvailability`
- [ ] 6.1 Implement `PrefabStamper.SpawnEntity` (prefab entity stamping via a shared entity factory)
- [ ] 6.2 Wire in or delete `SpawnNPCsFeature` / `ItemDistributionFeature` / `AdaptationPass` per the honest-stub rule
