[CmdletBinding()]
param(
    [string]$OutDir = 'artifacts/local/usd-industrial',
    [string]$UsdRoot = 'artifacts/local/usd-x0/tools/openusd',
    [string]$Blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe',
    [switch]$Render,
    [switch]$Motion
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$priorPath = $env:PATH
$priorPython = $env:PYTHONPATH
$priorCapture = $env:USD_X0_CAPTURE_DIR
$priorFrames = $env:USD_X0_FRAMES
Push-Location $repoRoot
try {
    $out = [IO.Path]::GetFullPath($OutDir)
    $usd = [IO.Path]::GetFullPath($UsdRoot)
    $python = Join-Path $usd 'python/python.exe'
    if (!(Test-Path -LiteralPath $python)) { throw 'Install the pinned NVIDIA OpenUSD SDK with scripts/qualify-usd-export-x0.ps1 first, or provide -UsdRoot.' }
    New-Item -ItemType Directory -Force $out | Out-Null
    $env:PATH = "$usd/python;$usd/lib;$usd/bin;$usd/plugin/usd;" + $env:PATH
    $env:PYTHONPATH = "$usd/lib/python;$usd/pip-packages"
    function Invoke-Logged([string]$Program, [string[]]$Arguments, [string]$Log) {
        & $Program @Arguments *> $Log
        if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $Program. See $Log" }
    }
    $fixture = 'fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas'
    $assembly = Join-Path $out 'atlas.usda'
    $evidence = Join-Path $out 'atlas.json'
    $exportArgs = @('Aetheris.CLI/bin/Release/net10.0/aetheris.dll','asm','export-usd',"$fixture/atlas-industrial.firmament",$assembly,
        '--materials',"$fixture/appearance.json",'--state','Shoulder=-75','--state','Elbow=-140','--state','GripLeft=10','--state','GripRight=10',
        '--evidence',$evidence,'--json')
    for ($frame=0; $frame -le 96; $frame++) {
        $blend = (1-[Math]::Cos(2*[Math]::PI*$frame/96))/2
        $shoulder = (-65-10*$blend).ToString('G17',[cultureinfo]::InvariantCulture)
        $elbow = (-120-20*$blend).ToString('G17',[cultureinfo]::InvariantCulture)
        $grip = (13-3*$blend).ToString('G17',[cultureinfo]::InvariantCulture)
        $exportArgs += @('--sample',"${frame}:Shoulder=$shoulder,Elbow=$elbow,GripLeft=$grip,GripRight=$grip")
    }
    Invoke-Logged 'dotnet' $exportArgs (Join-Path $out 'atlas-export.json')
    Invoke-Logged 'dotnet' @('Aetheris.CLI/bin/Release/net10.0/aetheris.dll','asm','export-ap242',"$fixture/atlas-industrial.firmament",'--out',(Join-Path $out 'atlas.step'),'--json') (Join-Path $out 'atlas-step-export.json')
    Invoke-Logged (Join-Path $usd 'bin/usdchecker.exe') @($assembly) (Join-Path $out 'atlas-checker.log')
    Invoke-Logged $python @('scripts/qualify-usd-export-x0.py',$assembly,$evidence,'--out',(Join-Path $out 'validation.json')) (Join-Path $out 'validation.log')
    $studio = Join-Path $out 'atlas-studio.usda'
    Invoke-Logged $python @('scripts/prepare-industrial-atlas-studio.py',$assembly,$studio) (Join-Path $out 'studio-prepare.log')
    Invoke-Logged (Join-Path $usd 'bin/usdchecker.exe') @($studio) (Join-Path $out 'studio-checker.log')
    $env:USD_X0_CAPTURE_DIR = Join-Path $out 'usdview'
    $env:USD_X0_FRAMES = '0,48,96'
    Invoke-Logged $python @('scripts/capture-usd-export-x0.py',$studio,'--camera','/Presentation/HeroCamera','--defaultsettings','--timing') (Join-Path $out 'viewer.log')
    if ($Render) {
        Invoke-Logged $Blender @('--background','--factory-startup','--python','scripts/render-industrial-atlas.py','--',$studio,(Join-Path $out 'atlas-hero.png')) (Join-Path $out 'blender-hero.log')
    }
    if ($Motion) {
        Invoke-Logged $Blender @('--background','--factory-startup','--python','scripts/render-industrial-atlas.py','--',$studio,(Join-Path $out 'atlas-motion.png'),'--motion') (Join-Path $out 'blender-motion.log')
        $frameList = Join-Path $out 'motion-frames.txt'
        $lines = 0..24 | ForEach-Object { "file '$((Join-Path $out ('motion-{0:d3}.png' -f ($_*4))).Replace('\','/'))'`nduration 0.166666667`n" }
        [IO.File]::WriteAllText($frameList,($lines -join ''))
        Invoke-Logged 'ffmpeg' @('-hide_banner','-y','-f','concat','-safe','0','-i',$frameList,'-r','24','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',(Join-Path $out 'atlas-motion.mp4')) (Join-Path $out 'ffmpeg.log')
    }
    Write-Host "Qualified IndustrialAtlas artifacts: $out"
}
finally {
    $env:PATH=$priorPath
    $env:PYTHONPATH=$priorPython
    $env:USD_X0_CAPTURE_DIR=$priorCapture
    $env:USD_X0_FRAMES=$priorFrames
    Pop-Location
}
