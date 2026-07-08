## Why

The [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §4.4 (Wave 1) confirms there is no within-run character progression at all: no XP, levels, skills, talents, classes, or attribute stats beyond `Health`. `Aetherium.Server/MetaProgression` (verified: `IMetaProgressionGrain`/`MetaProgressionModels.cs`) is a **cross-world unlock tracker** for world-generation content (discovered templates, unlocked generators) — it has no character stats, currency, or level concept, so it cannot stand in for within-run growth.

## What Changes

- Add `Attributes`: a per-campaign named vector (string-keyed, not a hardcoded field list), with `Vitality` and `Speed` shipped as the engine's own defaults (mirroring `Health`'s max and `ActionSpeed`'s refill rate — see design.md for why these aren't wired to those components yet).
- Add `ProgressPool`/`ProgressPools`: a generic named XP/level pool (a campaign can define `combat_xp`, `exploration_xp`, `crafting_xp`, etc., independently), with a pluggable `ILevelCurve` so campaigns choose their own XP-to-level conversion instead of the engine hardcoding one.
- Add `SkillDefinition`/`SkillCatalog`/`UnlockedSkills`/`SkillUnlockService`: skill/talent data assets with prerequisite gating (a tree, a web, or flat point-buy are all expressible as prerequisite lists), independent of the not-yet-built ability system (§4.3) — skills reference future ability/attribute effects by string id, not by hard type dependency.
- Add `RoleAffinity`: an optional per-character `{roleTag: weight}` map biasing which abilities/skills are available, supporting both freeform builds (empty) and fixed archetypes (a dominant role weight).
- **Phase 1 (this change): all of the above are new, additive ECS components/data classes, fully unit-tested, in isolation.** Nothing is attached to `Character`/`Monster` by default, no grain or DTO references these types, and `MetaProgression` is untouched.
- Phase 2 (follow-up change): attach default `Attributes`/`ProgressPools` to `Character` construction; wire XP awards into combat/quest/exploration events; document and implement the meta-progression handoff (which meta-unlocks seed a new character's start-kit; which within-run milestones post back to `MetaProgressionGrain`) without merging the two state shapes; feed `SkillUnlockService` from a real UI/command path.

## Impact

- Affected specs: new capability `character-progression` (attributes, progress pools, skill gating, role affinity)
- Affected code: new `Aetherium.Server/Progression/*.cs`, new tests under `Aetherium.Test/Progression/`. No changes to `Aetherium.Server/MetaProgression/`, `Character.cs`, or any grain in this change.
