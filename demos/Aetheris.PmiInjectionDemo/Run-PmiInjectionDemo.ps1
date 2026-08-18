[CmdletBinding()]
param(
    [double]$PmiValue = 32.2,
    [string]$PmiLabel = "demoInnerDiameter",
    [string]$Out,
    [string]$Firm,
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

$repoRoot = Get-RepoRoot -StartPath $PSScriptRoot
$demoProject = Join-Path $repoRoot "demos\Aetheris.PmiInjectionDemo\Aetheris.PmiInjectionDemo.csproj"
$outputDir = if ([string]::IsNullOrWhiteSpace($Out)) {
    Join-Path $repoRoot "artifacts\local\demos\pmi-injection"
} else {
    Resolve-AbsolutePath -Path $Out -BasePath (Get-Location).Path
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
    "run"
    "--project"
    $demoProject
    "--"
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

Write-Host "Aetheris FTC-11 PMI Injection Demo Runner"
Write-Host "────────────────────────────────────────"
Write-Host "Repo root:"
Write-Host "  $repoRoot"
Write-Host ""
Write-Host "Demo project:"
Write-Host "  $demoProject"
Write-Host ""
Write-Host "Output directory:"
Write-Host "  $outputDir"
Write-Host ""
Write-Host "Running:"
Write-Host "  dotnet $($demoArgs -join ' ')"
Write-Host ""

& dotnet @demoArgs
if ($LASTEXITCODE -ne 0) {
    throw "PMI injection demo failed with exit code $LASTEXITCODE."
}

$generatedFiles = Get-ChildItem -LiteralPath $outputDir -File | Sort-Object Name
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
Write-Host "    explorer `"$outputDir`""
Write-Host ""
Write-Host "  Edit the overlay:"
Write-Host "    $overlayOutputPath"
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
