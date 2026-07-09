## Why

Combat exists but is shallow (verified in the [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §3.1/§4.2): `CombatSystem.TryAttack` deals a single flat integer, always lands if in range, has no status effects, and deletes a defeated target outright instead of giving it a `Dying`/`Corpse` interaction window. §4.2 is the other **P0** combat item in Wave 0, alongside the already-shipped [continuous action pipeline](../add-continuous-action-pipeline/proposal.md) and [content atlas](../add-content-atlas/proposal.md).

## What Changes

- Add `DamagePacket`/`DamageComponent`: typed, tag-keyed damage (replaces a bare integer), campaign-defined tags.
- Add `Resistances`: per-tag flat/percent/minimum mitigation, applied in that stable order.
- Add pluggable hit resolution (`IHitResolver`): `AlwaysHitResolver` (the original MVP's deterministic behavior, kept available) and `RollHitResolver` (accuracy vs evasion, seedable `Random`, independent crit roll).
- Add `StatusEffect`/`StatusEffects` + `StatusEffectSystem`: `BurningEffect` (damage-over-time), `SlowedEffect`/`ProneEffect` (queryable markers), refresh-on-reapply stacking.
- Add `Dying`/`Corpse` + `DeathSystem`: a lethal hit transitions the target to `Dying` (stays in the world) for a countdown, then to `Corpse` — no more silent deletion.
- Add `ThreatTable`: per-target threat ledger crediting each attacker's cumulative damage.
- Add `DamagePipeline`: composes all of the above into one delivery-agnostic (melee/ranged/aoe) resolution call.
- **Phase 1 (this change): all of the above are new, additive types, fully unit-tested (37 tests) in isolation.** `CombatSystem.TryAttack` and `GameMapGrain.AttackAsync`/`StepNpcsAsync` are **unchanged** — the original flat-damage, always-hit, delete-on-death melee path keeps working exactly as it does today.
- Phase 2 (follow-up change): reroute `GameMapGrain.AttackAsync`/monster retaliation through `DamagePipeline` + a chosen `IHitResolver`, decide how `Dying`/`Corpse` entities interact with the existing loot-drop-on-kill logic (a `Dying` target isn't dead yet — does loot drop on `Dying` or on `Corpse`?), and give `ThreatTable` a consumer (currently nothing reads `GetTopThreat()` — that's NPC AI's job, §4.5, the next Wave 0 item).

## Impact

- Affected specs: new capability `combat` (schema requirements: damage packets, mitigation, hit resolution, status effects, death-state transition, threat)
- Affected code: new `Aetherium.Server/Combat/*.cs`, new tests under `Aetherium.Test/Combat/` (alongside the existing `CombatSystemTests.cs`, untouched). No changes to `Aetherium.Server/Core/CombatSystem.cs` or `GameMapGrain.cs` in this change.
