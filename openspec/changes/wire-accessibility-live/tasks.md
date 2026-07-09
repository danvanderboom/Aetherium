## Slice — Real input translation + enforced colorblind lint (Phase 2 of add-accessibility-contract)

- [x] 1.1 `ConsoleActionIntentBinding.Resolve(ConsoleKey)`: pure mapping for the 5 catalog-covered keys (move x4, pickup, drop, interact_open, interact_close); everything else (debug/meta keys, combat) returns null
- [x] 1.2 `ClientConsoleDungeonGameNew.HandleCommand` resolves + validates the intent id against `DefaultActionIntents` on every real keypress (`LastActionIntentId`), additive alongside the existing key switch — no dispatch restructuring
- [x] 1.3 `ClientConsoleMapView.ResolveKeyItemGlyph`: fixes the real color-only violation found in the survey (item key-color) by giving each key color a distinct first-letter glyph; `DrawItem` uses it
- [x] 1.4 `ConsoleRendererDistinctions.Build()`: registers the console renderer's real distinctions (terrain-type, player-marker, item-key-color)
- [x] 1.5 Tests: `ConsoleActionIntentBindingTests` (5 mapped keys + 4 unmapped debug keys), `KeyItemGlyphTests` (distinct glyph per key color, unrecognized color still gets a glyph), `ConsoleRendererDistinctionsTests` (the registered set produces zero `ColorblindLintRule` violations — the CI gate)
- [x] 1.6 `specs/accessibility-contract/spec.md` delta: ADDED requirements, each with a `**Verified by:**` line
- [x] 1.7 Full build + regression suite green

## Later (out of scope this slice)

- [ ] L.1 `use_item`/`toggle_inventory` keybindings, once an interactive item-use flow exists client-side to bind them to
- [ ] L.2 Hostile-vs-friendly monster visual differentiation (a real, separate gap this survey found — nothing distinguishes it on any channel today)
- [ ] L.3 Infrared heat-vision gradient: decide whether a continuous intensity ramp belongs in `ColorblindLintRule`'s scope at all, and if so, how
- [ ] L.4 Unity renderer: register its own `SemanticDistinction`s + lint test once it has real color-keyed entity/tile logic
- [ ] L.5 `ActionIntent` per-platform keybinding-suggestion field (still no consumer to judge it by)
- [ ] L.6 Screen-reader companion client consuming `PerceptionDto` (standalone client deliverable, not a schema change)
