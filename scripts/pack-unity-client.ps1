# Vendors the Aetherium.Client core (netstandard2.1) plus its dependency closure into the
# Unity package's Runtime/Plugins so Unity can resolve them as plain DLLs -- no NuGet in
# Unity (docs/design/unity-sample/repo-structure.md). Run manually before tagging a client
# release or before opening the Aphelion sample for the first time. DLLs are gitignored;
# this script is the reproducible source of truth for what ships.
# NOTE: ASCII only in this file -- Windows PowerShell 5.1 reads BOM-less files as ANSI.
#
# Usage:  .\scripts\pack-unity-client.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Aetherium.Client\Aetherium.Client.csproj"
$pluginsDir = Join-Path $repoRoot "clients\unity\com.aetherium.unity\Runtime\Plugins"
$publishDir = Join-Path $repoRoot "Aetherium.Client\bin\$Configuration\netstandard2.1\publish"

Write-Host "Publishing Aetherium.Client (netstandard2.1, $Configuration)..."
dotnet publish $project -c $Configuration -f netstandard2.1 --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Assemblies Unity itself provides (or that IL2CPP supplies from the class libraries):
# vendoring these would cause duplicate-assembly errors in the Editor.
$excluded = @(
    "netstandard.dll",
    "mscorlib.dll"
)

New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
Get-ChildItem -Path (Join-Path $pluginsDir "*.dll") | Remove-Item -Force

$copied = 0
foreach ($dll in Get-ChildItem -Path (Join-Path $publishDir "*.dll")) {
    if ($excluded -contains $dll.Name) { continue }
    Copy-Item $dll.FullName -Destination $pluginsDir -Force
    $copied++
}

Write-Host "Vendored $copied assemblies into clients/unity/com.aetherium.unity/Runtime/Plugins."
Write-Host "If the Unity Editor reports a duplicate assembly, add its name to the excluded list here --"
Write-Host "the known-good exclusion set gets pinned the first time the Aphelion sample opens."
