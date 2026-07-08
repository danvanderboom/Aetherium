## Why

The [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §4.11 (Wave 1, first item) flags that "what happens on death?" is unspecified: no config for whether corpses persist, whether characters have lives or delete, or how a downed state interacts with revive. The [deepen-combat-model](../deepen-combat-model/proposal.md) change already shipped the mechanism (`Dying`/`Corpse` components, `DeathSystem`) but hardcoded a fixed `dyingTicks` parameter and never expires a `Corpse` — every defeated entity's corpse persists in the world forever, with no policy governing it.

**Scope note:** §4.11 also describes `WorldPersistencePolicy` (how a live world reacts to a PCG generator-version bump — freeze/regen-unexplored/fork/migrate). That is a distinct, PCG-generator-migration concern unrelated to combat/death mechanics despite sharing a heading in the design doc; it is explicitly **out of scope** here and deferred to its own future change scoped against a PCG capability.

## What Changes

- Add `DeathPolicy`: a per-world-configurable data class (`Permadeath`, `CorpseRetentionTicks`, `DropOnDeath`, `RespawnPoint`, `XpLossPolicy`/`XpLossAmount`, `DownStateEnabled`, `ReviveWindowTicks`) plus a `Default` preset matching today's shipped behavior, and a pure `ResolveDyingTicks()` method a caller can feed into `DamagePipeline.Resolve`'s `dyingTicks` parameter instead of a hardcoded constant.
- Add `CorpseAge` (a new, separate, opt-in component — `Corpse` itself is untouched) + `CorpseExpirySystem`: entities carrying both `Corpse` and `CorpseAge` age each tick and are removed once `CorpseRetentionTicks` elapses. A `Corpse` with no `CorpseAge` is left alone — today's shipped "corpses persist forever" behavior is the default/backward-compatible case, not a regression.
- **Phase 1 (this change): schema + expiry system, additive and unit-tested (7 tests), in isolation.** No per-world config storage is wired up — `GameMapGrain`/`MapState` do not yet carry a `DeathPolicy`, `DamagePipeline.Resolve` still takes its `dyingTicks` parameter as a plain argument (now policy-*resolvable*, not policy-*wired*), and nothing yet attaches `CorpseAge` to a defeated entity.
- Phase 2 (follow-up change): persist a `DeathPolicy` per world (likely a new field on `MapState`, following the existing `IPersistentState<MapState>`/`WriteStateAsync` pattern), have the live attack path (Phase 2 of `deepen-combat-model`) call `policy.ResolveDyingTicks()` and attach `CorpseAge` on defeat, and design the actual respawn flow (session/grain-level — who decides `RespawnPoint`, how XP loss is applied to a progression system that doesn't exist yet either — see the sibling `add-character-progression` change).

## Impact

- Affected specs: new capability `death-respawn-policy` (policy schema, down-state resolution, corpse expiry)
- Affected code: new `Aetherium.Server/Combat/DeathPolicy.cs`, `CorpseAge.cs`, `CorpseExpirySystem.cs`, new tests under `Aetherium.Test/Combat/`. No changes to `Combat/DeathState.cs`, `Combat/DeathSystem.cs`, `Combat/DamagePipeline.cs`, `GameMapGrain.cs`, or `MapState` in this change.
