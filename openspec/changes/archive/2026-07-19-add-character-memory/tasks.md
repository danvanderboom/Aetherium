## 1. Engine
- [x] 1.1 Fix `Memory.AddMemory` copy-mutation bug (new memories were appended to a discarded `.ToList()` copy)
- [x] 1.2 Add `MemoryPolicy` (Enabled=true, MaxLocations=10000, DecayHalfLifeSeconds=3600) with pure `EffectiveStrength` helper; add `World.MemoryPolicy`
- [x] 1.3 Thread policy overrides from world `GeneratorParameters` (`MemoryEnabled`, `MemoryMaxLocations`, `MemoryDecayHalfLifeSeconds`) in `GameMapGrain.InitializeAsync`
- [x] 1.4 Record memories in `GameSession.GetPerception`: visible terrain + non-terrain entities at absolute locations; enforce location cap oldest-first

## 2. Server API
- [x] 2.1 Add `CharacterMemoryDto` (+ entry DTO) in `Aetherium.Model`
- [x] 2.2 Add `IGameManagementGrain.GetMemoryAsync(sessionId)` → JSON, operator-gated; null for unknown session

## 3. CLI
- [x] 3.1 Add `aetherctl memory get <sessionId> [--json]`; register in `Program.cs`; non-zero exit on missing session/gate

## 4. Tests (linked to spec requirements)
- [x] 4.1 Unit: `Memory.Remember` stores new memories (bug-fix regression) and bumps `Impressions` on identical re-record
- [x] 4.2 Unit: `EffectiveStrength` halves per half-life; no decay when halfLife<=0
- [x] 4.3 Server: after driving a headless session, memory contains visible terrain + entity memories at locations corroborated by the world snapshot
- [x] 4.4 Server: repeated perception bumps impressions
- [x] 4.5 Server: world created with `MemoryEnabled=false` records nothing
- [x] 4.6 Server: operator gate denies `GetMemoryAsync` when disabled
- [x] 4.7 CLI: structural coverage for `memory get`

## 5. Docs
- [x] 5.1 Update `docs/agents/README.md` + `TOOL_SYSTEM_STATUS.md`
