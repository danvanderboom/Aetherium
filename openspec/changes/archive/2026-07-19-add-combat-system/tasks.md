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

## Slice 2 — depth (this pass)

### 6. Variable damage
- [x] 6.1 New `AttackPower` component (base per-entity damage) + `Weapon` component (carried-item bonus); `SwordItem` loot weapon
- [x] 6.2 `CombatSystem.ComputeAttackDamage(attacker)` = base AttackPower (or `DefaultAttackDamage`) + best single carried weapon bonus (no stacking); `Character` AttackPower(10), `Monster` AttackPower(6)

### 7. Monster retaliation/aggro
- [x] 7.1 `CombatSystem.TryAttack` gains `removeOnDeath` (default true); retaliation passes false so a downed player survives at 0 HP
- [x] 7.2 `GameMapGrain.StepNpcsAsync`: a monster within reach of a joined player attacks (and stays put) instead of wandering; health delta fanned out

### 8. Death loot + analytics
- [x] 8.1 `AttackAsync` drops a `SwordItem` at a slain monster's cell (`EntityPlacedDelta`); loot id/type returned in `AttackResultDto`
- [x] 8.2 `MapState.MonstersDefeated` / `TotalDamageDealt` counters; `IGameMapGrain.GetCombatStatsAsync` + `CombatStatsDto`; `aetherctl combat stats <mapId>`

### 9. Tests
- [x] 9.1 `CombatSystem` variable-damage (default/AttackPower/best-weapon/no-stack) + `removeOnDeath:false` downs-but-keeps
- [x] 9.2 `GameMapGrain` retaliation (adjacent monster damages player on tick, stays put, player not removed) + kill drops loot + stats accrue
- [x] 9.3 Full solution build + suite green
