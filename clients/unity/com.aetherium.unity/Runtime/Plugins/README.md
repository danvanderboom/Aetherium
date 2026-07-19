# Vendored assemblies (generated — not committed)

This folder receives `Aetherium.Client.dll` and its dependency closure (the SignalR client
and its `Microsoft.Extensions.*`/`System.*` dependencies), built for `netstandard2.1`.

Populate it by running, from the repo root:

```powershell
.\scripts\pack-unity-client.ps1
```

The DLLs are **gitignored** — the pack script is the reproducible source of truth. Run it
after any change to `Aetherium.Client` before opening a Unity project that consumes this
package. When Unity 6.8's CoreCLR runtime lands, the same script flips to the `net10.0`
build with no package restructuring (see docs/design/unity-sample/unity-client-library.md).
