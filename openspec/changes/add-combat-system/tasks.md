## Slice 1 — attack → damage → death + kill-quest wiring (this pass)

### 1. Health
- [x] 1.1 `Character` defaults to `Health(100,100)`; `Monster` overrides to `Health(30,30)`

### 2. Combat core
- [x] 2.1 New pure `CombatSystem.TryAttack(world, attacker, targetId)` → `CombatResult` (reach + self + has-Health checks, all guarded by `Has<T>()` since `Get<T>()` throws on missing; fixed damage; remove on death)
- [x] 2.2 New `AttackResultDto` (Aetherium.Model): success, reason, damage, remainingHealth, targetDefeated, targetType, targetId

### 3. Mutation path
- [x] 3.1 `IMapMutationGateway.AttackAsync(targetEntityId)` + `LocalMutationGateway` (session.World/Player under state lock) + `GrainMutationGateway` (→ grain)
- [x] 3.2 `IGameMapGrain.AttackAsync(sessionId, targetId)` + `GameMapGrain` impl: apply hit; fan out `ComponentFieldChangedDelta` (Health) or `EntityRemovedDelta` (death); persist

### 4. Surface + wiring
- [x] 4.1 `attack` agent tool (category `combat`); add `combat` to the Player profile
- [x] 4.2 `GameHub.ExecuteTool` emits `enemy_defeated` on a lethal attack (wires the P3-2 `kill` objective)
- [x] 4.3 `ContextEvaluator` real `in-combat` detection (adjacent hostile with health) replacing `if (false)`

### 5. Tests
- [x] 5.1 `CombatSystem` (8): damage reduces HP; lethal removes entity; reach/self/no-Health/unknown-target rejected; Character spawns with HP
- [x] 5.2 `GameMapGrain.AttackAsync` (1): two non-lethal hits then a lethal one; the defeated target no longer exists (a follow-up attack reports not-found)
- [x] 5.3 Tool reachability (Player allows `attack`, Explorer denies)
- [x] 5.4 Full solution build + suite green

## Slice 2 — depth (follow-up, not this pass)
- [ ] 6.1 Monster retaliation/aggro on the tick
- [ ] 6.2 Weapon / attack-power components (variable damage)
- [ ] 6.3 Death loot + combat analytics
