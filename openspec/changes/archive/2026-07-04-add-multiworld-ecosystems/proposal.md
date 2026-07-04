## Why
Multi-world infrastructure exists but lacks cross-world mechanics. Players need portals to travel between worlds, cluster-scoped economies for supply chains, cross-world quests, meta-progression unlocks, and hub worlds connecting procedural zones.

## What Changes
- Add PortalNetworkPass to world generation (places portal entities with link metadata)
- Create cluster system for economy scope (markets, trade routes, transport schedules spanning worlds in a cluster)
- Extend quest system for cross-world objectives (travel_to objectives referencing world/map tags)
- Add MetaProgressionGrain tracking discoveries and unlocks across worlds
- Support authored hub worlds (JSON assets) and procedural hub generation

## Impact
- Affected specs: `world-building`, `narrative`, `multiworld` (new), `meta-progression` (new)
- Affected code: `Aetherium.Server/WorldGen/Passes/`, `Aetherium.Server/MultiWorld/`, `Aetherium.Server/Narrative/`, `Aetherium.Server/Meta/`, `Aetherium.WorldGen/Components/`

