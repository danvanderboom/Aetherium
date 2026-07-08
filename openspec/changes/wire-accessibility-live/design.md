## Context

Unlike the three prior "wire-X-live" slices (death/respawn, abilities, progression, factions), this one isn't per-world server data — `ActionIntent`/`SemanticDistinction` are fixed engine vocabulary, not something a game world configures differently. The live wiring is entirely client-side: the console client's real input handling and rendering. See [proposal.md](proposal.md) for what shipped in Phase 1 vs. what this change adds.

A survey of the console client (`ClientConsoleDungeonGameNew.HandleCommand`, a ~20-case `switch (keyInfo.Key)`) and its renderer (`ClientConsoleMapView`) found:
- 5 of the 8 seeded `ActionIntent`s already correspond to a real key. 3 don't: `attack` has no key at all (combat is an implicit bump-attack when `move` walks into a monster — see `6c5e9db`'s DamagePipeline wiring), and `use_item`/`toggle_inventory` have no client UI (inventory is a passive display widget, not an interactive one, today).
- The other ~15 keys (fine/sharp rotation, level up/down, vision/lighting presets, music, theme cycling, debug teleport, quit) are debug/meta features with no catalog entry — never claimed by the Phase 1 seed, which was scoped to "the game's real, already-shipped actions."
- Of the renderer's color usage, most distinctions (terrain glyphs, the player's `@` marker) already pair color with a glyph. Two genuine gaps: item key-colors were color-only, and infrared heat-vision tinting is a color-only continuous gradient. A third gap — hostile vs. friendly monsters aren't visually distinguished by *any* channel — isn't a colorblind-rule violation (the rule only flags color-only encodings) but is a real, separate accessibility/legibility gap.

## Goals / Non-Goals

- Goals:
  - Every `ActionIntent` that already has a real keybinding is demonstrably resolved from that keypress, validated against the catalog — the abstraction gets its first real caller.
  - Every `SemanticDistinction` registered this slice reflects something the console renderer *actually* draws today, and the registered set is provably colorblind-clean (an enforced test, not just documentation).
  - Fix the one real colorblind violation the survey found (item key-color), since finding it and not fixing it would leave the CI gate protecting a state we know is broken.
- Non-Goals (left for later, see tasks.md's Later section):
  - Inventing new keybindings for `use_item`/`toggle_inventory` — no interactive item-use flow exists client-side to bind to; building one is a gameplay feature, not accessibility wiring.
  - Restructuring `HandleCommand`'s dispatch around intent ids. The existing `switch (keyInfo.Key)` is untested, working code with ~20 cases; rewriting its control flow for architectural purity risks regressions for no player-visible benefit. The binding is additive: resolved and validated alongside the existing switch, not replacing it.
  - Registering or fixing the infrared heat-vision gradient — it's an intensity ramp on one thing, not a discrete distinction between two things, which is arguably outside `ColorblindLintRule`'s intended shape (see `add-accessibility-contract/design.md`'s framing).
  - Building hostile-vs-friendly monster visual differentiation — real gap, but a new rendering feature (every monster spawner today emits the same tile), not a bug this slice's scope covers.
  - The Unity renderer — no color-keyed entity/tile logic exists there yet to register or fix.
  - The screen-reader companion client and the `ActionIntent` keybinding-suggestion field — both explicitly deferred already in Phase 1's design.md, unchanged here.

## Decisions

- **The intent-id resolution is observational, not the source of dispatch truth.** `HandleCommand` still switches on `keyInfo.Key` exactly as before; the new `ConsoleActionIntentBinding.Resolve` call and `LastActionIntentId` field sit alongside it, giving the abstraction a real, tested consumer without touching proven dispatch logic. A future accessibility companion (remapping UI, screen-reader input layer) has a real signal to consume; today nothing in-tree acts on it beyond the tests.
- **The item key-color fix uses a letter glyph, not `OpensAndCloses.KeyShape`.** `KeyShape` (on the door component) turned out to hold the *same* color-name string as the key's `KeyId` ("red", "blue", ...) — it's a matching id, not a distinct geometric-shape vocabulary. Reusing it would add a color-named string next to a color, not a real second channel. A derived first-letter glyph (`R`/`B`/`G`/`Y`) is a genuine, independent visual signal and needs no new server-side data.
- **`ConsoleRendererDistinctions` only registers distinctions verified against the current renderer code**, mirroring `DefaultActionIntents`'s "grounded in real capabilities" precedent — not a wishlist of what the renderer *should* draw.

## Risks / Trade-offs

- **Console-only.** The Unity renderer has no equivalent wiring; if/when it grows real entity color-coding, it needs its own `SemanticDistinction` registration and lint test.
- **`LastActionIntentId` has no consumer yet** — same shape of risk Phase 1 accepted for the whole abstraction, now narrowed to one field. Zero behavioral risk: it's write-only from the game loop's perspective.

## Migration Plan

Additive only. No changes to existing keybindings, save data, or wire formats.

## Open Questions

None — the three deferred Phase 1 items (keybinding suggestions, hostile/friendly rendering, screen-reader client) remain deferred with no new information changing that judgment.
