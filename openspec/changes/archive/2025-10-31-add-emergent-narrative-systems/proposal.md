## Why
The current narrative system is constraint-based and static. Adding procedural storytelling capabilities will enable dynamic quest generation, environmental storytelling, NPC relationship networks, and consequence propagation that creates emergent narrative experiences.

## What Changes
- Add procedural quest generation with dependency chains (fetch/rescue/defend)
- Add environmental storytelling (ruins with history, abandoned camps with clues)
- Add NPC relationship networks that influence dialogue and quests
- Add consequence propagation engine that generates follow-up quests from player actions
- Add lore fragment generation with consistent historical flavor text
- Add hybrid narrative state persistence (shared or per-world)
- Add deterministic seeding for reproducible narrative generation
- Extend world generation with environmental story pass

## Impact
- Affected specs: narrative (new), world-building (modified)
- Affected code: Aetherium.Server/Narrative, Aetherium.Server/WorldGen, Aetherium.Server/MultiWorld, Aetherium.Server/GameHub

