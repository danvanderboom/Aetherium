# Design: ECA Asset-Action Scripting

**Status:** Draft design · **OpenSpec change:** `add-asset-action-scripting`

## Summary

Give designers a data-driven way to make the game *react*: an **Event-Condition-Action (ECA)** rule fires
**asset actions** on game entities — grow or shrink a vehicle, play an animation, flash a tint, attach a
particle effect, emit a sound. Actions are **presentation/state commands** delivered to clients (and, where
they affect gameplay state, applied on the server), so worlds can be authored to feel alive without new code
per effect.

Examples the feature must support:
- A dropship **grows** as it descends to fill its landing footprint, then **shrinks** back to a compact icon
  when parked.
- A monorail car **plays its door-open animation** when it dwells at a station.
- A satellite **pulses/tints** while being hacked.
- A boss vehicle **scales up** and **triggers a transform animation** at a phase change.

## Goals / Non-Goals

**Goals**
- A reusable **ECA rule** model: `WHEN <event> IF <condition> THEN <actions>`, authored as **data**.
- A vocabulary of **asset actions**: `scale/grow/shrink`, `playAnimation`, `tint/flash`, `attachEffect`,
  `playSound`, `setSprite/setModel`, `show/hide` — extensible.
- A **delivery channel** so clients (console + Unity) execute the visual/animation actions; server applies any
  that also change authoritative state (e.g. footprint size change).
- Rules bind to real triggers: entity enters/leaves a band, vehicle takeoff/landing, boarding, timer/schedule,
  interaction (hack), collision, health/phase thresholds.
- Per-world / per-entity **data**, consistent with the engine's data-vs-behavior split.

