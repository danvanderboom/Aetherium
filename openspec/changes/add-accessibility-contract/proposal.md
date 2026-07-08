## Why

The [2026-07-06 engine gap-analysis](../../docs/audits/2026-07-06-engine-gap-analysis/design-next-steps.md) §4.13 (Wave 1) observes that the Perception DTO is *already* the ideal accessibility abstraction — semantic, not visual — but nothing built on that: no input-device-agnostic action abstraction exists (verified: zero `ActionIntent`/`InputBinding`/`KeyBinding` matches anywhere in `Aetherium.Server`/`Aetherium.Console`), and no colorblind-contract enforcement exists (renderers are free to encode a distinction in color alone with nothing checking otherwise).

**Scope note:** the design doc's other §4.13 idea — an actual screen-reader companion client that speaks Perception frames — is a standalone client application, not an engine schema; it is out of scope here and left as a distinct future deliverable once a client team picks it up.

## What Changes

- Add `ActionIntent`/`ActionIntentCatalog`/`DefaultActionIntents`: a stable, input-device-agnostic action id per game action (`move`, `attack`, `interact_open`/`interact_close`, `pickup`, `drop`, `use_item`, `toggle_inventory`), seeded from the game's real, already-shipped command set — none of these ids assume keyboard, gamepad, touch, gaze, or sip-and-puff.
- Add `SemanticDistinction`/`AccessibilityChannel`/`ColorblindLintRule`: a data model for "this thing the player must be able to tell apart" plus which channels (`Color`/`Shape`/`Label`/`Audio`) encode it, and a lint pass that flags any distinction relying on `Color` alone — the design doc's explicit "enforced via a lint pass on renderer bindings" ask. A distinction's audio channel can reference an already-shipped `add-content-atlas` `AudioTag` id rather than inventing a second audio vocabulary.
- **Phase 1 (this change): both abstractions, fully unit-tested (9 tests), in isolation.** No client input handler emits an `ActionIntent`, no renderer registers a `SemanticDistinction`, and the lint rule has nothing real to check yet.
- Phase 2 (follow-up change): have the console client's input handler translate keypresses to `ActionIntent`s (today it switches on `ConsoleKey` directly); register every real tile/entity/effect distinction the console and Unity renderers actually draw and run the lint rule in CI; build the screen-reader companion client (a separate, larger deliverable).

## Impact

- Affected specs: new capability `accessibility-contract` (action intent abstraction, colorblind contract enforcement)
- Affected code: new `Aetherium.Server/Accessibility/*.cs`, new tests under `Aetherium.Test/Accessibility/`. No changes to `Aetherium.Console`'s input handling or either renderer in this change.
