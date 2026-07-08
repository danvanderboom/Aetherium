> Status (2026-07-03): grain internals (portals, cluster economy, hub worlds, meta-progression) are genuinely wired, and multi-world travel now works end-to-end (JoinWorld + UsePortal). Verified gaps: travel_to quest objectives can never complete (no quest-activation API), cross-world quest flow has zero tests, world ACL is unenforced at join, and world Metadata/tags are never populated (tag matching degrades to first-world fallback).

## 1. Implementation
- [x] 1.1 Author OpenSpec deltas for world-building, narrative, multiworld, meta-progression
- [x] 1.2 Implement PortalNetworkPass and PortalComponent with placement rules
- [x] 1.3 Add PortalNetworkPass to both Outdoor and Dungeon pipelines
- [x] 1.4 Create IClusterGrain, ClusterGrain, and ClusterModels for economy
- [x] 1.5 Extend WorldConfig/WorldInfo for ClusterId and register maps/portals
- [x] 1.6 Resolve portal link targets within cluster and persist mappings
- [x] 1.7 Implement markets, trade routes, and transport schedules with ticking
- [x] 1.8 Add CrossWorldConstraint types and resolver; extend NarrativeGraphGenerator
- [ ] 1.9 Emit and evaluate travel_to objective; complete via NarrativeStateGrain events (unchecked 2026-07-03: ActiveQuestIds is never populated — no StartQuest/ActivateQuest API exists, so travel_to objectives can never complete; see docs/audits/2026-07-03-initial-subsystem-audit/narrative-and-multiworld.md)
- [x] 1.10 Implement MetaProgressionGrain and models; expose unlock queries
- [x] 1.11 Implement HubWorldLoader for Data/Hubs assets and map creation
- [x] 1.12 Add hub-world generator/passes and template resolution hook
- [x] 1.13 Add server APIs to manage clusters, portals, transports, and unlocks
- [ ] 1.14 Add unit/integration tests for portals, economy, cross-world quests, meta (unchecked 2026-07-03: portal/economy/meta grain tests exist, but cross-world quests have zero tests — NarrativeStateGrain, NarrativeConsequenceEngine, and CrossWorldConstraintResolver are untested)

