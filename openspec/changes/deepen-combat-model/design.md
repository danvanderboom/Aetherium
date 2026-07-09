## Context

`CombatSystem.TryAttack` (`Aetherium.Server/Core/CombatSystem.cs:28`) is a real, shipped melee MVP (P3-7) but deliberately minimal: flat integer damage, deterministic always-hit, target deleted on death. The engine gap-analysis (§4.2) specs the deeper model this change ships as new, additive primitives. See [proposal.md](proposal.md) for why live rewiring is a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - `DamagePacket`/`Resistances`/`IHitResolver`/`StatusEffect`/`Dying`+`Corpse`/`ThreatTable` as independently testable, composable primitives following existing `Component`/system conventions.
  - `DamagePipeline` as the one place that composes them, so a future caller (Phase 2, or an ability system per §4.3) has a single entry point rather than re-deriving the composition order.
  - Preserve the original MVP's behavior as an explicit, still-available choice (`AlwaysHitResolver`), not a deleted code path.
- Non-Goals (Phase 2 / later):
  - Wiring `GameMapGrain.AttackAsync`/`StepNpcsAsync` onto `DamagePipeline`.
  - Deciding the loot-drop timing question (on `Dying` vs on `Corpse`) — that's a live-path decision, not a schema one.
  - NPC AI actually reading `ThreatTable.GetTopThreat()` to pick a target (§4.5, next Wave 0 item).
  - Ranged/AoE delivery actually finding targets in an area — `DamagePacket.Delivery` is a tag today, not yet consumed by any targeting logic.
  - Wiring `SlowedEffect`/`ProneEffect` into `ActionSystem`/`ActionSpeed` (from the [continuous action pipeline](../add-continuous-action-pipeline/proposal.md)) — they're queryable markers now; consuming them to actually throttle action cost is Phase 2+ work once a real caller needs it, to avoid speculative coupling between two independently-shipped systems.

## Decisions

- **`DamagePipeline` does not perform reach/range checks.** It's delivery-agnostic by design (melee/ranged/aoe), and reach rules differ per delivery — melee's adjacency check stays where it is today (`CombatSystem.TryAttack`); a future ranged/AoE caller supplies its own targeting before calling `DamagePipeline.Resolve`.
- **Crit multiplier (`1.5x`) lives in `DamagePipeline`, not in `IHitResolver`.** A resolver only decides *whether* a hit lands and *whether* it crits — how much a crit multiplies damage is a damage-model concern, not a targeting-model one, and keeping it in one place makes the multiplier easy to find and change.
- **`Dying`/`Corpse` are components, not a `Health` state enum**, matching this codebase's existing pattern of orthogonal marker/data components (`ObstructsMovement`, `ObstructsView`) rather than a single mutable status field.
- **`ThreatTable` uses a plain top-of-list heuristic (`GetTopThreat`)**, matching the design doc's "simple by default; overridable per NPC AI" — no configurable decay/falloff yet, since nothing consumes it until NPC AI does.
- **`RollHitResolver` takes an injected `Random`**, not a static/shared one, so combat resolution stays reproducible from `(seed, input-log)` per the engine's determinism principle — a future Phase 2 caller is responsible for seeding it per-world/per-tick rather than using a global `Random.Shared`.

## Risks / Trade-offs

- **Two damage models exist in parallel until Phase 2**: the old flat-integer path (live) and the new packet/mitigation path (unused). Zero risk to running gameplay — nothing in this change calls `DamagePipeline` from a grain.
- **`BurningEffect` writes directly to `Health.Level`** rather than routing through `DamagePipeline`/`Resistances` — a burning tick is not itself an "attack" with a hit/crit roll, so re-using the full pipeline would be a mismatch; if a campaign later wants DoT to respect resistances, that's a Phase 2+ refinement, not a Phase 1 gap.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) reroutes the live attack path and resolves the loot-drop-timing and NPC-targeting open questions above.

## Open Questions

- Should `StatusEffects` stacking support more than "refresh" (unique/N-stack/additive) before Phase 2, or wait until a concrete campaign need (e.g. multiple simultaneous burn sources) forces the decision? Leaning toward waiting — no current caller needs it.
