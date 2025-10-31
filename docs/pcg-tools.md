# Procedural Generation Tooling

Use the `WorldGenCLI` application to reproduce deterministic worlds and export metrics, or start an HTTP API server for remote generation requests.

## CLI Usage

```bash
dotnet run --project WorldGenCLI -- \
  --generator AdvancedDungeon \
  --template dungeon \
  --width 60 --height 60 --levels 2 \
  --seed 12345 --version 2.0.0 \
  --param minLoopRatio=0.12 \
  --output artifacts/dungeon-12345.json
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
dotnet run --project WorldGenCLI -- --serve --port 5000
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

