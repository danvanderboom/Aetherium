## Context

All NPC decision logic lives inline in `GameMapGrain.StepNpcsAsync` (`Aetherium.Server/MultiWorld/GameMapGrain.cs:895-975`); `Monster.NextWanderDirection()` is the only per-entity decision unit, and it only wanders. The engine gap-analysis (§4.5) specs behavior trees as the default cheap-brain architecture. This change ships the engine plus one worked example — see [proposal.md](proposal.md) for why live rewiring is a deferred Phase 2.

## Goals / Non-Goals

- Goals:
  - The node vocabulary §4.5 names: `Sequence`, `Selector`, `Parallel`, `Condition`, `Action`, `Wait`, `Random`, `Utility`.
  - A worked example (`MonsterBehaviors`) that reproduces the *exact* decision `StepNpcsAsync` makes today, proving the engine is expressive enough to be a drop-in replacement without changing observable behavior.
  - Actions call through existing, live systems (`CombatSystem`, `World.TryMoveSteps`, `Monster.NextWanderDirection`) — no parallel/duplicate game logic invented for the example.
- Non-Goals (Phase 2 / later):
  - Wiring `StepNpcsAsync` to actually use a `BehaviorTree` per monster.
  - `Blackboard` populated from real Perception — it's an empty key/value store today; nothing writes to it yet.
  - GOAP overlay (§4.5 mentions it as an optional, off-by-default extension for planner-style NPCs — not needed for the default tree-based brain).
  - Sharing one immutable tree definition across many NPC instances of the same type (a memory optimization once tree instantiation cost actually matters at scale) — Phase 1 gives every NPC its own tree instance, which is simpler and correct at today's scale.
  - Wiring tree Action nodes through the [continuous action pipeline](../add-continuous-action-pipeline/proposal.md)'s `ActionQueue`/`ActionSpeed` instead of calling `CombatSystem`/`World` directly — that pipeline isn't wired into live grains yet either (its own Phase 2), so coupling two not-yet-live systems together speculatively would just be extra surface area with no real caller.

## Decisions

- **One tree instance per NPC, not a shared immutable definition + blackboard-keyed state.** Composite nodes (`SequenceNode`/`SelectorNode`) hold their own "which child is running" index as a plain field. This is simpler to implement and test than routing every node's transient state through the blackboard by a stable node id, and is the same trade-off most small behavior-tree libraries make by default. Revisit only if per-NPC tree allocation is measured to matter at the "tens of thousands of NPCs" scale the design doc anticipates.
- **`MonsterBehaviors` calls the live `CombatSystem.TryAttack`, not the new `DamagePipeline`** from `deepen-combat-model`. The point of this worked example is proving parity with today's live behavior, not previewing the deeper damage model — swapping in `DamagePipeline` is a Phase 2 decision made alongside actually wiring the tree into `StepNpcsAsync`.
- **`ParallelNode`'s success/failure policy is a required-successes count, not a fixed "all"/"any" enum.** A plain integer (defaulting to "all children") covers both common policies without adding a second concept, and lets an author require e.g. "at least 2 of 4" without a third policy value.
- **`RandomSelectorNode`/`UtilitySelectorNode` re-select only when not `Running`.** Once a child is committed to (returns `Running`), the node keeps ticking that same child rather than re-rolling/re-scoring every tick — otherwise a channelled/long action could never complete.

## Risks / Trade-offs

- **The engine has no live caller.** Zero risk to running gameplay; the entire `Aetherium.Server/Ai/` namespace is new and unreferenced outside its own tests until Phase 2.
- **`MonsterBehaviors`'s adjacency check re-implements `GameMapGrain.FindAdjacentPlayer`'s Manhattan-distance-≤1 rule** rather than sharing code with it (that method is private to the grain). Acceptable for a Phase 1 worked example; Phase 2's actual wiring should resolve which copy is authoritative (likely: extract a shared helper, or replace the grain's private method entirely once the tree takes over).

## Migration Plan

Additive only — no migration. Phase 2 (separate change) replaces `StepNpcsAsync`'s inline decision with per-monster trees and decides the `DamagePipeline` question above.

## Open Questions

- Should `Snake`/`Zombie` get their own distinct trees (a snake that flees at low health, a zombie that's slower but hits harder) in Phase 2, or start from the same `MonsterBehaviors` tree and differentiate later? Deferred — no design pressure yet since neither subtype has any behavior today.
