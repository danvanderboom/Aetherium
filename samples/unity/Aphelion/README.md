# Aphelion — Unity Sample Game

A co-op sci-fi salvage crawl through hibernating mega-stations at the cold end of a decades-long orbit. This is the Unity presentation half of the game; the meaning half will be the `Data/Games/aphelion/` YAML bundle. Full design: [docs/design/unity-sample/](../../../docs/design/unity-sample/README.md) (game design, art/audio direction, client library, milestones).

## Current state: project skeleton + asset slice

This is a **Unity 6 (6000.4) URP project skeleton** — openable in Unity, but there are no scenes or scripts yet (those arrive with the `com.aetherium.unity` client library per [milestone M0](../../../docs/design/unity-sample/milestones.md)). What's here now:

```
Assets/
├─ ThirdParty/Quaternius/     14 CC0 models — the full creature cast (Reclaimer, Scrap Mite,
│                             Custodian, Sentinel, Vent Lurker, Overseer), props, two planets
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

## Opening the project

1. Unity Hub → Add project from disk → this folder (Unity 6000.4.x; Hub offers an upgrade if you're newer).
2. First open: Unity resolves packages (glTFast imports the `.glb` files) and generates `Library/` (gitignored).
3. There's no scene to press Play in yet — browse the models/audio, or listen to the theme at `Assets/Audio/Music/aphelion-theme.wav`.

## What lands next (M0)

Connection to a live Aetherium server via the `com.aetherium.unity` package, the graybox station kit, movement/combat against the `aphelion` bundle, and the first beauty pass — tracked in [milestones.md](../../../docs/design/unity-sample/milestones.md).
