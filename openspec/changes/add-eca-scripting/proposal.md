# Add ECA Scripting (event–condition–action rules, runtime T0)

## Why

The game-definition loader made a game's *rules* declarative and the content slice made its *nouns*
declarative. The one thing an author still cannot express without a C# pull request is **reactive
logic**: "when a cult acolyte dies, a wolf appears to avenge it," "when you kill a NetWatch agent,
its death-surge slows you." Today the only reaction to a creature death is the three engine-hardcoded
behaviors at the kill chokepoint (award XP, apply `kill:<type>` faction standing, drop loot). Adding a
fourth reaction means editing `GameMapGrain`.

Event–Condition–Action (ECA) rules are the declarative *verbs* — the last major authoring gap. There
is a thorough design vision (`docs/audits/2026-07-06-engine-gap-analysis/design-eca-visual-scripting.md`,
~520 lines, T5) and **zero implementation**. This change builds the T0 runtime spine: a small, closed,
deterministic rule language wired to one real event, so a bundle can add reactions as data. It
deliberately builds none of the visual editor, plugin SDK, selector engine, or FSM pages that vision
describes — those are the ceiling; this is the floor.

## What Changes

- **`EcaConfig` / `EcaRule`** join the per-world config family (`Aetherium.Model.Eca`): a rule is
  `id` + one trigger + an optional AND-ed condition list + an ordered action list — pure serializable
  data, reusing the `AbilityEffectDescriptor` "Kind + per-kind params, no polymorphism" discipline.
- **One trigger this slice — `creature_died`** — raised at the existing monster-defeat chokepoint that
  melee and ability kills already share. The event carries the victim's creature type, the killer, and
  the death location.
- **Closed condition vocabulary**: `creature_type_is`, `chance` (AND-ed; a rule with no conditions
  always fires on its trigger).
- **Closed action vocabulary, all routing through already-shipped systems**: `spawn_creature` (via the
  content catalog we just built), `deal_damage` (via the map's `DamagePipeline`), `apply_status` (via
  the shipped `burning`/`slowed`/`prone` effects). Each action targets `killer` or `victim` where
  meaningful.
- **A reflectable vocabulary registry** — every trigger/condition/action is a C# tile type carrying an
  `EcaTileDefinition` (id, role, human description, typed parameter schema, valid targets). `EcaVocabulary`
  enumerates them, and it is the single source of truth: the validator checks rules against it (no
  hand-written per-kind switch), the docs are generated from it, and a future editor palette / plugin SDK
  reads the same surface. This mirrors the shipped `AgentToolAttribute`/`AgentToolRegistry` pattern.
- **Pure evaluator, grain executor**: `EcaRuntime.Evaluate(event) → List<EcaActionRequest>` is a pure
  function (unit-testable); `GameMapGrain` executes each request against the world and fans out deltas
  — the same split the behavior tree uses (tree writes outcomes, grain fans out).
- **`rules.yaml`** becomes a conventional bundle section, loaded/validated exactly like `content.yaml`
  (cross-refs: `spawn_creature`→creature, `apply_status`→known status, `chance`∈[0,1], unique ids).
- **A committed language guide** — `docs/eca-scripting.md` — whose vocabulary reference is generated
  from `EcaVocabulary` and kept in sync by a test, so the docs can never drift from the code.
- Emberfall gains sample rules that make the world visibly reactive.

## Impact

- Affected specs: new capability `eca-scripting`; extends the `game-definitions` bundle format.
- Affected code: `Aetherium.Model` (new `Eca/` + config threading), `Aetherium.Server` (new
  `Eca/EcaCompiler`+`EcaRuntime`, the kill chokepoint in `GameMapGrain`, loader/validator/mapper),
  sample bundle, tests.
- No breaking changes: `EcaConfig` is nullable/optional; a null config means no rules fire and the kill
  path behaves exactly as today.

## Non-Goals (the T5 vision's ceiling — explicitly later)

- Visual/tile editor and the text projection.
- The `[Eca*]` Roslyn/WASM plugin SDK and inline script actions.
- Selectors / instance-picking / `it`/`them` bindings; FSM pages; sub-rule nesting.
- Triggers beyond `creature_died` (spatial, temporal, input, economy, quest events).
- Action-budget integration and the full `(seed, input-log)` determinism model.
