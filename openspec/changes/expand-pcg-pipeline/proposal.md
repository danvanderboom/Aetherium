## Why
Agents need rich, coherent spaces to explore with meaningful obstacles and choices. Our current generation creates functional maps but lacks variety, interactivity, multi-level structure, validation rigor, and deep narrative cohesion.

## What Changes
- Overhaul generation pipeline into explicit phases (layout → theming → population → interactions → validation)
- Add advanced dungeon generation (varied rooms, loops, verticality, secrets, gating)
- Add outdoor generation (biomes, rivers, roads, cities/villages, wilderness)
- Add interactive challenges (keys/locks, puzzles, traps, destructibles, secrets)
- Add narrative-driven constraints and placement guarantees
- Add validation framework and metrics with regeneration/fallback
- Add deterministic seeds, debug tooling, and visualizers

## Impact
- Affected specs: pcg-core, pcg-dungeons, pcg-outdoor, pcg-interactives, pcg-narrative, pcg-validation, pcg-tooling
- Affected code: ConsoleGameServer/WorldGen, WorldBuilders, MapGeneratorRegistry, Perception/Navigation, Entities/Components (locks, keys, traps), tests under ConsoleGame.Test


