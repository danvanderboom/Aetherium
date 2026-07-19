# Tasks: add-eca-scripting

## 1. Model tier

- [x] 1.1 `Aetherium.Model/Eca/EcaVocabularyTypes.cs`: `EcaTileRole`, `EcaValueType`, `EcaParameter`,
      `EcaTileDefinition` — pure metadata types the registry, validator, and doc-gen share.
- [x] 1.2 `Aetherium.Model/Eca/EcaConfig.cs`: `EcaConfig`, `EcaRule` (string `When`),
      `EcaConditionDescriptor` (string `Kind` + union fields), `EcaActionDescriptor` (string `Kind`,
      `EcaActionTarget` + union fields) — `[GenerateSerializer]`, flat/typed per D1.
- [x] 1.3 Add `Rules` to `GameDefinition`; `EcaConfig` to `WorldTemplate`, `WorldConfig`,
      `CreateWorldRequest` (nullable, additive serializer ids).

## 2. Vocabulary registry

- [x] 2.1 `Aetherium.Server/Eca/Vocabulary/`: one tile type per trigger/condition/action
      (`CreatureDiedTrigger`, `CreatureTypeIsCondition`, `ChanceCondition`, `SpawnCreatureAction`,
      `DealDamageAction`, `ApplyStatusAction`) exposing a static `EcaTileDefinition` + id constant.
- [x] 2.2 `EcaVocabulary`: `Id → EcaTileDefinition` registry (`All`, `TryGet`, by-role views);
      the single source of truth for validation, docs, and the runtime switch.

## 3. Loader / validator / mapper

- [x] 3.1 `GameDefinitionLoader`: `rules.yaml` conventional sibling (+ inline `rules:`),
      duplicate-declaration rejection, strict parsing.
- [x] 3.2 `GameDefinitionValidator`: registry-driven — unique rule ids; `Kind` ∈ vocabulary; required
      params present; `CreatureRef` params → content-creature; `StatusRef` params → known status;
      `chance`∈[0,1]; `deal_damage` amount > 0; `creature_type_is` unknown-creature warning.
- [x] 3.3 `GameDefinitionMapper`: carry `Rules` onto `CreateWorldRequest`.

## 4. Runtime evaluator + grain wiring

- [x] 4.1 `Aetherium.Server/Eca/EcaRuntime.cs`: pure `Evaluate(EcaEventContext) →
      List<EcaActionRequest>`; `EcaEventContext`, `EcaActionRequest` union; injected seeded RNG;
      compile step from `EcaConfig` keyed on the vocabulary tile-id constants (mirrors `AbilityCompiler`).
- [x] 4.2 `GameMapGrain`: thread `ecaConfig` through `InitializeAsync` → `MapState`; `ApplyEcaConfig`
      on init + reactivation; seed the runtime RNG from the world seed.
- [x] 4.3 Raise `creature_died` at the shared monster-defeat branch (both `AttackAsync` and
      `UseAbilityAsync`), after XP/faction/loot; execute each `EcaActionRequest`:
      spawn (content catalog + `EntityPlacedDelta`), damage (`DamagePipeline` + health delta),
      status (`StatusEffectSystem`).
- [x] 4.4 Thread through `WorldGrain`, `GameManagementGrain` (both paths), `OrleansWorldHost`.

## 5. Docs

- [x] 5.1 `Aetherium.Server/Eca/EcaVocabularyDoc.cs`: `GenerateMarkdown()` renders `EcaVocabulary` as a
      reference table (tile, role, description, parameter schema).
- [x] 5.2 `docs/eca-scripting.md`: hand-written language guide (fit vs the T5 vision, rule anatomy,
      Emberfall worked example, maturity ladder) with the generated vocabulary reference between
      `<!-- eca:vocab:start/end -->` markers; index it in `docs/README.md`.

## 6. Sample bundle

- [x] 6.1 Emberfall `rules.yaml`: "on creature_died, if cult_acolyte + chance 0.5, spawn wolf"
      (the cult's fallen are avenged) and "on creature_died, if wolf, apply slowed to killer"
      (a wolf's dying snarl) — both referencing content this bundle already defines.

## 7. Tests (names per spec Verified-by)

- [x] 7.1 Vocabulary: `EveryTileHasUniqueIdAndParameters`, `RuntimeKinds_MatchVocabularyIds`.
- [x] 7.2 Loader: `LoadBundle_RulesSection_BindsTriggersConditionsActions`.
- [x] 7.3 Validator: duplicate ids / unknown spawn creature / unknown status / chance range /
      creature_type_is warning.
- [x] 7.4 Evaluator (pure): no-conditions-fires, creature_type gating, chance 0/1, target resolution.
- [x] 7.5 Grain integration: death triggers spawn rule, deal_damage-to-killer applies, no-config
      kill path unchanged, rules recompile on reactivation.
- [x] 7.6 Docs: `CommittedDoc_MatchesGeneratedVocabularyReference`.
- [x] 7.7 Full suite green (Aetherium.Test + Aetherctl.Test).

## Later (out of scope this change — the T5 vision)

- L.1 More triggers: spatial (`entered_region`), temporal (`every <t>`), input, economy, quest, player
      death.
- L.2 Selectors / instance-picking / `it`/`them`; FSM pages; sub-rule nesting.
- L.3 Flow-control actions (`branch`, `for_each`, `wait`, `do_once`); `late`-phase cascades.
- L.4 The `[Eca*]` Roslyn/WASM plugin SDK; inline script actions; capability gating.
- L.5 Visual/tile editor + text projection over `aetherctl`; `rules trace` debugger.
- L.6 Action-budget integration; full `(seed, input-log)` determinism.
- L.7 Shared tile vocabulary with behavior trees (design doc §9.1).
