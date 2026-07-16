## 1. Model (Aetherium.Model)

- [ ] 1.1 Add `InteroceptionDto` + `SelfStatusDto` + `ResourcePoolStateDto` + `AbilityReadinessDto` (plain serializable POCOs, `Aetherium.Model`).
- [ ] 1.2 Add nullable `InteroceptionDto? Interoception` to `PerceptionDto` (additive; defaults to `null`).

## 2. Server projection (Aetherium.Server)

- [ ] 2.1 Add optional `Aetherium.Core.Entity? self = null` to the full `PerceptionService.ComputePerception` overload; thread `null` through the two backward-compat overloads.
- [ ] 2.2 When `self` is non-null, build `Interoception` by projecting the character's own components, each read guarded by `Has<T>()`:
  - `Health` → `Health`/`MaxHealth`
  - `StatusEffects.Active` → `Statuses` (`Id`, `RemainingTicks`)
  - `ResourcePools.All` → `Pools` (`Tag`, `Current`, `Max`, `IsInverse`)
  - `AbilityCooldowns.Snapshot` → `Cooldowns` (`AbilityId`, `RemainingTicks`)
- [ ] 2.3 Leave `Interoception` `null` when `self` is `null` (no behavior change for existing callers).
- [ ] 2.4 In `GameMapGrain.ComputeAgentPerceptionAsync`, pass the resolved player `Character` as `self`.

## 3. Tests (each maps to a spec requirement via its `**Verified by:**` line)

- [ ] 3.1 `Aetherium.Test.Perception.InteroceptionTests` — data model: `InteroceptionDto_SerializesAndRoundTrips_PascalCaseJson`, `PerceptionDto_Interoception_DefaultsToNull`.
- [ ] 3.2 Population: `Interoception_Health_ReflectsSelfLevelAndMax`, `Interoception_Statuses_ListSelfActiveStatuses_WithRemainingTicks`, `Interoception_Pools_CarryTagCurrentMaxAndInverseFlag`, `Interoception_Cooldowns_ListOnlyAbilitiesStillOnCooldown_WithRemainingTicks`.
- [ ] 3.3 Self-only + fail-safe: `Interoception_SelfOnly_DoesNotReflectAnotherEntitysState`, `Interoception_NullWhenNoSelfProvided_LegacyCallersUnaffected`, `Interoception_MissingComponents_DegradeToEmpty_WithoutThrowing`.
- [ ] 3.4 End-to-end through the grain: `ComputeAgentPerceptionAsync_IncludesInteroceptionForThePlayer`.
- [ ] 3.5 Run the full suite; confirm existing perception tests are unchanged (additive-only proof).

## 4. Close-out

- [ ] 4.1 `openspec validate add-interoception-channel --strict` passes.
- [ ] 4.2 Update `openspec/specs/perception/spec.md` at archive time (three ADDED requirements).
- [ ] 4.3 Confirm every `tasks.md` item is checked and every `**Verified by:**` test exists and is green.
