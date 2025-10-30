# Prefab Assets

Prefabs are reusable building blocks for procedural world generation. They can represent buildings, terrain features, or any other repeatable world elements.

## Directory Structure

```
Data/Prefabs/
├── Buildings/     # Architectural structures (houses, shops, etc.)
└── Terrain/       # Natural features (forests, ponds, etc.)
```

## Prefab JSON Format

### Required Fields

- **PrefabId** (string): Unique identifier for the prefab
- **Name** (string): Human-readable name
- **Category** (string): Category type (e.g., "building", "terrain")
- **Width** (int): Width in tiles
- **Height** (int): Height in tiles
- **Metadata** (object): Additional information and tags
- **Tiles** (2D array): The actual tile data

### Tile Format

Each tile in the `Tiles` array can have:

- **TerrainType** (string): The terrain type (e.g., "Wall", "Floor", "Grass", "Water", "Forest")
- **EntityType** (string, optional): Type of entity to spawn (e.g., "Door", "NPC")
- **EntityConfig** (object, optional): Configuration for the spawned entity

### Metadata Fields

- **Tags** (string): Comma-separated tags for searching/filtering
- **BiomeCompatibility** (string): Comma-separated list of compatible biomes
- **Description** (string): Human-readable description

## Example: Small House

```json
{
  "PrefabId": "building-small-house-01",
  "Name": "Small House",
  "Category": "building",
  "Width": 7,
  "Height": 5,
  "Metadata": {
    "Tags": "residential,small,entrance-south",
    "BiomeCompatibility": "plains,forest,city",
    "Description": "A simple rectangular house with a door on the south side"
  },
  "Tiles": [
    [
      { "TerrainType": "Wall" },
      { "TerrainType": "Wall" },
      { "TerrainType": "Wall" }
    ],
    [
      { "TerrainType": "Wall" },
      { "TerrainType": "Floor" },
      { "TerrainType": "Wall" }
    ],
    [
      { "TerrainType": "Wall" },
      { "TerrainType": "Door", "EntityType": "Door", "EntityConfig": { "IsOpen": false } },
      { "TerrainType": "Wall" }
    ]
  ]
}
```

## Example: Forest Cluster

```json
{
  "PrefabId": "terrain-forest-cluster-01",
  "Name": "Forest Cluster",
  "Category": "terrain",
  "Width": 5,
  "Height": 5,
  "Metadata": {
    "Tags": "natural,forest,organic",
    "BiomeCompatibility": "plains,forest",
    "Description": "A small cluster of trees"
  },
  "Tiles": [
    [
      { "TerrainType": "Grass" },
      { "TerrainType": "Forest" },
      { "TerrainType": "Grass" }
    ],
    [
      { "TerrainType": "Forest" },
      { "TerrainType": "Forest" },
      { "TerrainType": "Forest" }
    ],
    [
      { "TerrainType": "Grass" },
      { "TerrainType": "Forest" },
      { "TerrainType": "Grass" }
    ]
  ]
}
```

## Usage

### CLI Commands

Load prefabs into the server:

```powershell
# The PrefabLibrary will automatically scan the Data/Prefabs directory
# when configured with file storage mode

# To use prefabs in world generation, specify them in generator parameters
```

### In Code

```csharp
// Access prefab library
var prefabLibrary = serviceProvider.GetRequiredService<PrefabLibrary>();

// Load prefabs from directory
prefabLibrary.LoadFromDirectory("./Data/Prefabs");

// Get a specific prefab
var house = await prefabLibrary.GetPrefabAsync("building-small-house-01");

// Search by category
var buildings = prefabLibrary.GetByCategory("building");

// Search by tags
var residentialBuildings = prefabLibrary.SearchPrefabs(
    category: "building",
    tags: new List<string> { "residential" }
);
```

## Best Practices

1. **Naming Convention**: Use `category-description-number` format for PrefabIds
2. **Tagging**: Add descriptive tags for easy searching
3. **Biome Compatibility**: Specify which biomes the prefab fits naturally
4. **Entrances**: Tag building prefabs with entrance direction (e.g., "entrance-south")
5. **Size**: Keep prefabs reasonably sized (most buildings < 20x20)
6. **Modularity**: Design prefabs to work well when placed together

## Common Terrain Types

- **Wall**: Impassable solid wall
- **Floor**: Interior floor
- **Door**: Passable door (can be open/closed/locked)
- **Grass**: Basic ground terrain
- **Water**: Water terrain (impassable without swimming)
- **Forest**: Tree/forest terrain (partially blocks vision)
- **Stone**: Stone ground
- **Road**: Paved road

## Entity Types

When spawning entities on tiles:

- **Door**: Openable/closable barrier
- **NPC**: Non-player character (specify Role in config)
- **Item**: Spawns an item
- **Spawn**: Spawn point for monsters/players
- **Chest**: Container with loot

## Environment Variables

Configure prefab storage mode:

```powershell
$env:PREFAB_STORAGE = "file"           # Use file storage
$env:PREFAB_PATH = "./Data/Prefabs"    # Path to prefabs directory
```

