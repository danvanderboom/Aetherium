## Context

`add-character-progression` stopped at data primitives because its consumers didn't exist: no live caster, no `AbilityCatalog` to resolve `UnlocksAbilityId` against, no decided attribute→stat coupling. Two of those are now resolved — combat and abilities are live and both funnel kills through one grain-side chokepoint (`TargetEnteredDying && targetWasMonster`), and `AbilityCatalog` exists per-map. This change makes progression the connective tissue between them.

Vision constraint (unchanged from death/abilities): **Aetherium is an engine, not a game.** The engine owns the machinery (pools, curves, skill gating, attribute derivation, the award/unlock systems); each world declares its progression as `ProgressionConfig` data, YAML-authored later, ECA-scriptable later. The engine ships zero progression content.

## Goals / Non-Goals

- Goals:
  - The full loop is live and per-world data-driven: kill → XP → level → (meets a skill's requirements) → unlock skill → grant ability / modify attribute → derived stat changes.
  - Reuse every live mechanism: the shared monster-defeat chokepoint, the compiled `AbilityCatalog`, `SkillUnlockService`, the `DeathPolicy`/`AbilityConfig` threading recipe.
  - Data model anticipates ECA-style rules so generalizing awards/skill-effects later is additive.
- Non-Goals (deferred; see tasks.md "Later slices"):
  - **Meta-progression handoff** (`MetaProgressionGrain` ↔ start-kit/milestones). Its grain-call shape is its own change; this slice keeps earned progression within-run.
  - **XP from exploration/quests.** Kills are the only XP source this slice; other event kinds are additional `XpAwardRule` triggers later.
  - **A skill-point economy.** Skills gate on prerequisites plus an optional `RequiredPoolLevel`; "earn N points per level, spend to unlock" is a later, config-driven addition.
  - **Full ECA generalization** of award/skill rules (typed rule subset now).
  - **Persistence of earned progression across grain reactivation.** Progression components live on the in-world `Character` (ephemeral, like resource pools and player sessions today); only the *config* persists. Cross-session persistence is the meta-handoff's job.
  - **Monster progression.** Only players carry progression components; monsters award XP, they don't earn it.

## Decisions

- **Data/behavior split, mirroring `AbilityCompiler`.** `ILevelCurve` is an injected strategy (not serializable) and `SkillCatalog`/`SkillDefinition` are runtime types; the config carries `LevelCurveDefinition`/`SkillDefinitionData` (pure data), and `ProgressionCompiler` compiles them — building per-pool `ILevelCurve` instances, the runtime `SkillCatalog`, and fresh per-character components. Same load-bearing move abilities needed.
- **One shared XP-award helper feeds both kill paths.** `AttackAsync` and `UseAbilityAsync` already share `SpawnMonsterLoot`; they gain a shared `AwardKillXp(killer, defeatedType)` call at the exact `TargetEnteredDying && targetWasMonster` branch, so melee and ability kills award identically. (The GameHub `enemy_defeated` narrative hook is melee-only and stays as-is — it's a narrative signal, not the XP source.)
- **`XpAwardRule` is a typed, declarative record, not an ECA script yet.** Fields: `OnEvent` (enum, `MonsterDefeated` only this slice), `PoolId`, `Amount`, optional `EnemyTypeFilter`. A closed enum + filter mirrors how `AbilityEffectDescriptor` shipped a typed subset with the general (effect-kind / ECA) expansion deferred — the config shape is the ECA seam.
- **Skills gate on prerequisites AND an optional `RequiredPoolLevel`.** `SkillUnlockService` already gates prerequisites; adding a `{RequiredPoolId, RequiredLevel}` check connects the XP loop to skills (otherwise XP and skills are unrelated this slice). Kept optional so a pure prerequisite-tree still works. This is a thin addition to the unlock check, not a rewrite.
- **Ability grants gate casting only when a world opts in.** Today any player can cast any catalog ability. Silently gating on `GrantedAbilities` would change that for every existing world/test. So: a per-world `RequireSkillToCastAbilities` flag (default **false**). False → `UseAbilityAsync` is unchanged (catalog membership is the only gate). True → `UseAbilityAsync` additionally requires the ability be in the caster's `GrantedAbilities`. `UnlocksAbilityId` populates `GrantedAbilities` regardless, so the data is always meaningful; the flag only decides whether it's enforced.
- **Attribute→stat coupling is declarative and applied on-change, not per-tick.** `AttributeDerivation {AttributeId, DerivedStat, PerPoint, Base}` maps e.g. `vitality → health_max`. Resolving task 2.2's fork: a system *reads the attribute and writes the component* (not "the attribute replaces the field"), but only at change points (join, skill-modify), never polling each tick — cheaper and deterministic. `DerivedStat` is a small enum (`HealthMax`, `ActionSpeed`) this slice; more stats are additive.
- **Progression components stamped at join, not in `Character`'s constructor.** Keeps `Character` genre-neutral (a world with no `ProgressionConfig` gets no pools/attributes/skills), exactly as resource pools are stamped at join, not constructed.

## Risks / Trade-offs

- **`RequireSkillToCastAbilities` interaction with abilities tests.** Default-false means every existing `AbilityCastTests` case is unaffected; a dedicated new test covers the true path. Explicitly flagged so the behavior switch isn't silent.
- **Earned progression is ephemeral this slice.** A silo restart drops a player's XP/skills (they reconnect fresh, like today's sessions). Acceptable and consistent with the current player-session model; permanent progression is the deferred meta-handoff. Called out so it isn't mistaken for a durability bug.
- **`AttributeDerivation` overwriting `Health`.** Applying vitality→`Health.MaxLevel` at join replaces `Character`'s flat `Health(100,100)` *only when the world declares the derivation and a starting vitality*; otherwise Health is untouched. No existing world declares one, so no behavior change by default.

## Migration Plan

Additive only. `ProgressionConfig` is nullable everywhere it's threaded (null → no progression, the pre-change state). `RequireSkillToCastAbilities` defaults false. `Character` construction is unchanged. No existing test or behavior depends on the new components' presence.

## Open Questions

Resolved during scoping (recorded for traceability):

- **Content ownership:** per-world `ProgressionConfig` data, engine ships none — same as death/abilities.
- **XP source this slice:** monster kills only, via typed `XpAwardRule`s; exploration/quest triggers deferred.
- **XP↔skills connection:** optional `RequiredPoolLevel` on skills, so leveling actually gates unlocks; no skill-point economy yet.
- **Ability-grant enforcement:** `GrantedAbilities` always populated by `UnlocksAbilityId`; enforced as a cast gate only under the per-world `RequireSkillToCastAbilities` flag (default false, preserving current behavior).
- **Attribute coupling:** declarative `AttributeDerivation`, read-attribute-write-component on change events (not per-tick, not field-replacement).
- **Persistence:** earned progression ephemeral within-run; only config persists. Meta-handoff deferred.
