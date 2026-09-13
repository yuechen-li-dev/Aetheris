[CmdletBinding()]
param([string]$Output = 'artifacts/local/demos/editable-v8')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$destination = [IO.Path]::GetFullPath($Output)
$three = Join-Path $repo 'aetheris.client/node_modules/three'
if (-not (Test-Path -LiteralPath "$three/build/three.module.js")) { throw 'Run npm ci --prefix aetheris.client to install the lockfile-pinned Three.js dependency.' }
New-Item -ItemType Directory -Force -Path "$destination/vendor" | Out-Null
Copy-Item -LiteralPath "$three/build/three.module.js", "$three/build/three.core.js", "$three/examples/jsm/controls/OrbitControls.js" -Destination "$destination/vendor"
Copy-Item -LiteralPath "$three/LICENSE" -Destination "$destination/vendor/three.LICENSE"
Copy-Item -Path "$PSScriptRoot/web/*" -Destination $destination
Copy-Item -LiteralPath "$repo/Aetheris.Kernel.Core/Mechanisms/Web/prescribed-motion.mjs" -Destination $destination
Write-Host "Viewer prepared: $destination/index.html"
