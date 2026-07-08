## ADDED Requirements

### Requirement: Console Input Translated To Action Intents
The console client SHALL resolve every real keypress that corresponds to a seeded `ActionIntent` (movement, pickup, drop, interact-open, interact-close) to that intent's id and validate it against the registered catalog. Keys with no catalog entry (debug/meta features, and combat, which is an implicit action rather than a distinct keypress) SHALL resolve to no intent id, not a fabricated one.

**Verified by:** `Aetherium.Test.Accessibility.ConsoleActionIntentBindingTests.Resolve_KeyWithCatalogEntry_ReturnsItsIntentId`, `.Resolve_KeyWithNoCatalogEntry_ReturnsNull`

#### Scenario: A key with a catalog entry resolves to that intent
- **WHEN** `ConsoleActionIntentBinding.Resolve` is called with a key that maps to a real, already-shipped action (e.g. `W` for movement, `G` for pickup)
- **THEN** it returns that action's `ActionIntent` id

#### Scenario: A debug/meta key resolves to no intent
- **WHEN** `ConsoleActionIntentBinding.Resolve` is called with a key outside the seed catalog's scope (e.g. rotation, level navigation, vision presets, quit)
- **THEN** it returns null rather than an invented id

### Requirement: Colorblind Lint Enforced Against Real Renderer Distinctions
The system SHALL register `SemanticDistinction`s reflecting what the console renderer actually draws today, and running `ColorblindLintRule` against that registered set SHALL produce zero violations.

**Verified by:** `Aetherium.Test.Accessibility.ConsoleRendererDistinctionsTests.RealDistinctions_ProduceNoColorblindViolations`, `.RealDistinctions_CoverTerrainPlayerAndItemKeyColor`

#### Scenario: The registered renderer distinctions pass the colorblind lint
- **WHEN** `ColorblindLintRule.FindViolations` is run against `ConsoleRendererDistinctions.Build()`
- **THEN** it returns no violations

### Requirement: Colorblind-Safe Key Item Glyphs
A key item's colored-key identity (red/blue/green/yellow) SHALL be encoded by both color and a distinct glyph, not color alone.

**Verified by:** `Aetherium.Test.Accessibility.KeyItemGlyphTests.ResolveKeyItemGlyph_KnownColor_ReturnsDistinctLetterAndColor`, `.ResolveKeyItemGlyph_DifferentColors_NeverShareAGlyph`, `.ResolveKeyItemGlyph_UnrecognizedColorName_StillGetsAGlyph`

#### Scenario: Each key color renders its own glyph
- **WHEN** `ClientConsoleMapView.ResolveKeyItemGlyph` is called with a key item's color id
- **THEN** it returns a glyph distinct from every other key color's glyph, alongside that color
