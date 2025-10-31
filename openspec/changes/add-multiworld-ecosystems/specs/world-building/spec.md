## ADDED Requirements

### Requirement: Portal Network Generation Pass
World generation SHALL include a PortalNetworkPass that places portal entities during the Interactions phase, connecting worlds within clusters via link metadata.

#### Scenario: Portal placement during generation
- **WHEN** PortalNetworkPass executes during world generation
- **THEN** it SHALL place PortalComponent entities at strategic locations (major landmarks, zone boundaries, or narrative points)
- **AND** each portal SHALL contain link hints (TargetWorldId, TargetMapId, TargetTag, or hub references)
- **AND** portal placement SHALL use deterministic RNG from GeneratorContext

#### Scenario: Portal metadata structure
- **WHEN** PortalNetworkPass places a portal
- **THEN** the PortalComponent SHALL include PortalId, optional target identifiers, and Activation requirements
- **AND** portals SHALL be registered with the cluster grain for runtime link resolution

