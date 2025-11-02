# Procedural Generation Tooling

Use the `aetherctl` CLI tool to reproduce deterministic worlds and export metrics, or start an HTTP API server for remote generation requests.

## CLI Usage

```bash
# Generate a world map
aetherctl worldgen generate \
  --generator AdvancedDungeon \
  --template dungeon \
  --width 60 --height 60 --levels 2 \
  --seed 12345 --version 2.0.0 \
  --param minLoopRatio=0.12 \
  --output artifacts/dungeon-12345.json

# Or with JSON output
aetherctl worldgen generate --generator AdvancedDungeon --json

# Render world preview as ASCII
aetherctl worldgen render --template dungeon --width 60 --height 60 --ascii

# Render world preview as PNG
aetherctl worldgen render --template dungeon --width 60 --height 60 --png output.png
```

Key options:

- `--generator`: layout generator name (e.g., `AdvancedDungeon`, `AdvancedOutdoor`).
- `--template`: `dungeon` or `outdoor` to select pipeline passes.
- `--seed`: deterministic seed (omit for random seed).
- `--param`: additional generator parameters (`key=value`). Repeat for multiple values.
- `--output`: optional path to write metrics JSON.

The JSON export contains branching factor, loop ratio, room counts, biome coverage, and per-phase timings for regression tracking.

## REST API Server

Start the API server for remote generation requests:

```bash
aetherctl worldgen serve --port 5000
```

The server exposes the following endpoints:

### Generators

- `GET /api/generators`: List all available generators
- `GET /api/generators/{id}/constraints-schema`: Get parameter schema for a generator

Example:
```bash
curl http://localhost:5000/api/generators
curl http://localhost:5000/api/generators/AdvancedDungeon/constraints-schema
```

### Templates

- `GET /api/templates`: List all saved templates
- `GET /api/templates/{name}`: Load a specific template
- `POST /api/templates`: Save a template (body: TemplateDto JSON)
- `DELETE /api/templates/{name}`: Delete a template

Example:
```bash
curl http://localhost:5000/api/templates
curl -X POST http://localhost:5000/api/templates -H "Content-Type: application/json" -d '{"name":"my-template","generatorId":"AdvancedDungeon",...}'
```

### Generation

- `POST /api/generate`: Generate a world (body: GenerateRequest JSON)
- `POST /api/generate/abtest`: Generate multiple candidates for A/B testing (body: AbTestRequest JSON)

Example:
```bash
curl -X POST http://localhost:5000/api/generate \
  -H "Content-Type: application/json" \
  -d '{"layoutGenerator":"AdvancedDungeon","template":"dungeon","width":60,"height":60,"levels":1}'
```

### PCG Editor

The Dashboard includes a PCG Editor UI at `/pcg` that provides:
- Visual constraint editor with live preview
- Template save/load
- Hybrid anchor placement
- A/B testing with metric sorting
- Real-time map visualization with overlay toggles

Access the editor by navigating to the PCG Editor page in the Dashboard after starting both the API server and Dashboard.

## World Building Tool Integration

Feature builders can execute agent tools during world generation, providing consistent validation and error handling with runtime operations.

### Using Tools in Feature Builders

When a `WorldFeatureBuilder` has access to `AgentToolRegistry` and `IServiceProvider`, it can execute tools during `Build()`:

```csharp
public class CustomFeatureBuilder : WorldFeatureBuilder
{
    public override void Build()
    {
        // Execute SetTerrainTool during world generation
        ExecuteTool("setterrain", new Dictionary<string, object>
        {
            ["x"] = 10,
            ["y"] = 20,
            ["z"] = 0,
            ["terrainType"] = "Forest"
        });
    }
}
```

### Available World Building Tools

- **`setterrain`** - Set terrain type at coordinates (fully implemented)
- **`moveentity`** - Move entities to new locations (fully implemented)
- **`destroyentity`** - Remove entities from world (fully implemented)
- **`spawnentity`** - Create entities at coordinates (requires entity factory/prefab system)
- **`modifyentity`** - Modify entity properties (requires component system knowledge)

### Example: TorusFeatureBuilder

The `TorusFeatureBuilder` demonstrates tool integration by using `SetTerrainTool` for underground terrain placement when tools are available:

```csharp
if (location.Z < 0) // underground levels
{
    if (InsideTorus(location, axis, majorRadius, minorRadius))
    {
        // Use SetTerrainTool if registry/provider available
        if (ToolRegistry != null && ServiceProvider != null)
        {
            ExecuteTool("setterrain", new Dictionary<string, object>
            {
                ["x"] = location.X,
                ["y"] = location.Y,
                ["z"] = location.Z,
                ["terrainType"] = "Indoors"
            });
        }
        else
        {
            // Fall back to direct World manipulation
            World.SetTerrain("Indoors", location);
        }
    }
}
```

### Benefits

- **Consistency**: Same validation and error handling as runtime operations
- **Testability**: Tools can be tested independently
- **Composability**: Feature builders can compose tools for complex features
- **Backward Compatibility**: Direct `World` manipulation still works when tools aren't available

