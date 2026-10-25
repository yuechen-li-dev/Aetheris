param(
    [Parameter(Mandatory)][string]$BundleDirectory,
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$bundle = (Resolve-Path -LiteralPath $BundleDirectory).Path
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output | Out-Null
$cli = Join-Path $bundle 'aetheris'
$metadata = Get-Content (Join-Path $bundle 'BUILD-METADATA.json') -Raw | ConvertFrom-Json
if ($metadata.modifiedSource -or $metadata.defaultPresentation -ne 'Browser') { throw 'Invalid portable source/presentation metadata.' }
if ((Test-Path (Join-Path $bundle 'webview2')) -or (Test-Path (Join-Path $bundle 'cadmata'))) { throw 'Private browser/duplicated server in portable bundle.' }
$env:DOTNET_ROOT = Join-Path $output 'no-installed-dotnet'
& $cli --version
Copy-Item (Join-Path $source 'fixtures/Canonical/Features/Holes/through-hole.firmament') (Join-Path $output 'hole.firmament')
$validation = & $cli validate (Join-Path $output 'hole.firmament') --json | ConvertFrom-Json
if ($validation.firmamentV2Validation.status -ne 'valid' -or @($validation.firmamentV2Validation.diagnostics).Count) { throw 'Canonical hole must validate without diagnostics.' }
& $cli build (Join-Path $output 'hole.firmament') --output (Join-Path $output 'hole.step') --json
& $cli verify (Join-Path $output 'hole.firmament') --json
& $cli build (Join-Path $source 'fixtures/Canonical/WireForm/paperclip.firmament') --output (Join-Path $output 'paperclip.step') --json
& $cli canon (Join-Path $output 'paperclip.step') --out (Join-Path $output 'paperclip-roundtrip.step') --json
$paperclip = Join-Path $output 'paperclip-roundtrip.step'
if (Select-String -LiteralPath $paperclip -SimpleMatch 'B_SPLINE' -Quiet) { throw 'Analytic paperclip acquired spline entities.' }
$paperclipHash = (Get-FileHash -LiteralPath $paperclip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($paperclipHash -ne '82cf4b33274646807dfdb139728030959f36673d0c0a34ff99777be59a329a2c') { throw "Published Windows paperclip geometry parity failed: $paperclipHash" }
& $cli drawing compile (Join-Path $source 'fixtures/Canonical/Drawings/bearing-block-production-drawing.firmament') --out-dir (Join-Path $output 'drawing') --json
if (-not (Test-Path (Join-Path $output 'drawing/*.pdf')) -or -not (Test-Path (Join-Path $output 'drawing/*.pptx'))) { throw 'PDF/PPTX drawing outputs missing.' }
$forge = Join-Path $bundle 'forge-host/Aetheris.Forge.Host'
& $forge info
& $forge list
& (Join-Path $PSScriptRoot 'Test-ForgeHostTemplates.ps1') -ExecutablePath $forge -OutputDirectory (Join-Path $output 'forge')
@{ runtime=$metadata.runtime; sourceRevision=$metadata.sourceRevision; selfContained=$true; canonicalHoleDiagnostics=0;
   paperclipSha256=$paperclipHash; drawing=$true; nativeForge=$true } | ConvertTo-Json | Set-Content (Join-Path $output 'qualification.json')
