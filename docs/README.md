# Aetherium Documentation

Documentation for Aetherium - a multiplayer ASCII dungeon crawler with dynamic lighting, heat vision, and immersive gameplay.

## User Documentation

Documentation for players using the game clients.

### Console Client
📁 **[console/user/](console/user/)** - Console/Terminal client user guide

The console client is an ASCII-based terminal interface with rich features:
- Dynamic lighting (Torch, Sunlight, Infrared modes)
- Heat tracking and trail visualization  
- Day/night cycle with atmospheric effects
- Directional and omnidirectional vision
- Full keyboard controls

**Start here:** [Console Client Quick Reference](console/user/quick-reference.md)

### Future Clients
As new clients are developed, their documentation will be added here:
- 📁 `web/user/` - Web browser client (future)
- 📁 `mobile/user/` - Mobile app client (future)
- 📁 `gui/user/` - GUI desktop client (future)

## Developer Documentation

Documentation for developers working on the codebase.

### Agents & AI
📁 **[agents/](agents/)** - AI agent system documentation

The game features a comprehensive extensible agent tool system:
- **LLM-driven agents** using OpenAI-compatible APIs (phi-4 via LM Studio)
- **Heuristic fallback agents** with simple rule-based behavior
- **Extensible tool system** with 26+ discoverable tools
- **Profile-based access control** (Explorer, Player, WorldBuilder, etc.)
- **OpenAI function calling support** for advanced LLM integration
- **CLI tools** for agent and tool management
- **Rate limiting and error handling**

**Start here:** [Agent System Guide](agents/README.md)  
**Deep dive:** [Tool System Architecture](agents/TOOLS.md)

### Architecture & Design
- ✅ [Architecture Overview](architecture/overview.md) - System topology, protocol, data flow, configuration
- ✅ [Server Architecture](architecture/server.md) - Grains, simulation, ECS, perception, worldgen, agents, narrative
- ✅ [Client Architecture](architecture/clients.md) - Console, Unity, Dashboard, and planned Unreal clients
- ✅ [Tooling & Data](architecture/tooling-and-data.md) - aetherctl, WorldGenCLI, scripts, Data/ assets
- ✅ [Agent Tool System](agents/TOOLS.md) - Extensible tool architecture
- ✅ [Narrative Systems](narrative-systems.md) - Procedural storytelling and emergent narratives
- ✅ [Instance System](instances.md) - Dungeon instances, lockouts, and party support
- ✅ [Development Guide](development.md) - Developer setup, testing, and workflow
- ✅ [Multi-World Ecosystems](multiworld-ecosystems.md) - World clusters, portals, cross-world economy, meta-progression, and hub worlds
- ✅ [Monitoring Guide](monitoring.md) - Real-time game monitoring quick start

### Quality & Audits
- 📋 [Audit Index & Scorecard](audits/2026-07-03-initial-subsystem-audit/README.md) - Per-subsystem audit reports with build/test ground truth
- 📋 [Recommendations](audits/2026-07-03-initial-subsystem-audit/RECOMMENDATIONS.md) - Prioritized findings register
- 📋 [Design Analysis](audits/2026-07-03-initial-subsystem-audit/DESIGN_ANALYSIS.md) - High-level system design assessment
- 📋 [Improvement Plan](audits/2026-07-03-initial-subsystem-audit/IMPROVEMENT_PLAN.md) - Phased quality-improvement roadmap
- 🗄️ [Historical documents](history/README.md) - Archived point-in-time status reports

### SignalR Hubs

#### ManagementHub (`/managementHub`)
World management operations with Azure AD B2C authentication.

**Available Methods:**
- `Ping()`: Test connection
- `GetServerInfo()`: Get server status and world counts
- `ListWorlds()`: List all worlds
- `GetWorldInfo(string worldId)`: Get detailed world information
- `CreateWorld(CreateWorldRequest)`: Create a new world (Admin role required)
- `PauseWorld(string worldId)`: Pause a world (Admin role required)
- `ResumeWorld(string worldId)`: Resume a world (Admin role required)
- `Shutdown(string worldId)`: Shutdown a world (Admin role required)

**Client Usage:**
```csharp
using Aetherctl.SignalR;
using Aetherctl.Auth;

// Configure authentication
var authService = new AuthService(tenant, policy, clientId, scopes);
var token = await authService.AcquireTokenDeviceCodeAsync();

// Create client
await using var client = new ManagementClient(baseUrl, async () => token);
await client.ConnectAsync();

// Use methods
var worlds = await client.ListWorldsAsync();
var info = await client.GetWorldInfoAsync(worldId);
```

