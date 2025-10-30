# Game Data Assets

This directory contains sample game data assets for the Console Game procedural generation system.

## Directory Structure

```
Data/
├── Prefabs/          # Reusable building blocks for world generation
│   ├── Buildings/    # Architectural structures
│   ├── Terrain/      # Natural terrain features
│   └── README.md     # Prefab documentation
├── Narratives/       # Quest chains, loot tables, and NPC goals
│   └── README.md     # Narrative documentation
└── README.md         # This file
```

## Quick Start

### Loading a Narrative

```powershell
cd AgentCLI
dotnet run -- narrative load ../Data/Narratives/tutorial-village.json
dotnet run -- narrative show tutorial-village
```

### Creating a World with Narrative

```powershell
dotnet run -- world create "Tutorial World" "A beginner-friendly world" --narrative tutorial-village --width 100 --height 100
dotnet run -- world list
```

### Managing Worlds

```powershell
# List all worlds
dotnet run -- world list

# Get world info
dotnet run -- world info <world-id>

# Pause a world
dotnet run -- world pause <world-id>

# Resume a world
dotnet run -- world resume <world-id>

# Shutdown a world
dotnet run -- world shutdown <world-id>
```

## Asset Types

### Prefabs

Prefabs are reusable templates for buildings, terrain features, and other world elements. They can be:
- **Buildings**: Houses, shops, taverns, etc.
- **Terrain Features**: Forest clusters, ponds, rock formations
- **Dungeons**: Room templates, corridors, special chambers

See [Prefabs/README.md](Prefabs/README.md) for detailed documentation.

### Narratives

Narratives define the story, quests, and gameplay elements for a world:
- **Quests**: Objectives and rewards
- **Loot Tables**: Item drop probabilities
- **Monster Density**: Spawn rules per area
- **NPC Goals**: Character motivations and required assets

See [Narratives/README.md](Narratives/README.md) for detailed documentation.

## Sample Assets

### Included Prefabs

1. **small-house.json** - A simple 7x5 residential building
2. **shop.json** - A 9x7 commercial building with NPC spawn
3. **forest-cluster.json** - A 5x5 natural forest patch
4. **small-pond.json** - A 6x6 water feature

### Included Narratives

1. **tutorial-village.json** - Beginner tutorial with 3 quests
2. **dungeon-exploration.json** - Classic dungeon crawl with 3 quests

## Environment Configuration

Configure asset loading behavior with environment variables:

```powershell
# Prefab storage mode
$env:PREFAB_STORAGE = "file"           # Use file-based storage
$env:PREFAB_PATH = "./Data/Prefabs"    # Path to prefabs directory

# Orleans storage mode (for narratives and worlds)
$env:ORLEANS_STORAGE = "memory"        # Use in-memory storage (dev)
# $env:ORLEANS_STORAGE = "azure"       # Use Azure Table Storage (production)
```

## Creating Custom Assets

### Custom Prefabs

1. Copy an existing prefab as a template
2. Modify the dimensions, tiles, and metadata
3. Save with a unique PrefabId
4. Place in the appropriate category folder

### Custom Narratives

1. Start with `tutorial-village.json` as a template
2. Define your quests with clear objectives
3. Create loot tables for rewards and drops
4. Set monster density rules for different areas
5. Define NPC goals and required assets
6. Save with a unique NarrativeId

## Asset Validation

When loading assets, the system validates:
- JSON schema correctness
- Required fields presence
- ID uniqueness
- Reference integrity (prerequisite quests exist, etc.)

Check console output for validation errors when loading.

## Best Practices

1. **Version Control**: Keep your custom assets in version control
2. **Testing**: Load and test each asset before deploying
3. **Documentation**: Add clear descriptions to metadata
4. **Naming**: Use consistent naming conventions
5. **Modularity**: Design prefabs to work well together
6. **Balance**: Test quest difficulty and loot drop rates
7. **Narratives**: Create logical quest chains with clear progression

## Integration with World Generation

### Procedural Generators

Generators can use prefabs and narratives:

```csharp
// Generator receives narrative context
var context = new GeneratorContext(width, height)
{
    NarrativeId = "tutorial-village",
    NarrativeHints = new Dictionary<string, object>
    {
        { "PreferBuildings", true },
        { "MinimumShops", 2 }
    }
};

// Generator can query narrative for spawn rules
var narrative = grainFactory.GetGrain<INarrativeGrain>(context.NarrativeId);
var monsterRules = await narrative.GetMonsterDensityRulesAsync();

// Apply monster spawning based on rules
foreach (var rule in monsterRules)
{
    // Spawn monsters according to density and level
}
```

### Prefab Stamping

Use PrefabStamper to place prefabs in the world:

```csharp
var prefab = await prefabLibrary.GetPrefabAsync("building-small-house-01");
var stamper = new PrefabStamper();

stamper.Stamp(world, prefab, startX: 10, startY: 20);
```

## Troubleshooting

### "Narrative not found"
- Ensure the narrative was loaded first: `narrative load <file>`
- Check the NarrativeId matches exactly

### "Prefab library empty"
- Set `PREFAB_PATH` environment variable
- Verify JSON files are valid
- Check console for parsing errors

### "World creation failed"
- Ensure Orleans is running
- Check generator type is valid
- Verify narrative exists if specified

## Additional Resources

- [Prefab Format Documentation](Prefabs/README.md)
- [Narrative Format Documentation](Narratives/README.md)
- [World Generation Guide](../openspec/specs/world-generation/spec.md) (if available)
- [CLI Command Reference](../AgentCLI/README.md) (if available)

## Contributing

When creating new sample assets:
1. Follow existing naming conventions
2. Add comprehensive metadata
3. Update README files with examples
4. Test thoroughly before committing
5. Include clear descriptions and comments

