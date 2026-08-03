param(
    [Parameter(Mandatory = $true)]
    [string]$Probe,

    [string]$Name,

    [string]$Out,

    [switch]$FreeCAD,

    [switch]$Open,

    [switch]$Keep
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$probePath = (Resolve-Path -LiteralPath $Probe).Path
$requestedName = if ([string]::IsNullOrWhiteSpace($Name)) {
    [System.IO.Path]::GetFileNameWithoutExtension($probePath)
} else {
    $Name
}
$probeName = [regex]::Replace($requestedName, '[^A-Za-z0-9_.-]', '-')
$identifier = [regex]::Replace($requestedName, '[^A-Za-z0-9_]', '_')
if ($identifier -match '^[0-9]') {
    $identifier = "probe_$identifier"
}

$outputRoot = if ([string]::IsNullOrWhiteSpace($Out)) {
    Join-Path $repoRoot "demo-output\ruled-probes\$probeName"
} elseif ([System.IO.Path]::IsPathRooted($Out)) {
    [System.IO.Path]::GetFullPath($Out)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Out))
}

$inputDir = Join-Path $outputRoot 'input'
$wrapperDir = Join-Path $outputRoot 'wrapper'
$outputDir = Join-Path $outputRoot 'output'
New-Item -ItemType Directory -Force -Path $inputDir, $wrapperDir, $outputDir | Out-Null

$probeCopy = Join-Path $inputDir ([System.IO.Path]::GetFileName($probePath))
$canonicalInput = Join-Path $inputDir "$probeName.canonical-input.step"
$wrapperPath = Join-Path $wrapperDir "$probeName.firm"
$outputStep = Join-Path $outputDir "$probeName.canonical.step"
$reimportStep = Join-Path $outputDir "$probeName.reimport-smoke.step"
$reportPath = Join-Path $outputDir 'probe-report.json'

Copy-Item -LiteralPath $probePath -Destination $probeCopy -Force

& dotnet run --project (Join-Path $repoRoot 'Aetheris.CLI') -- canon $probeCopy --out $canonicalInput --mode production --json
if ($LASTEXITCODE -ne 0) {
    throw "Canonicalization failed for ruled STEP probe '$probePath'."
}

$relativeInput = "../input/$([System.IO.Path]::GetFileName($canonicalInput))"
$wrapper = @"
model ${identifier}ProbeHarness {
    units mm

    solid ${identifier}: InlineStep {
        path: "$relativeInput"
    }
}
"@
Set-Content -LiteralPath $wrapperPath -Value $wrapper -Encoding UTF8

& dotnet run --project (Join-Path $repoRoot 'Aetheris.CLI') -- build $wrapperPath --out $outputStep --json
if ($LASTEXITCODE -ne 0) {
    throw "InlineStep wrapper build failed for '$wrapperPath'."
}

& dotnet run --project (Join-Path $repoRoot 'Aetheris.CLI') -- canon $outputStep --out $reimportStep --mode production --json
$reimportSucceeded = $LASTEXITCODE -eq 0
if (-not $reimportSucceeded) {
    throw "Reimport smoke failed for '$outputStep'."
}

$analyzeText = & dotnet run --project (Join-Path $repoRoot 'Aetheris.CLI') -- analyze $outputStep --json
$analyzeSucceeded = $LASTEXITCODE -eq 0
$analyze = if ($analyzeSucceeded) { $analyzeText | ConvertFrom-Json } else { $null }
$stepText = Get-Content -LiteralPath $outputStep -Raw
$markers = [ordered]@{}
foreach ($marker in @('SURFACE_OF_LINEAR_EXTRUSION', 'SURFACE_OF_REVOLUTION', 'B_SPLINE_SURFACE_WITH_KNOTS', 'ELLIPSE', 'LINE', 'CIRCLE')) {
    $markers[$marker] = ([regex]::Matches($stepText, [regex]::Escape($marker))).Count
}

$freeCadResult = [ordered]@{ attempted = [bool]$FreeCAD; succeeded = $null; output = $null }
if ($FreeCAD) {
    $validator = Join-Path $repoRoot 'tools\Validate-Step-FreeCAD.ps1'
    $freeCadOutput = & $validator $outputStep 2>&1 | Out-String
    $freeCadResult.succeeded = $LASTEXITCODE -eq 0
    $freeCadResult.output = $freeCadOutput.Trim()
}

[ordered]@{
    toolingOnly = $true
    probe = $probePath
    inlineStepInput = $canonicalInput
    wrapper = $wrapperPath
    outputStep = $outputStep
    reimportSucceeded = $reimportSucceeded
    analyzeSucceeded = $analyzeSucceeded
    analyzeSummary = $analyze.summary
    surfaceEvidence = $markers
    freeCad = $freeCadResult
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

if (-not $Keep) {
    Remove-Item -LiteralPath $reimportStep -Force -ErrorAction SilentlyContinue
}

Write-Host "Ruled STEP probe receipt"
Write-Host "  InlineStep input:  $canonicalInput"
Write-Host "  Wrapper:           $wrapperPath"
Write-Host "  AP242 output:      $outputStep"
Write-Host "  Report:            $reportPath"

if ($Open) {
    Start-Process -FilePath $outputRoot | Out-Null
}
