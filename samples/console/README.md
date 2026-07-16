# Console Sample (pointer)

The console client lives at the repo top level as [`Aetherium.Console`](../../Aetherium.Console/) — the engine's reference renderer (Spectre.Console TUI, dynamic lighting, vision modes). It plays any game bundle under `Data/Games/` (Emberfall, Neonveil).

Relocating it under `samples/console/` is migration **Phase C** in the [repo-structure design](../../docs/design/unity-sample/repo-structure.md#migration-path) — deferred until the shared `Aetherium.Client` core is proven by the Unity sample.
