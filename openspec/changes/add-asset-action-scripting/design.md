## Context
This change implements the design in `docs/design/asset-action-scripting.md`: a data-driven Event-Condition-Action (ECA) system that fires **asset actions** (grow/shrink, animation, tint/flash, attach-effect, sound, show/hide, sprite swap) on entities so worlds feel alive without new code per effect.

Two existing shapes ground the work:
- **The ECA *pattern* already exists — but only for build-time content.** `AdaptationRuleEngine` evaluates `AdaptationRuleDefinition`s built from `RuleConditionDefinition` + `RuleActionDefinition` (`Aetherium.Server/WorldGen/Adaptation/`), and `ContentAdaptationRule`/`NarrativeAdaptationRule`/`QuestAdaptationRule` each pair a Condition with an Action. This change **generalizes that same pattern from content generation to runtime gameplay triggers** rather than inventing a new rule model.
- **`SoundEffects` is the presentation-cue precedent.** `Aetherium.Server/Core/SoundEffects.cs` and the audio perception DTO prove that pushing non-map cues to clients is an established shape; asset actions ride the same server→client push (`PerceptionService` → `GameHub`). Sound reuses this precedent, while **visual/animation/transform commands are new.**

## Goals / Non-Goals
- Goals: a reusable runtime ECA rule model (`WHEN event IF condition THEN actions`) authored as data; an extensible asset-action vocabulary; a delivery channel executed by Unity and the console; server-authoritative handling only for footprint-affecting scale.
- Non-Goals: a full scripting language (Lua/C#); authoritative physics beyond footprint/occupancy; cutscene/timeline sequencing.

## Decisions
- **Reuse, don't reinvent:** generalize `AdaptationRuleEngine` / `RuleConditionDefinition` / `RuleActionDefinition` into a runtime engine instead of a new DSL.
- **One command shape:** `AssetActionDto { EntityId, Action, Params }` delivered via a `ReceiveAssetActions` push alongside perception — mirroring how `SoundEffects` cues already reach clients.
- **Execution split:** the server applies only actions that change authoritative state (footprint-affecting `scale`) and re-indexes occupancy before broadcast; purely cosmetic actions stay client-side.
- **Forward-compatible:** an unrecognized action is a no-op on every client (never an error), enabling capability-tiered clients (Unity rich, console subset).

## Risks / Trade-offs
- Scope creep toward a scripting engine → keep it declarative ECA with a fixed, extensible vocabulary; escalate to timelines only if proven necessary.
- Client divergence (console < Unity) → a capability-tiered action set with required graceful degradation.
- Authoritative scale is subtle → only footprint-affecting transforms touch server state; draw the line explicitly in the action catalog.
- Ordering/latency → actions are idempotent and carry triggering game-time so reconnecting clients can reconcile.

## References
- Design doc: `docs/design/asset-action-scripting.md`
- ECA pattern to generalize: `Aetherium.Server/WorldGen/Adaptation/AdaptationRuleEngine.cs`, `AdaptationRuleDefinition.cs` (`RuleConditionDefinition`, `RuleActionDefinition`), `ContentAdaptationRule.cs`
- Presentation-cue precedent: `Aetherium.Server/Core/SoundEffects.cs`, `Aetherium.Model/AudioPerceptionDto.cs`
- Server→client push: `Aetherium.Server/PerceptionService.cs`, `GameHub` (`ReceivePerceptionUpdate`)
- Unity marker/animation hooks: `Aetherium.Unity/Assets/Scripts/Rendering/PlayerController.cs`, `TilemapRenderer2D.cs`
