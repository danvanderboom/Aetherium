## 1. Engine model
- [x] 1.1 Extend `MemoryPolicy` with dynamics fields (`DynamicsEnabled=false`, `StabilityGrowthFactor=2.0`, `MinReinforcementIntervalSeconds=60`, `PermanenceThresholdSeconds=2592000`, `ForgetThreshold=0.05`) and pure static helpers: `EffectiveStrength(strength, age, stabilitySeconds, permanent, fallbackHalfLife)` overload + `ReinforceStability(current, baseHalfLife, growthFactor)`; `MemoryDynamics` resolved struct + `ResolveDynamics(scalars)`
- [x] 1.2 Add `SpaceTimeMemory.StabilitySeconds` (default 0 ⇒ policy fallback) and `SpaceTimeMemory.Permanent`
- [x] 1.3 Add `MemoryProfile` component (`HalfLifeMultiplier=1.0`, `StabilityGrowthMultiplier=1.0`, `MaxLocationsOverride=null`)
- [x] 1.4 Thread generator parameters `MemoryDynamicsEnabled`, `MemoryStabilityGrowthFactor`, `MemoryMinReinforcementIntervalSeconds`, `MemoryPermanenceThresholdSeconds`, `MemoryForgetThreshold` in `GameMapGrain.ApplyMemoryPolicy`

## 2. Recording path
- [x] 2.1 In the perception-time recording path (`GameSession.RecordMemories` → `Memory`): when dynamics enabled and a re-encounter is spaced (`age ≥ MinReinforcementIntervalSeconds`), grow stability (initialize from effective base half-life on first growth), refresh strength to 1.0; massed re-encounters keep today's impressions/last-seen behavior only
- [x] 2.2 Latch `Permanent` when stability ≥ threshold; permanent memories skip decay and culls
- [x] 2.3 Cull sub-threshold entries at touched locations during recording; extend the `MaxLocations` cap sweep to drop all-forgotten locations first, oldest-first pruning second
- [x] 2.4 Honor `MemoryProfile` (half-life multiplier in effective-strength reads, growth multiplier on reinforcement, location-cap override)

## 3. Read surface
- [x] 3.1 Add `StabilitySeconds`/`Permanent` to `MemoryEntryDto`; compute `EffectiveStrength` via per-memory stability + profile multiplier in `GameManagementGrain.GetMemoryAsync`
- [x] 3.2 Display new fields in `aetherctl memory get` table output (JSON output picks them up automatically)

## 4. Tests (linked to spec requirements)
- [x] 4.1 Unit: spaced re-encounter multiplies stability and refreshes strength; massed re-encounter (< interval) does not grow stability but still bumps impressions — Memory Stability and Reinforcement
- [x] 4.2 Unit: effective strength uses per-memory stability when set, world half-life × profile multiplier when 0 — Memory Stability and Reinforcement
- [x] 4.3 Unit: stability crossing the threshold latches `Permanent`; permanent entries return full stored strength at any age — Memory Permanence Through Familiarity
- [x] 4.4 Unit: write-time cull removes sub-threshold entries at a touched location; `ForgetThreshold=0` disables culling; permanent entries are never culled — Forgetting Weak Memories
- [x] 4.5 Unit: `MemoryProfile.HalfLifeMultiplier` scales decay (forgetful faster, sharp slower); growth multiplier scales reinforcement — Per-Character Memory Profiles
- [x] 4.6 Server: world with `MemoryDynamicsEnabled=false` (and default) reproduces exact legacy decay (no stability writes, no culls) — Memory Dynamics Opt-In
- [x] 4.7 Server: headless session driven over the same route twice with spacing shows grown stability via `GetMemoryAsync`; DTO carries new fields — Memory Stability and Reinforcement
- [x] 4.8 CLI: structural coverage for new `memory get` fields (`FormatDurability` branches)

## 5. Docs
- [x] 5.1 Update `docs/agents/README.md` memory section with the dynamics model and parameters
