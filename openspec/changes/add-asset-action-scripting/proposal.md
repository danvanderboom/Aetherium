## Why
Worlds need to *react* — a dropship should grow as it descends to fill its landing footprint, a monorail should play its door-open animation when it dwells at a station, a satellite should pulse while it is hacked — but today there is no way to author these reactions as **data**, and no channel to deliver visual/animation/transform cues to clients. An Event-Condition-Action (ECA) rule pattern already exists for build-time content generation (`AdaptationRuleEngine`), and `SoundEffects` already proves that non-map presentation cues can be pushed to clients. This change generalizes that pattern to **runtime gameplay** and adds the missing visual command channel, so designers can make assets feel alive without writing new code per effect.

## What Changes
- Add a new `asset-actions` capability: a server→client **asset-action command channel** that delivers commands (an entity id, an action name, and parameters) alongside perception, with unknown actions ignored gracefully as no-ops.
- Define an extensible **asset-action vocabulary**: `scale`/`grow`/`shrink`, `playAnimation`, `tint`/`flash`, `attachEffect`, `playSound`, `show`/`hide`, `setSprite`.
- Add a **runtime ECA rule engine** — generalized from `AdaptationRuleEngine` / `RuleConditionDefinition` / `RuleActionDefinition` — that evaluates data-authored `WHEN event IF condition THEN actions` rules against runtime gameplay events (band entry, takeoff/landing, boarding, station dwell, interaction, collision, timers) and emits asset actions.
- Apply **server-authoritative** actions (a `scale` that changes a footprint) on the server, re-indexing occupancy before broadcast; keep purely cosmetic actions client-side.
- Add a **Unity asset-action executor** that tweens scale, plays Animator clips, and applies tint/effect while ignoring unsupported actions; the console interprets a capability-tiered subset.

## Impact
- Affected specs: `asset-actions` (NEW capability), `client` (adds the Unity asset-action executor requirement).
- Affected code:
  - `Aetherium.Model/AssetActionDto.cs` (new command DTO: entity id, action name, params)
  - `Aetherium.Server/PerceptionService.cs` + `GameHub` (`ReceiveAssetActions` push alongside perception)
  - New runtime rule engine generalized from `Aetherium.Server/WorldGen/Adaptation/AdaptationRuleEngine.cs`
  - `Data/Rules/` (data-authored ECA rule files)
  - `Aetherium.Unity/Assets/Scripts/Rendering/` (asset-action executor component)
  - Console renderer subset (glyph/color pulse/sound)
- Precedent: `Aetherium.Server/Core/SoundEffects.cs` (presentation-cue push); visual/animation/transform commands are new.
- Design reference: `docs/design/asset-action-scripting.md`.
- No breaking changes; unrecognized actions are forward-compatible no-ops.
