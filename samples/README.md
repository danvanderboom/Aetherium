# Aetherium Sample Games

Sample games demonstrating the engine across renderers. Each sample is **presentation only** — its game meaning lives in a YAML bundle under [`Data/Games/`](../Data/Games/), served by the same Aetherium server. See the [design suite](../docs/design/unity-sample/README.md) for the architecture.

| Sample | Engine | Status | Game bundle |
|---|---|---|---|
| [Aphelion](unity/Aphelion/) | Unity 6 (URP) | **M0 bring-up** — asset slice committed; [`Aetherium.Client`](../Aetherium.Client/) core + [`com.aetherium.unity`](../clients/unity/com.aetherium.unity/) package built; sample gameplay next (see [milestones](../docs/design/unity-sample/milestones.md)) | [`Data/Games/aphelion/`](../Data/Games/aphelion/) v0.1 — bestiary, kit, 4 reactive rules |
| [Unreal sample](unreal/) | Unreal Engine | Placeholder — see the [Unreal client guide](../docs/clients/unreal-client-guide.md) | — |
| [Console](console/) | Terminal (Spectre.Console) | Shipped as the top-level [`Aetherium.Console`](../Aetherium.Console/) project; relocation here is migration Phase C | plays any bundle (Emberfall, Neonveil, Hexhaven, Trigrove, Aphelion) |
