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

function Get-RepoRoot {
    $dir = Resolve-Path (Join-Path $PSScriptRoot '..')
    while ($null -ne $dir) {
        if (Test-Path -LiteralPath (Join-Path $dir.Path 'Aetheris.slnx')) {
            return $dir.Path
        }

        $parent = Split-Path -Parent $dir.Path
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $dir.Path) {
            break
        }

        $dir = Resolve-Path $parent
    }

    throw 'Unable to locate repository root from tools directory.'
}

function Get-SafeProbeName([string]$RawName) {
    if ([string]::IsNullOrWhiteSpace($RawName)) {
        return 'ruledProbe'
    }

    $safe = $RawName.Trim()
    foreach ($invalid in [System.IO.Path]::GetInvalidFileNameChars()) {
        $safe = $safe.Replace([string]$invalid, '-')
    }

    $safe = [System.Text.RegularExpressions.Regex]::Replace($safe, '\s+', '-')
    $safe = $safe.Trim('.').Trim('-')
    if ([string]::IsNullOrWhiteSpace($safe)) {
        return 'ruledProbe'
    }

    return $safe
}

function Get-SafeIdentifier([string]$RawName) {
    $identifier = [System.Text.RegularExpressions.Regex]::Replace($RawName, '[^A-Za-z0-9_]', '_')
    if ([string]::IsNullOrWhiteSpace($identifier)) {
        $identifier = 'probe'
    }

    if ($identifier[0] -match '[0-9]') {
        $identifier = "probe_$identifier"
    }

    return $identifier
}

function Invoke-CliCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    if (-not $script:CliReady) {
        $buildOutput = & dotnet build 'Aetheris.CLI\Aetheris.CLI.csproj' -f net10.0 --nologo 2>&1 | ForEach-Object { $_.ToString() }
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build Aetheris.CLI before probe execution.$([Environment]::NewLine)$($buildOutput -join [Environment]::NewLine)"
        }

        $script:CliDllPath = Join-Path $script:RepoRoot 'Aetheris.CLI\bin\Debug\net10.0\aetheris.dll'
        if (-not (Test-Path -LiteralPath $script:CliDllPath)) {
            throw "Expected built CLI at '$script:CliDllPath' after dotnet build."
        }

        $script:CliReady = $true
    }

    $output = & dotnet $script:CliDllPath @Arguments 2>&1 | ForEach-Object { $_.ToString() }
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        StdOut = ($output -join [Environment]::NewLine)
        StdErr = ''
        Arguments = $Arguments
    }
}

function Get-EvidenceSummary([string]$StepText) {
    $tokens = @(
        'SURFACE_OF_LINEAR_EXTRUSION',
        'SURFACE_OF_REVOLUTION',
        'B_SPLINE_SURFACE_WITH_KNOTS',
        'ELLIPSE',
        'LINE',
        'CIRCLE',
        'CONICAL_SURFACE',
        'CYLINDRICAL_SURFACE'
    )

    $result = [ordered]@{}
    foreach ($token in $tokens) {
        $count = ([regex]::Matches($StepText, [regex]::Escape($token))).Count
        $result[$token] = [ordered]@{
            present = ($count -gt 0)
            count = $count
        }
    }

    return $result
}

function Write-CommandArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        $Result
    )

    $artifact = [ordered]@{
        exitCode = $Result.ExitCode
        arguments = $Result.Arguments
        stdout = $Result.StdOut
        stderr = $Result.StdErr
    } | ConvertTo-Json -Depth 6

    Set-Content -LiteralPath $BasePath -Value $artifact -Encoding UTF8
}

$RepoRoot = Get-RepoRoot
$resolvedProbe = (Resolve-Path -LiteralPath $Probe).Path

if (-not (Test-Path -LiteralPath $resolvedProbe)) {
    throw "Probe STEP file not found: $Probe"
}

$requestedName = if ([string]::IsNullOrWhiteSpace($Name)) { [System.IO.Path]::GetFileNameWithoutExtension($resolvedProbe) } else { $Name }
$probeName = Get-SafeProbeName $requestedName
$wrapperIdentifier = Get-SafeIdentifier $requestedName

