## Context

Input handling today (console client) presumably switches on `ConsoleKey` directly (confirmed: no intent/binding abstraction exists anywhere), and no renderer-binding check exists for colorblind safety. §4.13 specs both as engine-level contracts. This change ships the two data models; see [proposal.md](proposal.md) for why translating live input and registering real renderer distinctions is a deferred Phase 2, and why the screen-reader client itself is out of scope entirely.

## Goals / Non-Goals

- Goals:
  - `ActionIntent` ids seeded from real, already-shipped actions (not speculative ones) — mirrors this project's `DefaultContentAtlas` precedent of grounding a seed catalog in actual code rather than a wishlist.
  - `ColorblindLintRule` correctly distinguishes "color-only" (a violation) from "color plus something else" (fine) from "no color at all" (not what this rule checks — a distinction encoded by label alone isn't a colorblind problem, it might be a *different* accessibility problem, out of this rule's scope).
  - Reuse `add-content-atlas`'s `AudioTag` for a distinction's audio channel rather than a second ad-hoc audio-tagging scheme.
- Non-Goals (Phase 2 / later):
  - Translating real client input (keyboard/gamepad) into `ActionIntent`s.
  - Registering the console/Unity renderers' actual tile/entity/effect distinctions and running the lint rule in CI.
  - The screen-reader companion client itself — a standalone client application, not a schema.
  - Sonification beyond "a distinction can reference an `AudioTag` id" — actually driving spatial audio from that reference is a client concern.

## Decisions

- **`ColorblindLintRule` flags "Color present AND no other channel present," not "Color present at all."** The design doc's actual requirement is that color never be the *sole* channel — a legitimate renderer commonly uses color *and* shape together (a red circle vs. a blue square), which must not be flagged. Flagging any use of color at all would be both wrong per the spec and impossible for any renderer to satisfy without becoming monochrome.
- **A distinction with *no* channels marked, or only non-color channels, is never a violation of this specific rule.** It might indicate a different accessibility gap (nothing communicates the distinction at all, or it's audio-only and unavailable to a Deaf player) — but that's a distinct concern from the colorblind contract this rule enforces, and conflating them would make the rule's pass/fail meaning muddy.
- **`ActionIntent` carries no keybinding data at all** — deliberately just an id and a description. Binding to a specific key/button/gesture is a client-side concern per the design doc ("renderers may bind it... none of this leaks into the server"); the engine's contract is only that the *id* exists and is stable.

## Risks / Trade-offs

- **Neither abstraction has a real caller yet.** Zero risk to running gameplay — `Aetherium.Server/Accessibility/` is new and unreferenced outside its own tests.
- **`DefaultActionIntents`'s seed list is hand-authored from a read of existing grain methods/tool categories, not mechanically derived** (unlike `DefaultContentAtlas`'s tile-type coverage test, there's no single enumerable "list of all real actions" in this codebase to check against automatically). Accepted: a hand-curated seed is still grounded in real capabilities; a future coverage test could be added once/if such an enumerable command registry exists.

## Migration Plan

Additive only — no migration. Phase 2 (separate change) wires real input translation and renderer-distinction registration.

## Open Questions

- Should `ActionIntent` eventually carry a default keybinding *suggestion* per platform (not a hard binding, just a sensible default for a new renderer to start from)? Deferred — no renderer consumes `ActionIntent` yet to judge whether that'd help or just be unused data.
