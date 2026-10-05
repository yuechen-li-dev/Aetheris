[CmdletBinding()]
param(
    [string]$OutDir = 'artifacts/local/presentation-3d-x0',
    [string]$DeckName = 'interactive-3d-demo.pptx',
    [string]$Blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $out = [IO.Path]::GetFullPath($OutDir)
    if ([IO.Path]::GetFileName($DeckName) -ne $DeckName -or [IO.Path]::GetExtension($DeckName) -ne '.pptx') { throw 'DeckName must be a .pptx filename.' }
    New-Item -ItemType Directory -Force $out | Out-Null
    foreach ($scene in @('artifacts/local/usd-industrial/atlas-hero.blend')) {
        if (!(Test-Path -LiteralPath $scene)) { throw "Missing qualified presentation scene: $scene. Run the existing qualify-industrial-atlas.ps1 -Render first." }
    }
    function Invoke-Logged([string]$Program, [string[]]$Arguments, [string]$Log) {
        & $Program @Arguments *> $Log
        if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $Program. See $Log" }
    }
    Invoke-Logged 'dotnet' @('build','Aetheris.CLI','-c','Release','-m:1','--nologo','-v:q') "$out/cli-build.log"
    $cli = 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
    Invoke-Logged 'dotnet' @($cli,'asm','export-glb','fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament',"$out/atlas-display.glb",'--materials','fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/appearance.json','--state','Shoulder=-75','--state','Elbow=-140','--state','GripLeft=10','--state','GripRight=10','--json') "$out/atlas-display-export.json"
    Invoke-Logged 'dotnet' @($cli,'asm','export-glb','fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm',"$out/guitar-display.glb",'--materials','fixtures/Canonical/AssemblyInterfaces/GuitarX0/preview-materials.json','--json') "$out/guitar-display-export.json"
    # Rebuild the shaded guitar from current compiled geometry, never a cached
    # studio scene: neck/bridge fixes can otherwise disappear from the deck.
    $guitarRoot = "$out/guitar-source"
    New-Item -ItemType Directory -Force $guitarRoot | Out-Null
    Invoke-Logged 'dotnet' @($cli,'asm','export-usd','fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm',"$guitarRoot/guitar.usda",'--materials','fixtures/Canonical/AssemblyInterfaces/GuitarX0/preview-materials.json','--evidence',"$guitarRoot/display.json",'--json') "$guitarRoot/export.json"
    Invoke-Logged $Blender @('--background','--factory-startup','--python-exit-code','1','--python','scripts/render-guitar-x0.py','--',"$guitarRoot/guitar.usda",$guitarRoot,'--preview','--view=hero') "$guitarRoot/render.log"
    foreach ($kind in @('atlas','guitar')) {
        $scene = if ($kind -eq 'atlas') { 'artifacts/local/usd-industrial/atlas-hero.blend' } else { "$guitarRoot/guitar-studio.blend" }
        Invoke-Logged $Blender @('--background','--python-exit-code','1','--python','scripts/export-presentation-scene.py','--',$kind,$scene,"$out/$kind.glb") "$out/$kind-polish.log"
        Invoke-Logged $Blender @('--background','--python-exit-code','1','--python','scripts/inspect-presentation-glb.py','--',"$out/$kind.glb",$out) "$out/$kind-blender.log"
    }
    $toolsRoot = "$out/tools"
    Invoke-Logged 'npm' @('install','--prefix',$toolsRoot,'gltf-validator@2.0.0-dev.3.10') "$out/validator-install.log"
    Invoke-Logged 'node' @('scripts/validate-presentation-glb.cjs',"$toolsRoot/node_modules/gltf-validator","$out/atlas-display.glb","$out/guitar-display.glb","$out/atlas.glb","$out/guitar.glb") "$out/validator.log"
    $deck = Get-Content -Raw fixtures/Canonical/Presentation3D/interactive-3d-demo.json | ConvertFrom-Json
    for ($i=0; $i -lt 2; $i++) {
        $kind = @('atlas','guitar')[$i]
        $deck.slides[$i].models[0].source = "$out/$kind.glb"
        $deck.slides[$i].models[0].preview = "$out/$kind-fallback.png"
    }
    $manifest = "$out/deck.json"
    $deck | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifest -Encoding utf8
    Invoke-Logged 'dotnet' @($cli,'presentation','compile',$manifest,"$out/$DeckName",'--json') "$out/deck-compile.json"
    $empty = $deck | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    foreach ($slide in $empty.slides) { $slide.models = @() }
    $empty | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath "$out/deck-without-models.json" -Encoding utf8
    Invoke-Logged 'dotnet' @($cli,'presentation','compile',"$out/deck-without-models.json","$out/deck-without-models.pptx",'--json') "$out/deck-without-models-compile.json"
    Write-Host "Generated and externally validated static GLBs and embedded PPTX: $out. Desktop rotation/save/reopen must be tested separately."
}
finally { Pop-Location }
