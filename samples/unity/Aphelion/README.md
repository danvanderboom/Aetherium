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
5. **To play the `aphelion` bundle** instead of the default world, create an instance and
   paste its world id into the `AetheriumClientBehaviour` inspector field:
   ```powershell
   Invoke-RestMethod -Method Post "http://localhost:50310/api/management/games/aphelion/instances"
   ```

## What lands next (M0)

The graybox station kit replacing the primitive stand-ins, damage numbers, pickups/doors,
death/respawn UI, extraction + score screen, and the first beauty pass — tracked in
[milestones.md](../../../docs/design/unity-sample/milestones.md).
