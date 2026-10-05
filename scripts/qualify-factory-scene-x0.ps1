[CmdletBinding()]
param(
    [string]$OutDir = 'artifacts/local/factory-scene-x0',
    [string]$Blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe',
    [string]$UsdRoot = 'artifacts/local/usd-x0/tools/openusd',
    [string]$GltfValidator = 'artifacts/local/presentation-3d-x0/tools/node_modules/gltf-validator'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$priorPath = $env:PATH
$priorPython = $env:PYTHONPATH
Push-Location $repoRoot
try {
    $out = [IO.Path]::GetFullPath($OutDir)
    $usd = [IO.Path]::GetFullPath($UsdRoot)
    foreach ($dependency in @($Blender,"$usd/bin/usdchecker.exe","$usd/python/python.exe",$GltfValidator)) {
        if (!(Test-Path -LiteralPath $dependency)) { throw "Missing qualification dependency: $dependency" }
    }
    New-Item -ItemType Directory -Force $out | Out-Null
    function Invoke-Logged([string]$Program,[string[]]$Arguments,[string]$Log) {
        & $Program @Arguments *> $Log
        if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $Program. See $Log" }
    }
    Invoke-Logged 'dotnet' @('build','Aetheris.CLI','-c','Release','-m:1','--nologo','-v:q') "$out/cli-build.log"
    $cli = 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
    $source = 'fixtures/Canonical/Scene/FactoryX0/factory.firmament'
    Invoke-Logged 'dotnet' @($cli,'validate',$source,'--json') "$out/source-validation.json"
    Invoke-Logged 'dotnet' @($cli,'scene','inspect',$source,'--repeat','2','--json') "$out/inspection.json"
    Invoke-Logged 'dotnet' @('run','scripts/measure-factory-scene-x0.cs','-c','Release','--',$source,$out) "$out/retained.log"
    Invoke-Logged 'dotnet' @($cli,'scene','export-usd',$source,"$out/factory.usda",'--json') "$out/usd-export.json"
    Invoke-Logged 'dotnet' @($cli,'scene','export-glb',$source,"$out/factory-enclosed.glb",'--json') "$out/enclosed-export.json"
    Invoke-Logged 'dotnet' @($cli,'scene','export-glb',$source,"$out/factory.glb",'--hide-boundary','hall.ceiling','--hide-boundary','hall.southWall','--hide-boundary','hall.eastWall','--json') "$out/glb-export.json"
    Invoke-Logged 'node' @('scripts/validate-presentation-glb.cjs',$GltfValidator,"$out/factory.glb","$out/factory-enclosed.glb") "$out/glb-validator.log"
    $env:PATH = "$usd/python;$usd/bin;$usd/lib;$usd/plugin/usd;$env:PATH"
    $env:PYTHONPATH = "$usd/lib/python;$usd/pip-packages"
    Invoke-Logged "$usd/bin/usdchecker.exe" @("$out/factory.usda") "$out/usdchecker.log"
    Invoke-Logged $Blender @('--background','--python-exit-code','1','--python','scripts/render-factory-scene-x0.py','--',"$out/factory.glb",$out) "$out/render.log"
    $deck = Get-Content -Raw fixtures/Canonical/Scene/FactoryX0/factory-presentation.json | ConvertFrom-Json
    foreach ($slide in $deck.slides) { $slide.models[0].source = "$out/factory.glb" }
    $deck.slides[0].models[0].preview = "$out/hero.png"
    $deck.slides[1].models[0].preview = "$out/overview.png"
    $deck | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath "$out/deck.json" -Encoding utf8
    Invoke-Logged 'dotnet' @($cli,'presentation','compile',"$out/deck.json","$out/factory.pptx",'--json') "$out/pptx-export.json"
    Invoke-Logged "$usd/python/python.exe" @('scripts/validate-factory-scene-x0.py',$out) "$out/external-validation.log"
    Write-Host "Factory artifacts validated: $out. Desktop PowerPoint interaction is a separate gate."
}
finally { $env:PATH = $priorPath; $env:PYTHONPATH = $priorPython; Pop-Location }
