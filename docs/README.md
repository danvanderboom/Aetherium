# Console Game Documentation

Documentation for Console Game - a multiplayer ASCII dungeon crawler with dynamic lighting, heat vision, and immersive gameplay.

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

### Architecture & Design
- Coming soon: System architecture overview
- Coming soon: Entity-Component-System (ECS) guide
- Coming soon: Client-Server communication protocol

### API Reference
- Coming soon: Server API documentation
- Coming soon: SignalR hub reference
- Coming soon: Game state DTOs

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
- **Planning a change?** → [OpenSpec Agents Guide](../openspec/AGENTS.md)
- **Exploring the code?** → Start with `ConsoleGameServer/` and `ConsoleGame/`
- **Running tests?** → See `ConsoleGame.Test/`

## Project Structure

```
ConsoleGame/
├── docs/                          # Documentation (you are here)
│   ├── README.md                  # This file
│   ├── console/                   # Console client docs
│   │   └── user/                  # User guides
│   │       ├── README.md          # Console docs index
│   │       ├── quick-reference.md # Fast lookup
│   │       ├── controls.md        # Key bindings
│   │       └── gameplay.md        # Mechanics & strategy
│   └── [future client docs]/
│
├── ConsoleGameServer/             # Server-side game logic
├── ConsoleGame/                   # Console client
├── ConsoleGameModel/              # Shared DTOs
├── ConsoleGame.Test/              # Tests
├── openspec/                      # Specifications
└── README.md                      # Project README
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
- [ ] Architecture overview
- [ ] ECS system guide
- [ ] Perception system deep dive
- [ ] Network protocol specification
- [ ] Performance optimization guide
- [ ] Testing strategy guide

## Getting Help

- **Gameplay questions?** See [user documentation](console/user/)
- **Technical issues?** Check project README or open an issue
- **Want to contribute?** See [OpenSpec workflow](../openspec/AGENTS.md)

---

**Last Updated:** October 2025  
**Documentation Version:** 1.0  
**Game Version:** Compatible with current master branch

