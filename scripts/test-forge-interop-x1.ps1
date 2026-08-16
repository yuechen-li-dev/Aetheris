[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$hostPath = Join-Path $repo "Aetheris.Forge.Host/bin/Release/net10.0/Aetheris.Forge.Host.exe"
$request = Join-Path $repo "samples/forge-interop-x1/request.json"
$root = Join-Path $repo ".tmp/forge-interop-x1"

dotnet build (Join-Path $repo "Aetheris.Forge.Host/Aetheris.Forge.Host.csproj") -c Release --nologo | Out-Host
New-Item -ItemType Directory -Force -Path $root | Out-Null

$pythonOut = Join-Path $root "python"
$goOut = Join-Path $root "go"
$rustOut = Join-Path $root "rust"
$typescriptOut = Join-Path $root "typescript"

$python = python (Join-Path $repo "samples/forge-interop-x1/python/client.py") $hostPath $request $pythonOut | ConvertFrom-Json
$go = go run (Join-Path $repo "samples/forge-interop-x1/go/main.go") $hostPath $request $goOut | ConvertFrom-Json
$rustBinary = Join-Path $root "rust-client.exe"
rustc (Join-Path $repo "samples/forge-interop-x1/rust/client.rs") -o $rustBinary
$rust = & $rustBinary $hostPath $request $rustOut | ConvertFrom-Json
$typescript = node (Join-Path $repo "samples/forge-interop-x1/typescript/client.ts") $hostPath $request $typescriptOut | ConvertFrom-Json

$results = [ordered]@{ python = $python; go = $go; rust = $rust; typescript = $typescript }
$baseline = ($python.artifacts | Sort-Object kind | ForEach-Object { "$($_.kind):$($_.sha256)" }) -join ";"
foreach ($entry in $results.GetEnumerator()) {
    $hashes = ($entry.Value.artifacts | Sort-Object kind | ForEach-Object { "$($_.kind):$($_.sha256)" }) -join ";"
    if ($hashes -ne $baseline) { throw "$($entry.Key) artifact hashes differ from Python." }
    $step = Join-Path (Join-Path $root $entry.Key) "part.step"
    dotnet run --project (Join-Path $repo "Aetheris.CLI") -- inspect $step --json | Out-Null
}

[pscustomobject]@{
    template = "Standard.SheetMetal.ElectronicsEnclosure"
    languages = @($results.Keys)
    artifactHashes = $python.artifacts | Select-Object kind, sha256, size
    equivalent = $true
} | ConvertTo-Json -Depth 5