**Non-Goals**
- A full scripting language (Lua/C#) — this is declarative ECA with a fixed, extensible action set.
- Authoritative physics from "grow/shrink" beyond footprint/occupancy updates.
- Cutscene sequencing (a later layer could compose ECA actions into timelines).

## Current state (grounding)

- **The ECA *pattern* already exists** — but only for content generation. `AdaptationRuleEngine` evaluates
  `AdaptationRuleDefinition`s built from `RuleConditionDefinition` + `RuleActionDefinition`
  (`Aetherium.Server/WorldGen/Adaptation/…`), and `ContentAdaptationRule`/`NarrativeAdaptationRule`/
  `QuestAdaptationRule` each pair a Condition with an Action. This is the model to **generalize** from
  build-time content to **runtime gameplay** triggers.
- **There is no asset-action / animation / transform command channel to clients.** Grep finds no
  `animate/scale/AssetAction/PresentationCommand/VisualEffect`. The only existing presentation-cue precedent
  is **`SoundEffects`** (`Aetherium.Server/Core/SoundEffects.cs`) and the audio perception DTO — proof that
  pushing non-map cues to clients is an established shape, but visuals/animation/transform are new.
- **Perception is the existing server→client push** (`PerceptionService` → `ReceivePerceptionUpdate`). Asset
  actions can ride alongside it as a parallel message (`ReceiveAssetActions`) or be embedded per-entity in the
  perception payload as an `actions`/`fx` list.
- **Unity has no animation/scale hooks yet** — tiles are placeholder, the player marker only lerps position
  and rotates the sprite (`PlayerController.cs:71-90`); there is no committed scene. So Unity needs a small
  **asset-action executor** component. Console interprets a subset (glyph swap, blink, color pulse, sound).

## Model

### Rule (data)
```jsonc
// Data/Rules/vehicle-fx.json  (illustrative; mirrors RuleConditionDefinition/RuleActionDefinition shape)
{
  "ruleId": "dropship-landing-grow",
  "when":  { "event": "vehicle.landing", "entityTag": "dropship" },
  "if":    { "all": [ { "state": "flight.state", "eq": "Landing" } ] },
  "then":  [
    { "action": "scale", "target": "self", "to": 1.0, "durationMs": 1200, "curve": "easeOut" },
    { "action": "playAnimation", "target": "self", "clip": "gear-deploy" },
    { "action": "playSound", "sound": "thrusters-down" }
  ]
}
```

### Asset action (command)
```csharp
// Aetherium.Model/AssetActionDto.cs (new)
public class AssetActionDto
{
    public string EntityId { get; set; }      // which asset
    public string Action { get; set; }         // "scale","playAnimation","tint","attachEffect","playSound","show","hide","setSprite"
    public Dictionary<string, object> Params { get; set; } // to, durationMs, curve, clip, color, effectId, sound…
}
```

### Trigger sources
Runtime events already available or added by sibling changes: flight-plan arrival, `land`/`takeoff`
transitions, boarding/disembark, station dwell, interaction (`hack`), collision (`collidable` policy from
[`flying-entities`](flying-entities.md)), timers/`EventSchedulerGrain`, health/phase changes. A lightweight
**runtime rule engine** (generalized from `AdaptationRuleEngine`) subscribes to these, evaluates conditions,
and emits `AssetActionDto`s.

### Execution split
- **Server** applies actions that change authoritative state — most importantly **`scale` that changes a
  footprint** (a grown ship occupies more tiles; re-index occupancy per
  [`boardable-vehicles`](boardable-vehicles.md)). It then broadcasts the action to clients in the area.
- **Clients** execute the *presentation*: Unity's asset-action executor tweens scale, plays the Animator clip,
  applies tint/effect; the console renders its subset (glyph/size hint, color pulse, sound). Unknown actions
  are ignored gracefully (forward-compatible).

## Interaction with other features
- **Vehicles** — grow/shrink on land/takeoff; door/animation on boarding; the footprint-affecting scale is
  the one server-authoritative case.
- **Flying** — hack-pulse on satellites; collision reactions under the `collidable` policy.
- **Transit** — station dwell animations (doors), service arrival/depart cues.
- **Adaptive-depth** — asset actions ride the same server→client push as perception; off-screen/occluded
  assets can skip their client fx.

## Phasing
- **Phase 1 — Command channel + Unity/console executors.** `AssetActionDto` + `ReceiveAssetActions` push +
  a Unity asset-action executor (scale/tint/animation) + console subset. Fire actions imperatively from code
  first (no rules yet) to validate the pipeline.
- **Phase 2 — Runtime ECA engine.** Generalize `AdaptationRuleDefinition`/`RuleConditionDefinition`/
  `RuleActionDefinition` into a runtime engine subscribing to gameplay events; load rules from `Data/Rules/`.
- **Phase 3 — Server-authoritative actions.** Footprint-affecting `scale` re-indexes occupancy; guard hot
  paths.
- **Phase 4 — Authoring & catalog.** Action catalog + validation; designer docs; hot-reload of rule data.

## Risks & trade-offs
- **Scope creep toward a scripting engine.** Keep it declarative ECA with a fixed action vocabulary; escalate
  to timelines only if needed.
- **Client divergence.** Console can't do everything Unity can; define a **capability-tiered** action set and
  require graceful degradation (an unsupported action is a no-op, never an error).
- **Authoritative scale is subtle.** Only footprint-affecting transforms touch server state; purely cosmetic
  scale stays client-side. Draw that line explicitly in the action catalog.
- **Ordering/latency.** Actions should be idempotent and carry the triggering game-time so late-joining or
  reconnecting clients can reconcile.

## Key source references
- ECA pattern to generalize: `Aetherium.Server/WorldGen/Adaptation/AdaptationRuleEngine.cs`,
  `AdaptationRuleDefinition.cs` (`RuleConditionDefinition`, `RuleActionDefinition`), `ContentAdaptationRule.cs`
- Presentation-cue precedent: `Aetherium.Server/Core/SoundEffects.cs`, `Aetherium.Model/AudioPerceptionDto.cs`
- Server→client push: `Aetherium.Server/PerceptionService.cs`, `GameHub` (`ReceivePerceptionUpdate`)
- Unity marker/animation hooks: `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs:71-90`,
  `TilemapRenderer2D.cs`
- Footprint (server-authoritative scale): [`boardable-vehicles`](boardable-vehicles.md)
