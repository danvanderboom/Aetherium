## 1. Phase 1 — Command channel + client executors
- [ ] 1.1 Add `AssetActionDto` (entity id, action name, parameter dictionary) to `Aetherium.Model`
- [ ] 1.2 Add a `ReceiveAssetActions` push on `GameHub` and emit it from `PerceptionService` alongside perception updates (following the `SoundEffects` precedent)
- [ ] 1.3 Implement a Unity asset-action executor component (tween scale, play Animator clip, apply tint/effect) that ignores unsupported actions
- [ ] 1.4 Implement the console subset (glyph/size hint, color pulse, sound)
- [ ] 1.5 Fire asset actions imperatively from code (no rules yet) to validate the end-to-end pipeline

## 2. Phase 2 — Runtime ECA engine
- [ ] 2.1 Generalize `AdaptationRuleDefinition` / `RuleConditionDefinition` / `RuleActionDefinition` into a runtime rule model (WHEN event / IF condition / THEN actions)
- [ ] 2.2 Implement a runtime rule engine that subscribes to gameplay events (band entry/exit, takeoff/landing, boarding, station dwell, interaction, collision, timers)
- [ ] 2.3 Evaluate rule conditions against runtime state and emit `AssetActionDto`s through the Phase 1 channel
- [ ] 2.4 Load rules as data from `Data/Rules/`
- [ ] 2.5 Author example rules (dropship landing grow + animation; satellite hack tint pulse)

## 3. Phase 3 — Server-authoritative actions
- [ ] 3.1 Identify footprint-affecting actions (a `scale` that changes occupancy) and apply them on the server
- [ ] 3.2 Re-index occupancy after an authoritative scale, before broadcasting the action to clients
- [ ] 3.3 Keep purely cosmetic actions client-side; document the authoritative-vs-cosmetic dividing line
- [ ] 3.4 Guard hot paths; make actions idempotent and carry triggering game-time for reconnect reconciliation

## 4. Phase 4 — Authoring & catalog
- [ ] 4.1 Define an action catalog with capability tiers (Unity rich vs console subset) and validation
- [ ] 4.2 Add hot-reload of rule data from `Data/Rules/`
- [ ] 4.3 Write designer authoring docs for rules and the action vocabulary
- [ ] 4.4 Add tests for channel delivery, rule evaluation, and server-authoritative occupancy re-indexing
