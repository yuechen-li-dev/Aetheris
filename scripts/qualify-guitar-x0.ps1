[CmdletBinding()]
param(
    [string]$OutDir = 'artifacts/local/guitar-x0',
    [string]$UsdRoot = 'artifacts/local/usd-x0/tools/openusd',
    [string]$Blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe',
    [switch]$Render,
    [switch]$Viewport
)
$ErrorActionPreference = 'Stop'
$priorPath = $env:PATH
$priorPython = $env:PYTHONPATH
$priorCapture = $env:GUITAR_X0_CAPTURE_DIR
Push-Location (Split-Path -Parent $PSScriptRoot)
function Invoke-Logged([string]$Exe, [string[]]$Arguments, [string]$Log) {
    & $Exe @Arguments > $Log 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed ($LASTEXITCODE): $Exe. See $Log" }
}
try {
    $out = [IO.Path]::GetFullPath($OutDir)
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $fixture = 'fixtures/Canonical/AssemblyInterfaces/GuitarX0'
    $source = "$fixture/guitar-x0.firmament"
    $cli = 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
    Invoke-Logged 'dotnet' @('build','Aetheris.slnx','-c','Release','-m:1','--nologo','-v:q') "$out/build.log"
    Invoke-Logged 'dotnet' @('run','--project','demos/GuitarSurfacingX0','-c','Release','--',$source,"$out/timings.json") "$out/benchmark.log"
    Invoke-Logged 'dotnet' @($cli,'asm','export-usd',$source,"$out/guitar.usda",'--materials',"$fixture/preview-materials.json",'--evidence',"$out/display.json",'--json') "$out/export.json"
    Invoke-Logged 'dotnet' @($cli,'asm','export-ap242',$source,'--out',"$out/guitar.step",'--json') "$out/step-export.json"
    Invoke-Logged 'dotnet' @($cli,'section-chain','build',"$fixture/CarvedMaple.firmament",'--out',"$out/carved-maple.step",'--json') "$out/carve-build.json"
    Invoke-Logged 'dotnet' @($cli,'section-chain','build',"$fixture/Neck.firmament",'--out',"$out/neck.step",'--json') "$out/neck-build.json"
    Invoke-Logged 'dotnet' @($cli,'build',"$fixture/string-low-e.firmament",'--out',"$out/string-low-e.step",'--json') "$out/string-build.json"
    Invoke-Logged 'python' @('scripts/check-guitar-x0.py',$out) "$out/mesh-check.log"
    if ($Viewport) {
        $usd = [IO.Path]::GetFullPath($UsdRoot)
        $env:PATH = "$usd/python;$usd/lib;$usd/bin;$usd/plugin/usd;" + $priorPath
        $env:PYTHONPATH = "$usd/lib/python;$usd/pip-packages"
        $env:GUITAR_X0_CAPTURE_DIR = "$out/viewport"
        Invoke-Logged "$usd/bin/usdchecker.exe" @("$out/guitar.usda") "$out/usdchecker.log"
        Invoke-Logged "$usd/python/python.exe" @('scripts/qualify-usd-export-x0.py',"$out/guitar.usda","$out/display.json",'--out',"$out/usd-validation.json") "$out/usd-validation.log"
        Invoke-Logged "$usd/python/python.exe" @('scripts/capture-guitar-x0.py',"$out/guitar.usda",'--defaultsettings','--timing') "$out/viewport.log"
    }
    if ($Render) {
        Invoke-Logged $Blender @('--background','--factory-startup','--python','scripts/render-guitar-x0.py','--',"$out/guitar.usda",$out) "$out/render.log"
    }
    Write-Host "Guitar X0 artifacts: $out"
}
finally {
    $env:PATH = $priorPath
    $env:PYTHONPATH = $priorPython
    $env:GUITAR_X0_CAPTURE_DIR = $priorCapture
    Pop-Location
}
