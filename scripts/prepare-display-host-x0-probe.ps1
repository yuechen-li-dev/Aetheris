param([Parameter(Mandatory = $true)][string]$HeliosRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$helios = (Resolve-Path -LiteralPath $HeliosRoot).Path
$output = Join-Path $repo 'artifacts/local/display-host-x0'
$threeOutput = Join-Path $output 'helios-three'
New-Item -ItemType Directory -Force $threeOutput | Out-Null
foreach ($name in @('three.core.js', 'three.webgpu.js', 'three.tsl.js')) {
    Copy-Item -LiteralPath (Join-Path $helios "node_modules/three/build/$name") -Destination $threeOutput
}
$html = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'display-host-x0-attachment-probe.html') -Raw
$html = $html.Replace('/aetheris.client/node_modules/three/build/', '/artifacts/local/display-host-x0/helios-three/')
$html = $html.Replace("'./display-host-x0-attachment-probe.js'", "'/scripts/display-host-x0-attachment-probe.js'")
Set-Content -LiteralPath (Join-Path $output 'helios-probe.html') -Value $html -Encoding utf8
$cadmataPackage = Get-Content -LiteralPath (Join-Path $repo 'aetheris.client/node_modules/three/package.json') -Raw | ConvertFrom-Json
$heliosPackage = Get-Content -LiteralPath (Join-Path $helios 'node_modules/three/package.json') -Raw | ConvertFrom-Json
@{ cadmataThree = $cadmataPackage.version; heliosThree = $heliosPackage.version; heliosRoot = $helios } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'dependencies.json') -Encoding utf8
Write-Output 'Serve the repository root on localhost, then open /scripts/display-host-x0-attachment-probe.html and /artifacts/local/display-host-x0/helios-probe.html.'
Write-Output 'Repeat each with ?ownedDepth to isolate external color. Three-owned attachment controls run in every page.'
