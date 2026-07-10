# Design: Content Definitions

## Context

Code survey (2026-07-10) of how content enters a world today:

- **Creature identity is a constructor.** `Monster(world)` sets `Health(30,30)`,
  `AttackPower(6)`, `ActionSpeed(1,1)`, `Tile` (glyph `M`, DarkRed), `HeatSignature`, `Memory`.
  `Zombie`/`Snake` are variations. `CreatureTypeTag` (added by wire-factions-live) already
  carries a data identity string ("wolf", "bandit") on spawned entities, and the faction loop
  consumes it as `kill:<creature-type>` — the identity plumbing exists; only the *source* of
  identity is still code.
- **Placement is engine logic.** `DungeonPopulationPass`/`OutdoorPopulationPass` choose
  candidate tiles (passable, off primary path), a density (`enemyCount` param or 1-per-50
  tiles), and call `new Monster(world)`. `GameMapGrain.SpawnEntityAsync` maps creature-type
  strings through a hardcoded switch (`"wolf" => new Monster(...)`).
- **Items are component bags.** `Item` base + `Carriable {Label, Icon, Weight}` + optional
  `Consumable {EffectType, EffectValue, Uses}` + optional `Weapon(name, bonus)`. Monster loot
  is always `new SwordItem()` (`SpawnMonsterLoot`).
- **Behavior is one preset.** `MonsterBehaviors.BuildWanderAndMeleeTree` is assigned to every
  monster in `StepNpcsAsync`; the tree operates on components (`Health`, `WorldLocation`,
  `ActionSpeed`), not concrete types, except a `ctx.Self is not Monster` guard in the wander leaf.
- **Snapshot hydration** (`EntityFactory`) reconstructs entities by type name and re-applies a
  known property set (`HealthLevel`, …). `CreatureTypeTag` is not captured today.

## Goals / Non-Goals

**Goals**
- Creatures, items, and spawn mix defined per game in YAML; zero engine code changes to add a
  new creature to a game.
- Exact legacy behavior when no `ContentConfig` is present.
- Deterministic content assignment per world seed (same seed → same wolves in the same places).
- Survive grain reactivation: a re-hydrated world keeps its data-driven creatures.

**Non-Goals**
- New component kinds or combat semantics — definitions bind only to components that exist
  today (`Health`, `AttackPower`, `ActionSpeed`, `Carriable`, `Consumable`, `Weapon`).
- Data-driven *placement* (spawn regions, room-type rules, item placement on the map) — the
  passes' placement logic is untouched; defined items enter play as loot this slice.
- New behavior trees — `wander-melee` is the only preset; the field exists so games can name a
  preset and validation can catch typos, and richer presets/ECA graduation come later.
- Terrain/material/audio atlas sections in YAML — only entity kinds are auto-registered this
  slice.

## Decisions

### D1. Placement/identity split — re-skin, don't re-place

Population passes keep full ownership of *where and how many*. After generation,
`GameMapGrain.InitializeAsync` walks the pass-placed `Monster` entities and, when the world has
a compiled content catalog with a non-empty spawn table, re-materializes each one from a
weighted draw: overwrite `Health`, `AttackPower`, `ActionSpeed`, `Tile` (a per-creature
`TileType` registered in `world.TileTypes`), and set `CreatureTypeTag = creature id`.

*Why not thread the catalog into the worldgen pipeline?* The pipeline's contract
(`WorldGenerationRequest`, string params, static pass catalog) is deliberately config-blind and
shared with benchmarks/tools. Re-skinning keeps the pipeline signature stable, keeps
placement deterministic and identical across games (same seed → same layout), and puts the
data-binding at the same tier as every other config (`ApplyAbilityConfig` et al.).

The draw uses a `Random` seeded from the world's generation seed, applied in deterministic
entity order, so a given `(seed, spawn table)` always produces the same creature mix.

### D2. A defined creature IS a `Monster` (component overrides, not a new class)

Data-driven creatures are `Monster` instances with components overwritten from the definition.
Everything downstream — behavior trees, `Dying`/`Corpse` lifecycle, threat, faction
`kill:<id>` tags, `OfType<Monster>()` sweeps, `EntityFactory` hydration — keeps working with
zero changes. A future slice can introduce a generic `DataCreature : Character` if `Monster`
accretes unwanted legacy; today it is the cheapest correct carrier.

### D3. `CreatureDefinition` shape (YAML = camelCase of the POCO, like every config)