$rootOut = if ([string]::IsNullOrWhiteSpace($Out)) {
    Join-Path $RepoRoot "demo-output\ruled-probes\$probeName"
}
else {
    $candidate = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $RepoRoot $Out }
    [System.IO.Path]::GetFullPath($candidate)
}

$inputDir = Join-Path $rootOut 'input'
$wrapperDir = Join-Path $rootOut 'wrapper'
$outputDir = Join-Path $rootOut 'output'

New-Item -ItemType Directory -Force -Path $inputDir, $wrapperDir, $outputDir | Out-Null

$originalProbeName = [System.IO.Path]::GetFileName($resolvedProbe)
$rawProbeCopyPath = Join-Path $inputDir $originalProbeName
$canonicalInputPath = Join-Path $inputDir ($probeName + '.canonical-input.step')
$wrapperPath = Join-Path $wrapperDir ($probeName + '.firm')
$outputStepPath = Join-Path $outputDir ($probeName + '.canonical.step')
$reimportSmokePath = Join-Path $outputDir ($probeName + '.reimport-smoke.step')
$reportPath = Join-Path $outputDir 'probe-report.json'

Copy-Item -LiteralPath $resolvedProbe -Destination $rawProbeCopyPath -Force

$canonResult = Invoke-CliCommand -Arguments @('canon', $rawProbeCopyPath, '--out', $canonicalInputPath, '--mode', 'production', '--json')
if ($Keep) {
    Write-CommandArtifact -BasePath (Join-Path $outputDir 'canon-result.json') -Result $canonResult
}

if ($canonResult.ExitCode -ne 0) {
    if (-not [string]::IsNullOrWhiteSpace($canonResult.StdErr)) {
        Write-Error $canonResult.StdErr.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($canonResult.StdOut)) {
        Write-Host $canonResult.StdOut.Trim()
    }

    throw "Canonicalization failed for probe '$resolvedProbe'."
}

$wrapperRelativeInput = "../input/$([System.IO.Path]::GetFileName($canonicalInputPath))"
 $wrapperText = @"
model ${wrapperIdentifier}ProbeHarness {
    units mm

    solid ${wrapperIdentifier}: InlineStep {
        path: "$wrapperRelativeInput"
    }
}
"@

Set-Content -LiteralPath $wrapperPath -Value $wrapperText -Encoding UTF8

$buildResult = Invoke-CliCommand -Arguments @('build', $wrapperPath, '--out', $outputStepPath, '--json')
if ($Keep) {
    Write-CommandArtifact -BasePath (Join-Path $outputDir 'build-result.json') -Result $buildResult
}

if ($buildResult.ExitCode -ne 0) {
    if (-not [string]::IsNullOrWhiteSpace($buildResult.StdErr)) {
        Write-Error $buildResult.StdErr.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($buildResult.StdOut)) {
        Write-Host $buildResult.StdOut.Trim()
    }

    throw "Build failed for wrapper '$wrapperPath'."
}

$reimportResult = Invoke-CliCommand -Arguments @('canon', $outputStepPath, '--out', $reimportSmokePath, '--json')
if ($Keep) {
    Write-CommandArtifact -BasePath (Join-Path $outputDir 'reimport-result.json') -Result $reimportResult
}

$reimportSucceeded = ($reimportResult.ExitCode -eq 0)
if (-not $Keep -and (Test-Path -LiteralPath $reimportSmokePath)) {
    Remove-Item -LiteralPath $reimportSmokePath -Force -ErrorAction SilentlyContinue
}

$analyzeResult = Invoke-CliCommand -Arguments @('analyze', $outputStepPath, '--json')
if ($Keep) {
    Write-CommandArtifact -BasePath (Join-Path $outputDir 'analyze-result.json') -Result $analyzeResult
}

$analyzeSucceeded = ($analyzeResult.ExitCode -eq 0)
$analyzeJson = $null
if ($analyzeSucceeded -and -not [string]::IsNullOrWhiteSpace($analyzeResult.StdOut)) {
    $analyzeJson = $analyzeResult.StdOut | ConvertFrom-Json
}

