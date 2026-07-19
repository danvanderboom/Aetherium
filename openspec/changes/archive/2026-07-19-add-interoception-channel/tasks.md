## 1. Model (Aetherium.Model)

- [x] 1.1 Add `InteroceptionDto` + `SelfStatusDto` + `ResourcePoolStateDto` + `AbilityReadinessDto` (plain serializable POCOs, `Aetherium.Model`).
- [x] 1.2 Add nullable `InteroceptionDto? Interoception` to `PerceptionDto` (additive; defaults to `null`).

## 2. Server projection (Aetherium.Server)

- [x] 2.1 Add optional `Aetherium.Core.Entity? self = null` to the full `PerceptionService.ComputePerception` overload; thread `null` through the two backward-compat overloads.
- [x] 2.2 When `self` is non-null, build `Interoception` by projecting the character's own components, each read guarded by `Has<T>()`:
  - `Health` → `Health`/`MaxHealth`
  - `StatusEffects.Active` → `Statuses` (`Id`, `RemainingTicks`)
  - `ResourcePools.All` → `Pools` (`Tag`, `Current`, `Max`, `IsInverse`)
  - `AbilityCooldowns.Snapshot` → `Cooldowns` (`AbilityId`, `RemainingTicks`)
- [x] 2.3 Leave `Interoception` `null` when `self` is `null` (no behavior change for existing callers).
- [x] 2.4 In `GameMapGrain.ComputeAgentPerceptionAsync`, pass the resolved player `Character` as `self`.
- [x] 2.5 *(discovered during client-library integration testing)* In `GameSession.GetPerception` — the path behind every `ReceivePerceptionUpdate` hub push — pass the session's `Player` as `self`, so live client frames (not just agent JSON) carry interoception. Verified end-to-end by `Aetherium.Client.Tests.InProcServerIntegrationTests.LiveFrame_CarriesInteroception_ThroughTheHubPush` against the real in-proc server.

## 3. Tests (each maps to a spec requirement via its `**Verified by:**` line)

- [x] 3.1 `Aetherium.Test.Perception.InteroceptionTests` — data model: `InteroceptionDto_SerializesAndRoundTrips_PascalCaseJson`, `PerceptionDto_Interoception_DefaultsToNull`.
- [x] 3.2 Population: `Interoception_Health_ReflectsSelfLevelAndMax`, `Interoception_Statuses_ListSelfActiveStatuses_WithRemainingTicks`, `Interoception_Pools_CarryTagCurrentMaxAndInverseFlag`, `Interoception_Cooldowns_ListOnlyAbilitiesStillOnCooldown_WithRemainingTicks`.
- [x] 3.3 Self-only + fail-safe: `Interoception_SelfOnly_DoesNotReflectAnotherEntitysState`, `Interoception_NullWhenNoSelfProvided_LegacyCallersUnaffected`, `Interoception_MissingComponents_DegradeToEmpty_WithoutThrowing`.
- [x] 3.4 End-to-end through the grain: `ComputeAgentPerceptionAsync_IncludesInteroceptionForThePlayer`.
- [x] 3.5 Run the full suite; confirm existing perception tests are unchanged (additive-only proof). *(2238 passed, 0 failed; the 2 skips are pre-existing seed-dependent self-ignores in EndToEndSharedMutationTests, unrelated.)*

## 4. Close-out

- [x] 4.1 `openspec validate add-interoception-channel --strict` passes.
- [x] 4.2 Update `openspec/specs/perception/spec.md` at archive time (three ADDED requirements).
- [x] 4.3 Confirm every `tasks.md` item is checked and every `**Verified by:**` test exists and is green.