```yaml
creatures:
  - id: wolf
    name: Wolf
    description: A grey-pelted pack hunter.
    glyph: w
    color: Gray          # ConsoleColor name, validated
    health: 20
    attackPower: 4
    speed: 1.25          # ActionSpeed speed (maxBudget = 1.0, cost model unchanged)
    behavior: wander-melee   # must name a known preset
    lootItemId: wolf_pelt    # optional; null = no drop
```

### D4. `ItemDefinition` binds to the existing item component set

```yaml
items:
  - id: healing_salve
    name: Healing Salve
    icon: "+"
    weight: 1
    heal: { amount: 25, uses: 1 }   # optional → Consumable(HealthRestore)
  - id: ember_blade
    name: Ember Blade
    icon: "/"
    weight: 3
    weaponBonus: 7                  # optional → Weapon(name, bonus)
```

Runtime materialization is a plain `Item` with `Carriable` relabeled and the optional
components attached — identical shape to `HealthRestorativeItem`/`SwordItem`, minus the class.

### D5. Spawn table = weighted mix, not placement

```yaml
spawns:
  - creatureId: wolf
    weight: 3
  - creatureId: cult_acolyte
    weight: 1
```

Weights choose *which* creature fills each slot the passes created. `enemyCount`/density remain
generator parameters (already per-game via `world.generatorParameters`).

### D6. Loot resolves victim → definition → item, with legacy fallback

`SpawnMonsterLoot` gains the victim: if the victim's `CreatureTypeTag` resolves to a defined
creature with a `lootItemId`, materialize that item; a defined creature with null `lootItemId`
drops nothing; no catalog (or unresolvable tag) → legacy `SwordItem`, preserving today's
kill→sword→hit-harder loop for undefined worlds.

### D7. Reactivation: capture `CreatureTypeTag` in snapshots, re-skin on hydrate

`EntityFactory.ExtractProperties`/`ApplyProperties` round-trip `CreatureType`. On grain
reactivation (`OnActivateAsync` hydration path), after entities are rebuilt,
re-apply each tagged entity's definition from the catalog (compiled from persisted
`MapState.ContentConfig`) so glyphs/stats survive without persisting every component.
Captured `HealthLevel` is re-applied *after* the re-skin so current damage is not reset.

### D8. Threading follows the established recipe verbatim

`GameDefinition.Content` (+ `content.yaml` sibling) → `CreateWorldRequest.ContentConfig` →
`WorldTemplate.ContentConfig` → `WorldConfig.ContentConfig` → `WorldGrain` state →
`IGameMapGrain.InitializeAsync(..., contentConfig)` → `MapState.ContentConfig` +
`ApplyContentConfig` (compile at bind time, like abilities/factions/progression).

### D9. Validation (extends `GameDefinitionValidator`)

Errors: duplicate creature/item ids; spawn entry referencing unknown creature; `lootItemId`
referencing unknown item; unknown behavior preset; unparsable `color`; non-positive
`health`/`weight`; empty `glyph`. Warning: a faction doctrine `kill:<x>` delta where `<x>`
matches no defined creature id *and* the bundle defines content (typo detector — `kill:player`
and undefined worlds stay silent).

### D10. Per-world atlas: entity kinds only

`ContentCompiler` builds a per-world `Model.ContentAtlas.ContentAtlas` seeded from
`DefaultContentAtlas` plus one `EntityKindTag` per defined creature/item. Stored on the
catalog; nothing live consumes it yet (same phase-1 status as `DefaultContentAtlas`), but the
per-game vocabulary now exists where renderer binding expects to find it.

## Risks / Trade-offs

- **Re-skin runs after passes** → a pass that counted "monsters" for validation sees generic
  monsters. Acceptable: no pass inspects monster identity today.
- **`ConsoleColor` in a renderer-agnostic definition** is a pragmatic tie to the one shipped
  renderer; the atlas `EntityKindTag` is the forward-looking identity. Revisit when a second
  renderer binds.
- **Speed overrides** interact with the AP budget (`maxBudget` stays 1.0): `speed > 1` acts
  more often than baseline, `< 1` less — same semantics the ActionSpeed slice established.

## Migration

None. All fields optional; absent `ContentConfig` → bit-identical legacy behavior.

## Open Questions

- Should treasure placement (`PlaceTreasure`'s restorative/lantern pair) also draw from item
  definitions? Deferred — placement stays engine logic this slice; revisit with spawn regions.
- Multi-tag creatures (a wolf that is also `beast` for doctrine purposes) — deferred until a
  doctrine needs it; `CreatureTypeTag` stays single-valued.