See [Development Guide - Unified CLI](development.md#unified-cli-aetherctl) for CLI usage examples.

### API Reference
- [SignalR Hubs](#signalr-hubs) - SignalR hub reference and usage
- **ManagementHub**: World management operations with B2C authentication
- **GameHub**: Gameplay communication between clients and server
- **AgentDashboardHub**: Agent telemetry and monitoring
- **REST APIs**:
  - `/api/cluster` - Cluster, portal, and economy management
  - `/api/metaprogression/{playerId}` - Meta-progression (discoveries, unlocks)
  - `/api/management/worlds` - World creation and management
- Coming soon: Game state DTOs reference

### Contributing
- See: [OpenSpec workflow](../openspec/AGENTS.md) for change proposals
- Coming soon: Contribution guidelines
- Coming soon: Code style guide

## Quick Navigation

### For Players
- **New to Console Client?** → [Console User Docs](console/user/README.md)
- **Need a quick reference?** → [Quick Reference](console/user/quick-reference.md)
- **Want to learn the game?** → [Gameplay Guide](console/user/gameplay.md)
- **Looking for a key?** → [Controls Guide](console/user/controls.md)

### For Developers
- **Getting started?** → [Development Guide](development.md)
- **Planning a change?** → [OpenSpec Agents Guide](../openspec/AGENTS.md)
- **Working with agents?** → [Agent System Guide](agents/README.md)
- **Exploring the code?** → Start with `Aetherium.Server/` and `Aetherium.Console/`
- **Running tests?** → [Development Guide - Testing](development.md#testing)

## Project Structure

```
docs/                              # Documentation (you are here)
├── README.md                      # This file
├── architecture/                  # System architecture (overview, server, clients, tooling)
├── audits/                        # Subsystem audits, recommendations, improvement plan
├── history/                       # Archived point-in-time status docs
├── clients/                       # Client guides (Unreal migration guide)
├── console/user/                  # Console client user guides
├── agents/                        # Agent system docs
├── training/                      # Agent training & benchmark docs
├── unity/                         # Unity client docs
├── monitoring.md                  # Monitoring quick start
└── development.md                 # Developer guide

Aetherium.Server/                  # Server: engine, grains, hubs
Aetherium.Model/                   # Shared DTOs
Aetherium.Console/                 # Terminal client
Aetherium.Unity/                   # Unity 2D client
Aetherium.Dashboard/               # Blazor ops/training dashboard
Aetherctl/                         # Operator CLI
WorldGenCLI/                       # PCG client library
Aetherium.Test/  Aetherctl.Test/   # Test suites
openspec/                          # Specifications & change proposals
```

## Documentation Standards

### User Documentation
- **Audience**: Players who want to play the game
- **Tone**: Friendly, instructive, example-driven
- **Format**: Markdown with tables, lists, and clear sections
- **Structure**: 
  - Quick reference for fast lookup
  - Detailed guides for learning
  - Progressive disclosure (beginner → advanced)

### Developer Documentation
- **Audience**: Developers contributing to the codebase
- **Tone**: Technical, precise, architectural
- **Format**: Markdown with code examples and diagrams
- **Structure**:
  - Architecture overviews
  - API references with signatures
  - Design decisions and rationale

## Contributing to Documentation

### For User Docs
1. Focus on player experience and clarity
2. Include practical examples and screenshots (when applicable)
3. Keep quick reference concise
4. Make guides progressively detailed
5. Test instructions by following them yourself

### For Developer Docs
1. Follow [OpenSpec workflow](../openspec/AGENTS.md) for major changes
2. Keep technical accuracy high
3. Include code examples
4. Document "why" not just "what"
5. Update docs when code changes

## Documentation Roadmap

### Planned User Documentation
- [ ] FAQ for common issues
- [ ] Video tutorial links (when available)
- [ ] Multiplayer etiquette guide
- [ ] Advanced strategies and tactics
- [ ] Modding guide (if supported)

### Planned Developer Documentation
- [x] Agent tool system architecture
- [x] Narrative systems guide
- [x] Development guide (testing, workflow, best practices)
- [x] System architecture overview (architecture/)
- [ ] ECS system guide
- [ ] Perception system deep dive
- [ ] Network protocol specification
- [ ] Performance optimization guide

## Getting Help

- **Gameplay questions?** See [user documentation](console/user/)
- **Technical issues?** Check project README or open an issue
- **Want to contribute?** See [OpenSpec workflow](../openspec/AGENTS.md)

---

**Last Updated:** 2026-07-03  
**Documentation Version:** 2.0  
**Game Version:** Compatible with current main branch