$outputStepText = Get-Content -LiteralPath $outputStepPath -Raw
$evidence = Get-EvidenceSummary $outputStepText

$freeCadStatus = [ordered]@{
    attempted = [bool]$FreeCAD
    available = $false
    importSucceeded = $null
    shapeValid = $null
    skipped = $false
    exitCode = $null
    command = $null
    output = $null
}

if ($FreeCAD) {
    $validatorPath = Join-Path $RepoRoot 'tools\Validate-Step-FreeCAD.ps1'
    if (Test-Path -LiteralPath $validatorPath) {
        $freeCadCmd = Get-Command FreeCADCmd.exe, FreeCADCmd, freecadcmd -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $freeCadCmd) {
            $freeCadStatus.available = $true
            $freeCadStatus.command = "$validatorPath $outputStepPath"

            $freeCadOutput = & $validatorPath $outputStepPath 2>&1 | Out-String
            $freeCadExitCode = $LASTEXITCODE
            $freeCadStatus.exitCode = $freeCadExitCode
            $freeCadStatus.output = $freeCadOutput.Trim()
            $freeCadStatus.importSucceeded = ($freeCadExitCode -eq 0 -or $freeCadExitCode -eq 3)
            $freeCadStatus.shapeValid = ($freeCadOutput -match 'shape_valid=true')

            if ($Keep) {
                Set-Content -LiteralPath (Join-Path $outputDir 'freecad-output.txt') -Value $freeCadOutput -Encoding UTF8
            }
        }
        else {
            $freeCadStatus.skipped = $true
            $freeCadStatus.output = 'Skipped: FreeCADCmd was not found on PATH.'
        }
    }
    else {
        $freeCadStatus.skipped = $true
        $freeCadStatus.output = 'Skipped: tools/Validate-Step-FreeCAD.ps1 is not present in this repository.'
    }
}

$report = [ordered]@{
    toolingOnly = $true
    probe = $resolvedProbe
    probeName = $probeName
    rawProbeCopy = $rawProbeCopyPath
    inlineStepInput = $canonicalInputPath
    wrapper = $wrapperPath
    outputStep = $outputStepPath
    buildSucceeded = $true
    reimportSucceeded = $reimportSucceeded
    analyzeSucceeded = $analyzeSucceeded
    analyzeSummary = if ($null -ne $analyzeJson) { $analyzeJson.summary } else { $null }
    analyzeRaw = if ($analyzeSucceeded) { $null } else { $analyzeResult.StdOut }
    surfaceEvidence = $evidence
    freeCad = $freeCadStatus
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Ruled STEP probe receipt"
Write-Host "  Input probe:            $resolvedProbe"
Write-Host "  Input copy:             $rawProbeCopyPath"
Write-Host "  InlineStep input:       $canonicalInputPath"
Write-Host "  Generated wrapper:      $wrapperPath"
Write-Host "  Canonical AP242 output: $outputStepPath"
Write-Host "  Reimport status:        $(if ($reimportSucceeded) { 'succeeded' } else { 'failed' })"
Write-Host "  Analyze status:         $(if ($analyzeSucceeded) { 'succeeded' } else { 'failed (best-effort only)' })"
Write-Host "  Report:                 $reportPath"
Write-Host "  Entity evidence:"

foreach ($token in $evidence.Keys) {
    $entry = $evidence[$token]
    Write-Host "    $token => present=$($entry.present) count=$($entry.count)"
}

if ($FreeCAD) {
    if ($freeCadStatus.available) {
        Write-Host "  FreeCAD:                exit=$($freeCadStatus.exitCode) shapeValid=$($freeCadStatus.shapeValid)"
    }
    else {
        Write-Host "  FreeCAD:                skipped ($($freeCadStatus.output))"
    }
}

if (-not $reimportSucceeded) {
    if (-not [string]::IsNullOrWhiteSpace($reimportResult.StdOut)) {
        Write-Host $reimportResult.StdOut.Trim()
    }

    throw "Reimport/analyze failed for '$outputStepPath'."
}

if ($Open) {
    Start-Process $rootOut | Out-Null
}
