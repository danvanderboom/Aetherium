## Why
Players and AI agents need to pick up, carry, use, and drop items. Doors and other objects should change state (open/close/lock) based on interactions (e.g., using keys). Interactions must be exposed via client/server actions and perception frames friendly to AI planners.

## What Changes
- Add per-character inventory (10 slots), items (keys), and interaction actions (pickup/drop/use/open/close)
- Add flexible key→door matching via string IDs (colors, codes)
- Extend perception with affordances and door state visibility
- Render items and door states in the console client

## Impact
- Affected specs: client-server-communication, world-entities, perception, rendering
- Affected code: Aetherium.Server (components/entities/systems, hub), Aetherium.Model (DTOs), Aetherium (views, client)


