# Aphelion — Unity Sample Game

A co-op sci-fi salvage crawl through hibernating mega-stations at the cold end of a decades-long orbit. This is the Unity presentation half of the game; the meaning half will be the `Data/Games/aphelion/` YAML bundle. Full design: [docs/design/unity-sample/](../../../docs/design/unity-sample/README.md) (game design, art/audio direction, client library, milestones).

## Current state: First Light wiring + asset slice

A **Unity 6 (6000.5) URP project** referencing the [`com.aetherium.unity`](../../../clients/unity/com.aetherium.unity/) package, with a one-click scene bootstrap. What's here now:

```
Assets/
├─ Editor/                    AphelionSceneBootstrap — menu "Aetherium → Build First Light Scene"
│                             (stand-in prefabs, ThemeAsset, wired scene; safe to re-run)
├─ Scripts/                   AphelionPlayerController (WASD + Space attack),
│                             AphelionCameraRig (follows the perception anchor)
├─ ThirdParty/Quaternius/     14 CC0 models — the full creature cast (Reclaimer, Scrap Mite,
│                             Custodian, Sentinel, Vent Lurker, Overseer), props, two planets
├─ ThirdParty/Quaternius/Animated/  4 CC0 rigged characters with full skeletal clip sets —
│                             two 18-clip astronaut player skins, the Overseer mech
│                             (Walk/Run/Shoot/Death), the flying Vent Lurker
├─ ThirdParty/Kenney/         18 CC0 sci-fi sounds — doors, lasers, impacts, explosions,
│                             force fields, reactor hum (mapped to game events)
└─ Audio/                     Music + body SFX synthesized by Tools~/AudioGen — the Aphelion
                              theme and the explore/tension/combat adaptive stems (D minor, 90 BPM)
Packages/manifest.json        URP 17.4, Input System, glTFast (imports the .glb models),
                              Newtonsoft JSON, test framework
Tools~/AudioGen/              Deterministic C# synthesizer for everything in Assets/Audio
                              (dotnet run -- ../../Assets/Audio to regenerate)
```

Licensing: **everything committed is CC0 or generated in-repo** — per-asset provenance in [ATTRIBUTIONS.md](ATTRIBUTIONS.md).

## Running First Light

1. **Vendor the client DLLs** (once, and after any `Aetherium.Client` change) — from the repo root:
   ```powershell
   .\scripts\pack-unity-client.ps1
   ```
2. **Start the server** — from the repo root:
   ```powershell
   dotnet run --project Aetherium.Server
   ```
   It listens on `http://localhost:5000` (and `https://localhost:5001`), and auto-discovers
   the game bundles in `Data/Games/`.
3. **Create a world to join.** The client resolves a *running* instance from the lobby, so
   create one first — for the square station:
   ```powershell
   dotnet run --project Aetherctl -- game create aphelion
   ```
   (For the spherical planet, create `aphelion-h3` instead and use the planet scene below.)
4. **Open the project** (Unity Hub → this folder, Unity 6000.5.x) and run the menu item
   **Aetherium → Build First Light Scene**. It creates `Assets/Scenes/FirstLight.unity`,
   stand-in primitive prefabs, and a station ThemeAsset — all rewireable in the Inspector. The
   rig's `AetheriumClientBehaviour` is bootstrapped with `serverUrl = http://localhost:5000` and
   `joinGameDefinitionId = aphelion`, so on Play it lists the lobby and joins the newest running
   `aphelion` station — no world GUID to paste. (Clear that field and paste a `worldId` to pin one
   specific instance.)
