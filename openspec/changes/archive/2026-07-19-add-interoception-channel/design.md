## Context

`PerceptionService.ComputePerception(...)` builds the whole `PerceptionDto` from a `World` and a
`playerLocation` (plus modes/session). It receives a *location*, not the perceiving entity, so today it
literally cannot read the perceiver's own components. `GameMapGrain.ComputeAgentPerceptionAsync` already
resolves the player `Character` (via `GetPlayerCharacter(entityId)`) before calling the service — it
just passes only the location through.

The self-state this channel reports already exists as ECS components on the `Character`:
- `Aetherium.Components.Health` — `Level`, `MaxLevel`.
- `Aetherium.Server.Combat.StatusEffects` — `Active` (each `StatusEffect` has `Id`, `RemainingTicks`).
- `Aetherium.Server.Abilities.ResourcePools` — `All` (each `ResourcePool` has `Tag`, `Current`, `Max`, `IsInverse`).
- `Aetherium.Server.Abilities.AbilityCooldowns` — `Snapshot` (abilityId → remaining ticks; entries drop at 0).

## Goals / Non-Goals

- **Goals:** surface the perceiver's own body as a perception channel; zero gameplay change; additive
  and backward-compatible; self-only; graceful when a component is absent.
- **Non-Goals:** other entities' state (G2), interoception distortion, new mechanics, ability casting.

## Decisions

- **Optional `self` parameter, not a location lookup.** Add `Aetherium.Core.Entity? self = null` to the
  full `ComputePerception` overload. A location→entity lookup would be ambiguous (multiple entities can
  share a tile) and would couple the projection to spatial indexing; the caller already holds the exact
  perceiver, so pass it. Default `null` keeps every existing overload/caller compile- and behavior-identical.
- **Nullable `Interoception` block.** `PerceptionDto.Interoception` is `InteroceptionDto?`, mirroring
  `Inventory?`/`Audio?`. `null` ⇔ "no self supplied," so the wire stays additive and JSON (PascalCase,
  System.Text.Json) simply omits/nulls the field for legacy callers.
- **Guarded reads (`Has<T>()` before `Get<T>()`).** `Entity.Get<T>()` throws on a missing component, so
  each of the four projections is guarded; a character without, say, `ResourcePools` yields an empty
  `Pools` list rather than throwing. When `self` is provided, `Interoception` is non-null even if some
  sub-lists are empty. (In practice `Health` can never be absent — the `Entity` base ctor gives every
  body `Health(100, 100)` — so the guard there is pure defense; the three genuinely optional
  components are statuses, pools, and cooldowns.)
- **Cooldowns list only what's not ready.** `AbilityCooldowns.Snapshot` holds only entries with ticks
  remaining (they're removed at 0), so `Cooldowns` is a direct projection — a ready ability is absent,
  which is exactly the "what isn't ready yet" read a HUD wants.
- **`IsInverse` travels with each pool.** Without it a client can't tell a draining battery from a
  filling heat gauge; it's one bool and it's the difference between a correct and a backwards meter.
- **DTO shape (all in `Aetherium.Model`, plain serializable POCOs):**
  ```csharp
  public class InteroceptionDto {
      public int Health { get; set; }
      public int MaxHealth { get; set; }
      public List<SelfStatusDto> Statuses { get; set; } = new();
      public List<ResourcePoolStateDto> Pools { get; set; } = new();
      public List<AbilityReadinessDto> Cooldowns { get; set; } = new();
  }
  public class SelfStatusDto      { public string Id { get; set; } = ""; public int RemainingTicks { get; set; } }
  public class ResourcePoolStateDto { public string Tag { get; set; } = ""; public double Current { get; set; } public double Max { get; set; } public bool IsInverse { get; set; } }
  public class AbilityReadinessDto  { public string AbilityId { get; set; } = ""; public int RemainingTicks { get; set; } }
  ```

## Risks / Trade-offs

- **Server-truth leakage.** Exact self HP/pools/cooldowns are precise numbers — but they are the
  perceiver's *own* body, which the player is entitled to know exactly; the coarse-banding privacy
  stance applies to *others'* state (G2), not self.
- **Component coupling.** The projection references server-side ability/combat components, but only for
  reads that already exist; it introduces no new dependency direction (`Aetherium.Server` → its own
  components).

## Migration Plan

Additive only. Ship the DTO + the optional `self` param + the one grain call-site update together;
every other `ComputePerception` caller is untouched. No data migration, no wire break, nothing to roll
back beyond reverting the additive field.

## Open Questions

- Should `Interoception` ever be populated for *non-player* agents (NPC "self-awareness" for AI
  training)? The `self`-param design already allows it; M0 only needs it for players, so leave the extra
  call sites for a later change rather than speculatively wiring NPC perception here.
