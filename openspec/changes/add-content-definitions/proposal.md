# Add Content Definitions (data-driven creatures, items, and spawns)

## Why

The game-definition loader (add-game-definition-loader) made a game's *rules* declarative —
death, abilities, progression, factions all load from a YAML bundle. But a game's *content* is
still hardcoded C#: every world, whatever its bundle says, is populated by `new Monster(world)`
(30 HP, attack 6, glyph `M`, dark red) placed by the population passes, and every defeated
monster drops the same `SwordItem`. Emberfall's faction doctrine reacts to `kill:wolf` — but no
wolf exists; Neonveil's netrunners fight the same fantasy monsters as everyone else.

The engine's core value — a game is data, not code — requires the nouns to be data too: what
creatures exist, what their stats/glyphs/behavior are, what items exist, what drops from what.

## What Changes

- **`ContentConfig`** joins the per-world config family (`Aetherium.Model.Content`): creature
  definitions (stats, glyph/color, behavior preset, loot), item definitions (label/icon/weight,
  optional heal effect, optional weapon bonus), and a weighted spawn table.
- **`content.yaml`** becomes a conventional bundle section (or inline `content:` in `game.yaml`),
  loaded/validated exactly like the four existing sections, with cross-reference validation
  (spawn→creature, creature-loot→item, behavior→known preset, faction-doctrine `kill:` tags).
- **Placement/identity split**: population passes keep deciding *where and how many* monsters a
  map gets; when a world has a `ContentConfig`, each pass-placed monster is re-materialized from
  the weighted spawn table (deterministic per world seed) — stats, glyph, `CreatureTypeTag`,
  loot all from data. Worlds without content config behave exactly as today.
- **`SpawnEntityAsync`** prefers defined creature ids over the legacy hardcoded switch; monster
  loot drops resolve through the victim's creature definition instead of always `SwordItem`.
- **Per-world content atlas seed**: compiled creatures/items auto-register `EntityKindTag`s on a
  per-world `ContentAtlas`, taking the first step from the engine-wide `DefaultContentAtlas`
  toward per-game vocabulary.
- Sample bundles gain real content: Emberfall wolves and cult acolytes (making its `kill:wolf`
  doctrine live), Neonveil drones and NetWatch agents.

## Impact

- Affected specs: new capability `content-definitions`; extends `game-definitions` bundle format.
- Affected code: `Aetherium.Model` (new `Content/` + config threading), `Aetherium.Server`
  (loader/validator/mapper, new `Content/ContentCompiler`, `GameMapGrain` init/spawn/loot paths,
  `EntityFactory` snapshot round-trip), sample bundles under `Data/Games`, tests.
- No breaking changes: every new field is nullable/optional; a null `ContentConfig` preserves
  today's behavior bit-for-bit (legacy monster, sword loot, `M` glyph).
