[CmdletBinding()]
param(
    [string]$OutDir = 'artifacts/local/usd-x0',
    [string]$UsdRoot,
    [switch]$SkipBuild,
    [switch]$CaptureMotion
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$previousPath = $env:PATH
$previousPythonPath = $env:PYTHONPATH
$previousCaptureDir = $env:USD_X0_CAPTURE_DIR
$previousFrames = $env:USD_X0_FRAMES
Push-Location $repoRoot
try {
    $out = [IO.Path]::GetFullPath($OutDir)
    New-Item -ItemType Directory -Force $out | Out-Null
    if (!$UsdRoot) { $UsdRoot = Join-Path $out 'tools/openusd' }
    $UsdRoot = [IO.Path]::GetFullPath($UsdRoot)
    $download = 'https://developer.nvidia.com/downloads/usd/usd_binaries/25.08/usd.py312.windows-x86_64.usdview.release-v25.08.71e038c1.zip'
    $sha256 = '61BAE28D18C873871047E7A8B3FE1FFE2188FB88FDDE113BE429812D27F0C8B4'
    if (!(Test-Path -LiteralPath (Join-Path $UsdRoot 'python/python.exe'))) {
        $zip = Join-Path $out 'tools/openusd-25.08.zip'
        New-Item -ItemType Directory -Force (Split-Path -Parent $zip) | Out-Null
        if (!(Test-Path -LiteralPath $zip)) { Invoke-WebRequest -Uri $download -OutFile $zip }
        if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $sha256) { throw 'OpenUSD download checksum mismatch.' }
        Expand-Archive -LiteralPath $zip -DestinationPath $UsdRoot -Force
    }
    $env:PATH = "$UsdRoot/python;$UsdRoot/lib;$UsdRoot/bin;$UsdRoot/plugin/usd;" + $env:PATH
    $env:PYTHONPATH = "$UsdRoot/lib/python;$UsdRoot/pip-packages"
    $python = Join-Path $UsdRoot 'python/python.exe'
    function Invoke-Logged([string]$Program, [string[]]$Arguments, [string]$Log) {
        & $Program @Arguments *> $Log
        if ($LASTEXITCODE -ne 0) { throw "Command failed ($LASTEXITCODE): $Program. See $Log" }
    }
    if (!$SkipBuild) { Invoke-Logged 'dotnet' @('build','Aetheris.slnx','-c','Release','--no-restore','-m:1') (Join-Path $out 'build.log') }
    $cli = 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
    $fixture = 'fixtures/Canonical/AssemblyInterfaces'
    $witnesses = @(
        @{ Name='fixed'; Source='fixed-nested-usd.firmament'; Options=@() },
        @{ Name='arm'; Source='two-link-arm-usd.firmament'; Options=@('--state','Shoulder=35','--state','Elbow=65','--sample','0:Shoulder=0,Elbow=0','--sample','48:Shoulder=35,Elbow=65','--sample','96:Shoulder=-20,Elbow=35') },
        @{ Name='slider'; Source='external-step-slider.firmament'; Options=@('--state','Travel=24') },
        @{ Name='occt-slider'; Source='external-occt-slider-usd.firmament'; Options=@('--state','Travel=120') },
        @{ Name='demo'; Source='physical-ai-demo.firmament'; Options=@('--state','Shoulder=25','--state','Elbow=80','--state','GripLeft=8','--state','GripRight=8','--materials',"$fixture/physical-ai-demo.appearance.json") }
    )
    for ($frame=0; $frame -le 96; $frame++) {
        $blend = if ($frame -le 48) { $frame / 48.0 } else { (96-$frame) / 48.0 }
        $shoulder=(5+20*$blend).ToString('G17',[cultureinfo]::InvariantCulture)
        $elbow=(20+60*$blend).ToString('G17',[cultureinfo]::InvariantCulture)
        $grip=(14-6*$blend).ToString('G17',[cultureinfo]::InvariantCulture)
        $witnesses[-1].Options += @('--sample',"${frame}:Shoulder=$shoulder,Elbow=$elbow,GripLeft=$grip,GripRight=$grip")
    }
    $reports = @()
    foreach ($witness in $witnesses) {
        $name = $witness.Name
        $usd = Join-Path $out "$name.usda"
        $evidence = Join-Path $out "$name.json"
        $exportLog = Join-Path $out "$name-export.json"
        Invoke-Logged 'dotnet' (@($cli,'asm','export-usd',"$fixture/$($witness.Source)",$usd,'--evidence',$evidence,'--json') + $witness.Options) $exportLog
        Invoke-Logged (Join-Path $UsdRoot 'bin/usdchecker.exe') @($usd) (Join-Path $out "$name-checker.log")
        $validation = Join-Path $out "$name-validation.json"
        Invoke-Logged $python @('scripts/qualify-usd-export-x0.py',$usd,$evidence,'--out',$validation) (Join-Path $out "$name-validation.log")
        $env:USD_X0_CAPTURE_DIR = Join-Path $out "$name-captures"
        $env:USD_X0_FRAMES = if ($name -eq 'arm') { '0,48,96' } else { '0' }
        Invoke-Logged $python @('scripts/capture-usd-export-x0.py',$usd,'--defaultsettings','--timing') (Join-Path $out "$name-viewer.log")
        $result = Get-Content -LiteralPath $validation -Raw | ConvertFrom-Json
        $metrics = Get-Content -LiteralPath $exportLog -Raw | ConvertFrom-Json
        $reports += @{ witness=$name; source="$fixture/$($witness.Source)"; usd="$name.usda"; meshMilliseconds=$metrics.meshMilliseconds; serializationMilliseconds=$metrics.serializationMilliseconds; bytes=$metrics.bytes; external=$result }
        Write-Host "$name passed: $($result.occurrences) occurrences, $($result.sharedPrototypes) shared prototypes."
    }
    $studio = Join-Path $out 'demo-studio.usda'
    Invoke-Logged $python @('scripts/prepare-usd-x0-studio.py',(Join-Path $out 'demo.usda'),$studio,'--environment',(Join-Path $UsdRoot 'resources/Lights/table_mountain.hdr')) (Join-Path $out 'studio-prepare.log')
    Invoke-Logged (Join-Path $UsdRoot 'bin/usdchecker.exe') @($studio) (Join-Path $out 'studio-checker.log')
    $env:USD_X0_CAPTURE_DIR = Join-Path $out 'demo-captures'
    $env:USD_X0_FRAMES = '0,48,96'
    Invoke-Logged $python @('scripts/capture-usd-export-x0.py',$studio,'--camera','/Presentation/HeroCamera','--defaultsettings','--timing') (Join-Path $out 'demo-studio-viewer.log')
    if ($CaptureMotion) {
        $env:USD_X0_CAPTURE_DIR = Join-Path $out 'demo-motion'
        $env:USD_X0_FRAMES = (0..24 | ForEach-Object { $_*4 }) -join ','
        Invoke-Logged $python @('scripts/capture-usd-export-x0.py',$studio,'--camera','/Presentation/HeroCamera','--defaultsettings','--timing') (Join-Path $out 'demo-motion.log')
        $frames = 0..24 | ForEach-Object { "file '$((Join-Path $out "demo-motion/render-$($_*4).png").Replace('\','/'))'`nduration 0.166666667`n" }
        $frameList = Join-Path $out 'motion-frames.txt'
        [IO.File]::WriteAllText($frameList,($frames -join ''))
        Invoke-Logged 'ffmpeg' @('-hide_banner','-y','-f','concat','-safe','0','-i',$frameList,'-vf','scale=1280:-2','-r','24','-c:v','libx264','-crf','18','-pix_fmt','yuv420p',(Join-Path $out 'atlas-motion.mp4')) (Join-Path $out 'ffmpeg.log')
    }
    @{ milestone='USD-EXPORT-X0'; tool='NVIDIA-distributed OpenUSD usdview / Hydra Storm'; source=$download; archiveSha256=$sha256; witnesses=$reports } |
        ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $out 'qualification-summary.json')
    Write-Host "Qualified files and external screenshots: $out"
}
finally {
    $env:PATH=$previousPath
    $env:PYTHONPATH=$previousPythonPath
    $env:USD_X0_CAPTURE_DIR=$previousCaptureDir
    $env:USD_X0_FRAMES=$previousFrames
    Pop-Location
}
