[CmdletBinding()]
param([string]$Output='artifacts/local/demos/difference-engine-showcase')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$Output=[IO.Path]::GetFullPath($Output)
$dist=Join-Path $Output 'dist'
$three=Join-Path $repo 'aetheris.client/node_modules/three'
if(!(Test-Path -LiteralPath "$three/build/three.module.js")){throw 'Restore aetheris.client dependencies to provide the existing Three.js renderer.'}
New-Item -ItemType Directory -Force -Path "$dist/vendor"|Out-Null
Copy-Item -LiteralPath "$three/build/three.module.js","$three/build/three.core.js","$three/examples/jsm/controls/OrbitControls.js" -Destination "$dist/vendor"
Copy-Item -LiteralPath "$three/LICENSE" -Destination "$dist/vendor/three.LICENSE"
Copy-Item -LiteralPath "$three/examples/jsm/environments/RoomEnvironment.js" -Destination "$dist/vendor"
Copy-Item -Path "$PSScriptRoot/web/*" -Destination $dist
Copy-Item -LiteralPath "$Output/machine.mesh.json","$Output/difference-engine.step","$Output/design.json","$Output/motion.json","$Output/receipts.json" -Destination $dist
if(Test-Path -LiteralPath "$Output/review/assembly-hero.png") { Copy-Item -LiteralPath "$Output/review/assembly-hero.png" -Destination "$dist/preview.png" }
Write-Host "Production assets prepared: $dist"
