## 1. Action intent + colorblind contract (Phase 1 — this change)

- [x] 1.1 `ActionIntent`/`ActionIntentCatalog` + `DefaultActionIntents` seed (grounded in real, already-shipped actions)
- [x] 1.2 `SemanticDistinction`/`AccessibilityChannel` (with optional `AudioTag` id reuse from `add-content-atlas`) + `ColorblindLintRule`
- [x] 1.3 Unit tests (9 tests): action-intent add/get/duplicate-rejection, default-seed coverage of real actions, color-only violation detection, color-plus-shape/color-plus-audio non-violation, no-color-at-all is not flagged, multiple distinctions checked independently
- [x] 1.4 `openspec/specs/accessibility-contract/spec.md` delta: ADDED requirements, each with a `**Verified by:**` line

## 2. Live wiring (Phase 2 — separate follow-up change, not started here)

- [ ] 2.1 Translate the console client's real keyboard input into `ActionIntent`s instead of switching on `ConsoleKey` directly
- [ ] 2.2 Register every real tile/entity/effect `SemanticDistinction` the console and Unity renderers actually draw, and run `ColorblindLintRule` in CI against them
- [ ] 2.3 Scope and build a screen-reader companion client that consumes `PerceptionDto` and speaks it (a separate, larger deliverable — not a schema addition)
- [ ] 2.4 Decide whether `ActionIntent` should carry a per-platform default keybinding suggestion
