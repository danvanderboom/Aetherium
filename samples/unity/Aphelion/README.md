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
   It listens on `http://localhost:50310`.
3. **Open the project** (Unity Hub → this folder, Unity 6000.5.x) and run the menu item
   **Aetherium → Build First Light Scene**. It creates `Assets/Scenes/FirstLight.unity`,
   stand-in primitive prefabs, and a ThemeAsset — all rewireable in the Inspector.
4. **Press Play.** The client connects, joins the server's default session, and the maze
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
5. **To play the `aphelion` bundle** instead of the default world, create an instance and
   paste its world id into the `AetheriumClientBehaviour` inspector field:
   ```powershell
   Invoke-RestMethod -Method Post "http://localhost:50310/api/management/games/aphelion/instances"
   ```

## Overworld sandbox scene

This project also hosts the **Overworld** sample — a large open world (no monsters) that
exercises the procedural-generation, door/key, and window systems. Menu:
**Aetherium → Build Overworld Scene** creates `Assets/Scenes/Overworld.unity` with primitive
terrain stand-ins (plains, forest, desert, hills, mountains, water, roads, walls, floors, and
a **translucent window wall**), an `OverworldTheme`, and the client rig.

To run it:

1. Start the server (it auto-discovers the `overworld` bundle in `Data/Games/`), then create an
   instance and copy the returned world id:
   ```powershell
   Invoke-RestMethod -Method Post "http://localhost:50310/api/management/games/overworld/instances"
   ```
2. **Aetherium → Build Overworld Scene**, then paste the world id into the `AetheriumClient`
   rig's `worldId` field in the Inspector.
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
