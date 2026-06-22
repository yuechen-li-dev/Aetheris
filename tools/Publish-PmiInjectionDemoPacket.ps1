[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot,
    [switch]$FrameworkDependent,
    [switch]$Zip,
    [switch]$Keep,
    [switch]$Open
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-RepoRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StartPath
    )

    $current = [System.IO.DirectoryInfo](Resolve-Path -LiteralPath $StartPath).Path
    while ($null -ne $current) {
        $solutionPath = Join-Path $current.FullName "Aetheris.slnx"
        $demoProjectPath = Join-Path $current.FullName "demos\Aetheris.PmiInjectionDemo\Aetheris.PmiInjectionDemo.csproj"
        if ((Test-Path -LiteralPath $solutionPath) -and (Test-Path -LiteralPath $demoProjectPath)) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not resolve the Aetheris repository root from '$StartPath'."
}

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Write-PacketRunner {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $content = @'
[CmdletBinding()]
param(
    [double]$PmiValue = 32.2,
    [string]$PmiLabel = "demoInnerDiameter",
    [string]$Firm,
    [switch]$Keep,
    [switch]$Open
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

$packetRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $packetRoot "Aetheris.PmiInjectionDemo.exe"
$dllPath = Join-Path $packetRoot "Aetheris.PmiInjectionDemo.dll"
$outputDir = Join-Path $packetRoot "output"
$templatePath = Join-Path $packetRoot "assets\ftc11-pmi-overlay.template.firm"

$launch = if (Test-Path -LiteralPath $exePath) {
    @{
        Command = $exePath
        Prefix = @()
    }
} elseif (Test-Path -LiteralPath $dllPath) {
    @{
        Command = $dllPath
        Prefix = @("dotnet")
    }
} else {
    throw "Could not find Aetheris.PmiInjectionDemo.exe or Aetheris.PmiInjectionDemo.dll in $packetRoot"
}

$firmPath = if ([string]::IsNullOrWhiteSpace($Firm)) {
    $null
} else {
    Resolve-AbsolutePath -Path $Firm -BasePath (Get-Location).Path
}

if ($firmPath -and -not (Test-Path -LiteralPath $firmPath)) {
    throw "Firmament overlay not found: $firmPath"
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$demoArgs = @(
    "--out"
    $outputDir
    "--pmi-value"
    $PmiValue.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    "--pmi-label"
    $PmiLabel
)

if ($Keep) {
    $demoArgs += "--keep"
}

if ($firmPath) {
    $demoArgs += @("--firm", $firmPath)
}

$displayCommand = if ($launch.Prefix.Count -gt 0) {
    "$($launch.Prefix[0]) $($launch.Command)"
} else {
    $launch.Command
}

Write-Host "Aetheris FTC-11 PMI Injection Demo Packet"
Write-Host "-----------------------------------------"
Write-Host "Packet root:"
Write-Host "  $packetRoot"
Write-Host ""
Write-Host "Executable:"
Write-Host "  $displayCommand"
Write-Host ""
Write-Host "Output directory:"
Write-Host "  $outputDir"
Write-Host ""
Write-Host "Bundled overlay template:"
Write-Host "  $templatePath"
Write-Host ""
Write-Host "Running:"
Write-Host "  $displayCommand $($demoArgs -join ' ')"
Write-Host ""

if ($launch.Prefix.Count -gt 0) {
    & $launch.Prefix[0] $launch.Command @demoArgs
} else {
    & $launch.Command @demoArgs
}

if ($LASTEXITCODE -ne 0) {
    throw "PMI injection demo failed with exit code $LASTEXITCODE."
}

$generatedFiles = @(Get-ChildItem -LiteralPath $outputDir -File | Sort-Object Name)
$overlayOutputPath = Join-Path $outputDir "ftc11-pmi-overlay.firm"
$enrichedStepPath = Join-Path $outputDir "ftc11-with-aetheris-pmi.step"
$reportPath = Join-Path $outputDir "demo-report.json"

Write-Host ""
Write-Host "Generated files:"
foreach ($file in $generatedFiles) {
    Write-Host "  $($file.Name)"
}

Write-Host ""
Write-Host "Next:"
Write-Host "  Open the output folder:"
Write-Host "    Invoke-Item `"$outputDir`""
Write-Host ""
Write-Host "  Edit the generated overlay and rerun:"
Write-Host "    .\Run-Demo.ps1 -Firm .\output\ftc11-pmi-overlay.firm -Keep"
Write-Host ""
Write-Host "  Start from the template instead:"
Write-Host "    Copy-Item .\assets\ftc11-pmi-overlay.template.firm .\output\my-overlay.firm"
Write-Host "    .\Run-Demo.ps1 -Firm .\output\my-overlay.firm -Keep"
Write-Host ""
Write-Host "  Inspect the enriched STEP:"
Write-Host "    $enrichedStepPath"
Write-Host ""
Write-Host "  Check the machine-readable report:"
Write-Host "    $reportPath"

if ($Open) {
    Write-Host ""
    Write-Host "Opening output folder..."
    Invoke-Item -LiteralPath $outputDir
}
'@

    Set-Content -LiteralPath $Path -Value $content -Encoding UTF8
}

function Write-PacketReadme {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$PublishMode
    )

    $content = @"
# Aetheris FTC-11 AP242 PMI Injection Demo Packet

This packet is a standalone shareable demo for Aetheris semantic PMI injection on the public NIST FTC-11 AP242 model. It is packaging and demo UX only; it does not add new CAD or STEP capability.

## Run the demo

Recommended Windows PowerShell path:

```powershell
.\Run-Demo.ps1 -Open
```

Customize the semantic PMI label and value:

```powershell
.\Run-Demo.ps1 -PmiValue 33.0 -PmiLabel demoInnerDiameter33 -Open
```

You can also run the executable directly:

```powershell
.\Aetheris.PmiInjectionDemo.exe --out .\output-direct
```

This packet was published in `$PublishMode` mode. The packet-local runner always writes to `output/` beside the executable and assets.

## Packet contents

```text
Aetheris.PmiInjectionDemo.exe
assets/
  nist_ftc_11_asme1_ap242-e2.stp
  ftc11-pmi-overlay.template.firm
Run-Demo.ps1
README.md
```

The demo executable uses the bundled `assets/` folder and does not require the Aetheris repository or .NET SDK at runtime in this published packet mode.

## Generated output

Running `.\Run-Demo.ps1` creates:

```text
output/
  nist_ftc_11_asme1_ap242-e2.stp
  nist_ftc_11_asme1_ap242-e2.canonical.step
  ftc11-pmi-overlay.firm
  ftc11-with-aetheris-pmi.step
  demo-report.json
```

## What to inspect

Check these files after a run:

* `output/ftc11-pmi-overlay.firm`
* `output/ftc11-with-aetheris-pmi.step`
* `output/demo-report.json`

The report should show that input import, canonical import, enriched AP242 reimport, and PMI evidence checks succeeded for the demo pipeline.

## Editing the overlay

The runner generates `output/ftc11-pmi-overlay.firm` automatically. You can edit that file and rerun:

```powershell
.\Run-Demo.ps1 -Firm .\output\ftc11-pmi-overlay.firm -Keep
```

Or start from the bundled template:

```powershell
Copy-Item .\assets\ftc11-pmi-overlay.template.firm .\output\my-overlay.firm
.\Run-Demo.ps1 -Firm .\output\my-overlay.firm -Keep
```

## FTC-11 provenance

The bundled FTC-11 input was copied from vendored NIST PMI FTC-11 repository test data and retains its original filename:

* Vendored source path: `testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp`
* Preserved filename: `nist_ftc_11_asme1_ap242-e2.stp`

## Limitations

This demo intentionally does not claim or provide:

* graphical PMI;
* drawing views;
* automatic decompilation;
* exact FTC-11 curved trimmed-shell volume integration;
* SolidWorks MBD replacement behavior.

Canonicalization happens before Firmament InlineStep overlay injection. The demo verifies STEP import/reimport validity and semantic PMI evidence presence for this FTC-11 case rather than exact volume equality.
"@

    Set-Content -LiteralPath $Path -Value $content -Encoding UTF8
}

$repoRoot = Get-RepoRoot -StartPath $PSScriptRoot
$demoProject = Join-Path $repoRoot "demos\Aetheris.PmiInjectionDemo\Aetheris.PmiInjectionDemo.csproj"
$packetRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot "artifacts\demo-packets"
} else {
    Resolve-AbsolutePath -Path $OutputRoot -BasePath (Get-Location).Path
}
$packetDir = Join-Path $packetRoot "Aetheris.PmiInjectionDemo"
$zipName = if ($FrameworkDependent) { "Aetheris.PmiInjectionDemo-framework-dependent.zip" } else { "Aetheris.PmiInjectionDemo-win-x64.zip" }
$zipPath = Join-Path $packetRoot $zipName
$nistSource = Join-Path $repoRoot "testdata\step242\nist\FTC\nist_ftc_11_asme1_ap242-e2.stp"
$overlaySource = Join-Path $repoRoot "demos\Aetheris.PmiInjectionDemo\assets\ftc11-pmi-overlay.template.firm"
$packetAssetsDir = Join-Path $packetDir "assets"
$publishMode = if ($FrameworkDependent) { "framework-dependent" } else { "self-contained single-file" }

if (-not $Keep -and (Test-Path -LiteralPath $packetDir)) {
    Remove-Item -LiteralPath $packetDir -Recurse -Force
}

New-Item -ItemType Directory -Path $packetDir -Force | Out-Null

$publishArgs = @(
    "publish"
    $demoProject
    "-c"
    $Configuration
    "-f"
    "net10.0"
    "-o"
    $packetDir
)

if ($FrameworkDependent) {
    $publishArgs += @("--self-contained", "false")
} else {
    $publishArgs += @(
        "-r"
        $Runtime
        "--self-contained"
        "true"
        "/p:PublishSingleFile=true"
        "/p:IncludeNativeLibrariesForSelfExtract=true"
    )
}

Write-Host "Publishing Aetheris FTC-11 PMI injection demo packet"
Write-Host "----------------------------------------------------"
Write-Host "Repo root:"
Write-Host "  $repoRoot"
Write-Host ""
Write-Host "Packet directory:"
Write-Host "  $packetDir"
Write-Host ""
Write-Host "Publish mode:"
Write-Host "  $publishMode"
Write-Host ""
Write-Host "Running:"
Write-Host "  dotnet $($publishArgs -join ' ')"
Write-Host ""

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $packetAssetsDir -Force | Out-Null
Copy-Item -LiteralPath $nistSource -Destination (Join-Path $packetAssetsDir "nist_ftc_11_asme1_ap242-e2.stp") -Force
Copy-Item -LiteralPath $overlaySource -Destination (Join-Path $packetAssetsDir "ftc11-pmi-overlay.template.firm") -Force

Write-PacketRunner -Path (Join-Path $packetDir "Run-Demo.ps1")
Write-PacketReadme -Path (Join-Path $packetDir "README.md") -PublishMode $publishMode

if ($Zip) {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    New-Item -ItemType Directory -Path $packetRoot -Force | Out-Null
    Compress-Archive -Path $packetDir -DestinationPath $zipPath
}

Write-Host ""
Write-Host "Packet ready:"
Write-Host "  $packetDir"

if ($Zip) {
    Write-Host ""
    Write-Host "Zip ready:"
    Write-Host "  $zipPath"
}

if ($Open) {
    Write-Host ""
    Write-Host "Opening packet folder..."
    Invoke-Item -LiteralPath $packetDir
}