5. **Press Play.** The client connects, resolves the station from the lobby, and the maze
   reveals around your cyan avatar as you explore. Controls:
   - **WASD** — move by compass (the client composes the rotate-then-step the server requires)
   - **← / →** — turn 90°; the map sweeps to keep your heading up-screen
   - **↑ / ↓** — step forward / backward along your heading (the engine's native verbs)
   - **Space** — attack an adjacent creature
   - **L** — toggle the suit lamp vs. debug sunlight (sight is gated by light, range ~6 cells)

   Creatures render as their own Quaternius models (scrap-mite, custodian, sentinel,
   vent-lurker, overseer-node), each falling back to a distinct-colored capsule if a model
   has not imported. Remembered-but-out-of-view cells dim; anything without a binding
   renders as a bright magenta capsule (loud beats invisible).

   **Creature memory:** a creature that leaves your view (you turned away, or it slipped
   past the lamp) lingers as a last-seen impression: the model stays where you saw it and
   dims, while a translucent circle in the creature's color — starting clear and sharp at
   the creature's own size — expands smoothly on the floor around it, about one cell of
   radius per second, dispersing from a crisp disc into a soft diffuse cloud as it grows:
   a probability waveform spreading as its position grows uncertain. Turn back and you SEE
   the memory (the circle means "somewhere in here", so an empty spot doesn't erase it);
   re-spotting the actual creature replaces its ghost instantly, and after ~5 seconds the
   impression fades out on its own. Tune `ghostSeconds` / `ghostSpreadCellsPerSecond` /
   `ghostGlowStartCells` / `ghostGlowOpacity` on the EntityViewRegistry component live in
   Play mode to taste. Vision is directional (a 120° forward arc from `game.yaml`
   `player.vision`), so minding your back — and remembering what was behind you — matters.
6. **To pin one specific instance** instead of the newest-of-a-bundle lobby pick, clear
   `joinGameDefinitionId` and paste a world id into the rig's `worldId` field. List running
   worlds with `dotnet run --project Aetherctl -- game instances aphelion`.

## Aphelion Prime — the H3 planet

The spherical companion to the station: the whole world the crew orbits, a resolution-4 H3 sphere
(~288k hex/pentagon cells) of oceans, forests, deserts, hills and mountains, threaded with rivers,
roads, rail, and 320 tiered settlements (`Data/Games/aphelion-h3/`, design in
[docs/design/h3-sphere-worldgen.md](../../../docs/design/h3-sphere-worldgen.md)). It has its own
scene + biome ThemeAsset — the station's Wall/Door theme doesn't cover planet terrain.

1. Vendor the DLLs and start the server as above, then create a planet instance:
   ```powershell
   dotnet run --project Aetherctl -- game create aphelion-h3
   ```
2. Menu **Aetherium → Build Aphelion Planet (H3) Scene**. It creates
   `Assets/Scenes/AphelionPlanet.unity` and `AphelionPlanetTheme.asset`, whose terrain bindings
   cover the planet's full vocabulary — **water, plains, forest, desert, hills, mountains, plus the
   transport layers road / rail / subway and the settlement tiles wall / indoor floor / window
   wall**. The rig is wired to `joinGameDefinitionId = aphelion-h3`.
3. **Press Play.** The client joins the newest `aphelion-h3` world and the planet reveals around
   you. Terrain renders as **hexagonal prisms** — a pointy-top hex of circumradius 1/√3 tessellates
   the axial layout `GridCellLayout` uses for `"h3"`, so tiles meet edge-to-edge (no overlap, so no
   z-fighting), grounds flush at the walkable plane and mountains/walls rising as taller hex columns.
   (The smooth `RoundedRegionRenderer` water/biome meshes are square-topology-only, so the planet
   uses these hex tiles instead.) Vision here is **360°** (from the bundle's `player.vision`), and
   it's a calm exploration/economy sandbox, so this scene uses the open-world controls:
   - **WASD** — move by compass; **← / →** turn; **↑ / ↓** step along your heading
   - **E** — interact: pick up an adjacent item, open/close or unlock an adjacent door
   - **L** — toggle daylight vs. carried lamp

   Stand in a settlement and you can trade: the server exposes a `market` tool (quote/buy/sell)
   and grants a joining player a wallet. The Unity sample doesn't surface a market UI yet — drive
   it from `aetherctl` (`tools test market …`) or wire a panel to `Client.Tools.ExecuteToolAsync("market", …)`.

   > **Tuning the look.** Every binding is editable on `AphelionPlanetTheme.asset` in the Inspector
   > (biome colors live on the `PL_Rounded*` materials; re-run the menu item to regenerate from the
   > palette in `AphelionPlanetSceneBootstrap.cs`). Unknown terrain falls back to the Plains ground
   > slab, never magenta.

## Overworld sandbox scene

This project also hosts the **Overworld** sample — a large open world (no monsters) that
exercises the procedural-generation, door/key, and window systems. Menu:
**Aetherium → Build Overworld Scene** creates `Assets/Scenes/Overworld.unity` with primitive
terrain stand-ins (plains, forest, desert, hills, mountains, water, roads, walls, floors, and
a **translucent window wall**), an `OverworldTheme`, and the client rig.

To run it:

1. Start the server (it auto-discovers the `overworld` bundle in `Data/Games/`), then create an
   instance:
   ```powershell
   dotnet run --project Aetherctl -- game create overworld
   ```
2. **Aetherium → Build Overworld Scene**, then set the `AetheriumClient` rig's
   `joinGameDefinitionId` to `overworld` in the Inspector (or paste a specific `worldId`).
3. Press **Play.** The world defaults to **daylight** (this is a sunlit sandbox). Controls:
   - **WASD** — move by compass; **← / →** turn; **↑ / ↓** step along your heading
   - **E** — interact: pick up an adjacent item (e.g. a key), open/close an adjacent door, or
     unlock a locked door with a key you're carrying
   - **L** — toggle daylight vs. the carried lamp (interiors are lit through windows and the
     door you came in by; making walls truly shadow the sun is the next lighting pass)

   Three cities (a grid capital, an organic town, a sparse outpost) sit in the wilderness,
   joined by roads. One building in the capital is locked — its brass key is on the street
   nearby. Windows are the pale cyan panes you can see through but not walk through.

## What lands next (M0)

The graybox station kit replacing the primitive stand-ins, damage numbers, pickups/doors,
death/respawn UI, extraction + score screen, and the first beauty pass — tracked in
[milestones.md](../../../docs/design/unity-sample/milestones.md).
