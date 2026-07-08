## Context

No within-run growth exists today; `MetaProgression` (verified) is a different layer (cross-world content unlocks, not character stats). §4.4 specs generic, data-driven progression so the engine stays genre-agnostic (no hardcoded "level 20 fighter"). This change ships the primitives; see [proposal.md](proposal.md) for why attaching them to live entities is a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - `Attributes` as a string-keyed vector, not a fixed field list — a sci-fi campaign's `Hacking` attribute costs the same as fantasy's `Strength`.
  - `ProgressPool` decoupled from any specific XP curve — `ILevelCurve` is an interface a campaign implements; the engine ships one boring default (`LinearLevelCurve`).
  - `SkillUnlockService` correctly gates on prerequisites (a real tree/web, not just a flat unlocked-list) without needing the ability system (§4.3) to exist — skills reference abilities by string id, resolved later.
  - Clear, documented (not yet coded) handoff to `MetaProgressionGrain` so Phase 2 doesn't have to re-derive the boundary.
- Non-Goals (Phase 2 / later):
  - Attaching `Attributes`/`ProgressPools` to `Character`/`Monster` construction.
  - Wiring `Vitality`/`Speed` attributes to actually drive `Health.MaxLevel`/`ActionSpeed.Speed` — that coupling is a Phase 2 decision (does an attribute *replace* those components' fields, or does a system read the attribute and write the component each tick?), not assumed here.
  - Any UI/command path for spending skill points or leveling up.
  - The meta-progression handoff's actual code (only its contract is documented in this design).

## Decisions

- **`Attributes` storage is `Dictionary<string, double>`, not named properties.** Matches the `ContentAtlas` precedent of choosing string-keyed extensibility over a fixed C# type whenever the *set* of things (attributes, tags) is campaign-defined, not engine-defined. `Vitality`/`Speed` are provided as `public const string` name constants for convenience and typo-safety, not as separate strongly-typed properties.
- **`ProgressPool` is a plain data record (`Id`, `Xp`, `Level`); `ILevelCurve` is a separate injected strategy**, not a virtual method on the pool itself — this lets `ProgressPools.AddXp` be tested with a trivial curve without any campaign-specific subclassing, and matches the engine's established "inject the policy, don't hardcode it" pattern (`IHitResolver` in `deepen-combat-model` is the precedent).
- **`SkillUnlockService` is a stateless service class operating on `UnlockedSkills` + `SkillCatalog`**, not a method on either component — mirrors `CombatSystem`/`DamagePipeline`'s existing "pure stateless service" shape in this codebase, and keeps `UnlockedSkills` a plain data component.
- **Skills reference abilities/attributes by string id (`UnlocksAbilityId`, `ModifiesAttributeId`), not by a typed reference.** The ability system (§4.3) doesn't exist yet in this codebase; a typed reference would force this change to either invent a placeholder `Ability` type (wrong abstraction, guessed ahead of its own design) or block on `add-abilities` landing first. String ids are resolved by whichever system consumes them later — the same "loose coupling via stable ids" principle the content atlas already established.

## Risks / Trade-offs

- **No live entity carries these components yet.** Zero risk to running gameplay or existing tests — entirely new, unreferenced types.
- **`SkillDefinition.UnlocksAbilityId`/`ModifiesAttributeId` are unvalidated string ids with no registry to check against yet** (no `AbilityCatalog` exists — that's `add-abilities`, next in this wave). Accepted: validating them now would require inventing the very abstraction this change intentionally defers.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) attaches these components to live entities and wires the meta-progression handoff.

## Open Questions

- Should the meta-progression handoff be a one-way push (character milestones → `MetaProgressionGrain`) or bidirectional (meta-unlocks also seed starting `ProgressPools`/`Attributes`)? The design doc implies bidirectional ("which meta-unlocks add to the character start-kit, which character achievements post to meta"); deferred to Phase 2 where the actual grain-call shape is decided.
