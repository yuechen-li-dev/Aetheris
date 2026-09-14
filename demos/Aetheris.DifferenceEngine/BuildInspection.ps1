[CmdletBinding()]
param([string]$Output='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$Output){$Output=Join-Path $repo 'artifacts/local/demos/difference-engine'}
$three=Join-Path $repo 'aetheris.client/node_modules/three/build/three.cjs'
if(!(Test-Path -LiteralPath $three)){throw 'Restore aetheris.client dependencies to supply its existing Three.js renderer.'}
$template=Get-Content -LiteralPath "$PSScriptRoot/inspection.html" -Raw
$html=$template.Replace('/*THREE_RUNTIME*/',(Get-Content -LiteralPath $three -Raw)).Replace('/*ASSEMBLY_DOCUMENT*/',(Get-Content -LiteralPath "$Output/storage-gate.mesh.json" -Raw))
Set-Content -LiteralPath "$Output/storage-inspection.html" -Value $html -Encoding utf8
Write-Host "Open $Output/storage-inspection.html in a browser. No server is required."
