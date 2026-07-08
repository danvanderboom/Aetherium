## Why

`add-accessibility-contract` (Phase 1, already shipped) built the `ActionIntent`/`ActionIntentCatalog` and `SemanticDistinction`/`ColorblindLintRule` abstractions but left both with zero real callers: no client input handler emitted an `ActionIntent`, no renderer registered a `SemanticDistinction`, and the lint rule had nothing real to check. Its own tasks.md named this as Phase 2, deferred but scoped: translate the console client's real keypresses into `ActionIntent`s, register the renderers' actual distinctions, and run the lint rule against them.

## What Changes

- **Real input translation.** `Aetherium.Console/Input/ConsoleActionIntentBinding.cs`: a pure `ConsoleKey -> ActionIntent id?` mapping for the 5 catalog-covered actions that already have a live key (`move`: arrows/WASD, `pickup`: G, `drop`: P, `interact_open`: O, `interact_close`: L). `ClientConsoleDungeonGameNew.HandleCommand` resolves and validates it against `DefaultActionIntents` on every real keypress. `attack` (an implicit bump-attack on `move`, not a distinct key) and `use_item`/`toggle_inventory` (no client UI for either yet) are deliberately left unbound — not faked.
- **A real colorblind violation, found and fixed.** The console renderer's item key-color distinction (red/blue/green/yellow) was color-only. `ClientConsoleMapView.ResolveKeyItemGlyph` now gives each key color a distinct glyph (its own first letter) alongside its color.
- **Real distinctions registered and linted.** `Aetherium.Console/Accessibility/ConsoleRendererDistinctions.cs` registers the console renderer's real distinctions (terrain type, player marker, item key-color); a test runs `ColorblindLintRule` against them and asserts zero violations — the actual CI gate the design doc asked for.
- **Explicitly still out of scope**: heat-vision's infrared color ramp (a continuous gradient, not a discrete "this vs that" distinction — out of this rule's intended shape), hostile-vs-friendly monster visual differentiation (a real, separate gap — nothing currently distinguishes it on any channel — but a new rendering feature, not a wiring fix), an `ActionIntent` keybinding-suggestion field (still no consumer to judge it by), and the screen-reader companion client itself (a standalone client deliverable).

## Impact

- Affected specs: `accessibility-contract` (adds live-wiring requirements on top of Phase 1's abstraction requirements)
- Affected code: `Aetherium.Console/Input/ConsoleActionIntentBinding.cs` (new), `Aetherium.Console/Accessibility/ConsoleRendererDistinctions.cs` (new), `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs` (resolves+validates the intent id per keypress), `Aetherium.Console/Views/ClientConsoleMapView.cs` (`ResolveKeyItemGlyph` extraction + colorblind fix), new tests under `Aetherium.Test/Accessibility/`
