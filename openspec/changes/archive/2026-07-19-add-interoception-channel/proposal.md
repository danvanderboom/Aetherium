# Add the Interoception Channel (self-sense in perception, engine gap G1)

## Why

The engine's contract with a client is that it receives **perception** — semantic senses, not
privileged world state. Every outward sense is already there: sight (FOV), lighting/vision modes,
hearing (`Audio`), the felt layout of nearby affordances and items. The one sense missing is
**interoception** — a character's awareness of *their own body*.

Today a client learns its own condition only obliquely: a `ReceiveDowned`/`ReceiveDied` event tells
it the body failed, and it can infer damage from attack-result deltas — but no perception frame
carries the character's own health/max, its felt statuses (am I burning? slowed?), its resource
pools (charge, oxygen, hack-battery), or which abilities are still cooling down. A rich visual client
can't render a HUD from that; inferring HP from attack deltas is unacceptably lossy.

This is the **one true engine blocker for Aphelion M0** ([docs/design/unity-sample/milestones.md](../../../docs/design/unity-sample/milestones.md),
[engine-gaps.md G1](../../../docs/design/unity-sample/engine-gaps.md)) — the HUD cannot exist without it — and it is the whole engine ask for a playable, beautiful
M0. It is deliberately framed as a **sense**, not "HUD data": interoception is a perception channel
like sight, which keeps the door open for later depth (a concussion status that *distorts* your own
readings fits the model naturally).

## What Changes

- **`InteroceptionDto`** joins `Aetherium.Model` and hangs off `PerceptionDto` as an **optional,
  nullable** `Interoception` block (same pattern as `Inventory?`/`Audio?`). It carries:
  - `Health` / `MaxHealth` — the felt integrity of the body,
  - `Statuses` — active status ids with `RemainingTicks` (you feel that you are burning/slowed/prone),
  - `Pools` — resource pools as `{ Tag, Current, Max, IsInverse }` (a normal pool drains and a heat-style
    inverse pool fills, so the client can render a battery vs. a heat gauge correctly),
  - `Cooldowns` — abilities still cooling down as `{ AbilityId, RemainingTicks }` (a ready ability is
    simply absent — the read is "what isn't ready yet").
- **`PerceptionService.ComputePerception` gains an optional `self` entity parameter.** When supplied,
  the service projects the perceiver's own `Health`, `StatusEffects`, `ResourcePools`, and
  `AbilityCooldowns` components into the block. Every read is **guarded** (`Has<T>()` before `Get<T>()`)
  so a character missing a component degrades to an empty list, never a throw. When `self` is omitted
  (every legacy caller), `Interoception` stays `null` — a pure additive, backward-compatible change.
- **`GameMapGrain.ComputeAgentPerceptionAsync` passes the resolved player `Character`** as `self`, so
  the live perception a player/agent receives now includes their own interoception.
- **Self-only by construction.** The block reflects *only* the perceiving character's own components.
  It never exposes another entity's internals — reading *others'* condition is the separate,
  deliberately-banded social-insight channel (G2, M1), not this change.

Pure protocol read-model. No gameplay change, no new per-world config, no tick behavior — the state
already lives on the grain; this change only *senses* it.

## Impact

- Affected specs: `perception` (ADDED: the interoception channel requirements).
- Affected code: `Aetherium.Model` (new `InteroceptionDto` + nullable field on `PerceptionDto`),
  `Aetherium.Server` (`PerceptionService` projection + optional `self` param; one call site in
  `GameMapGrain`), tests.
- No breaking changes: `Interoception` is nullable and additive; existing perception frames and every
  current `ComputePerception` caller are byte-identical until they opt in by passing `self`.

## Non-Goals

- **Social insight into other entities** (condition bands, capability tags, others' statuses) — that is
  G2, a separate M1 change; this channel is self-only.
- **Interoception-distorting statuses** (concussion blurring your own readings) — the framing leaves
  room for it, but no distortion is implemented here; the block reports ground truth.
- **New pools/statuses/cooldown mechanics** — this senses existing state; it adds none.
- **Ability *casting*** — the `ability` tool (G3) is a separate M1 change; interoception only reports
  cooldown readiness.
