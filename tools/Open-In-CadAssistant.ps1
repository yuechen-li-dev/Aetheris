[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [Alias('StepFile')]
    [string]$Step,

    [string]$CadAssistantPath,

    [string]$BuildReport,

    [string]$AnalyzeReport,

    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:DefaultCadAssistantRoot = 'C:\Program Files\CAD Assistant'

function Resolve-AetherisExistingFile {
    param(
        [Parameter(Mandatory = $true)] [string]$Path,
        [Parameter(Mandatory = $true)] [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw [System.IO.FileNotFoundException]::new("$Description not found: $Path", $Path)
    }

    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
}

function Get-CadAssistantCandidates {
    param([Parameter(Mandatory = $true)] [string]$Directory)

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        throw [System.IO.DirectoryNotFoundException]::new("CAD Assistant directory not found: $Directory")
    }

    # The documented/current application is CADAssistant.exe. The fallback keeps
    # discovery useful for versioned distributions without accepting updater or
    # uninstaller executables as viewer candidates.
    $exactCandidates = @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter 'CADAssistant.exe' |
        Sort-Object -Property FullName)
    if ($exactCandidates.Count -eq 1) {
        return $exactCandidates
    }
    if ($exactCandidates.Count -gt 1) {
        throw "Multiple CAD Assistant executables named CADAssistant.exe were found under '$Directory': $($exactCandidates.FullName -join '; ')"
    }

    $candidates = @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.exe' |
        Where-Object { $_.BaseName -match '^CAD[ _-]?Assistant(?:[ _-].+)?$' } |
        Sort-Object -Property FullName)
    if ($candidates.Count -eq 1) {
        return $candidates
    }
    if ($candidates.Count -eq 0) {
        throw "No CAD Assistant viewer executable was found under '$Directory'. Expected CADAssistant.exe or one unambiguous CAD Assistant executable."
    }

    throw "Multiple ambiguous CAD Assistant executable candidates were found under '$Directory': $($candidates.FullName -join '; ')"
}

function Resolve-CadAssistantExecutable {
    param([string]$OverridePath)

    $candidatePath = if (-not [string]::IsNullOrWhiteSpace($OverridePath)) {
        $OverridePath
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:AETHERIS_CAD_ASSISTANT_EXE)) {
        $env:AETHERIS_CAD_ASSISTANT_EXE
    }
    else {
        $script:DefaultCadAssistantRoot
    }

    if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
        $resolved = (Resolve-Path -LiteralPath $candidatePath -ErrorAction Stop).Path
        if ([System.IO.Path]::GetExtension($resolved) -ine '.exe') {
            throw "CAD Assistant executable path must end in .exe: $resolved"
        }
        return $resolved
    }

    if (Test-Path -LiteralPath $candidatePath -PathType Container) {
        return (Get-CadAssistantCandidates -Directory $candidatePath).FullName
    }

    throw "CAD Assistant path not found: $candidatePath. Supply -CadAssistantPath <executable-or-directory>, set AETHERIS_CAD_ASSISTANT_EXE, or install under '$script:DefaultCadAssistantRoot'."
}

function Get-CadAssistantLaunchSpec {
    param(
        [Parameter(Mandatory = $true)] [string]$StepPath,
        [string]$CadAssistantOverride,
        [string]$BuildReportPath,
        [string]$AnalyzeReportPath
    )

    $resolvedStep = Resolve-AetherisExistingFile -Path $StepPath -Description 'STEP file'
    if ([System.IO.Path]::GetExtension($resolvedStep) -notin '.step', '.stp') {
        throw "Expected a .step or .stp file: $resolvedStep"
    }

    $spec = [ordered]@{
        StepPath = $resolvedStep
        Sha256 = (Get-FileHash -LiteralPath $resolvedStep -Algorithm SHA256).Hash
        CadAssistantExecutable = Resolve-CadAssistantExecutable -OverridePath $CadAssistantOverride
        BuildReportPath = $null
        AnalyzeReportPath = $null
    }

    if (-not [string]::IsNullOrWhiteSpace($BuildReportPath)) {
        $spec.BuildReportPath = Resolve-AetherisExistingFile -Path $BuildReportPath -Description 'Build report'
    }
    if (-not [string]::IsNullOrWhiteSpace($AnalyzeReportPath)) {
        $spec.AnalyzeReportPath = Resolve-AetherisExistingFile -Path $AnalyzeReportPath -Description 'Analyze report'
    }

    return [pscustomobject]$spec
}

function Invoke-CadAssistantLaunch {
    param([Parameter(Mandatory = $true)] $LaunchSpec)

    # ArgumentList is an array: Start-Process receives this exact resolved path as
    # one argument, including when the artifact path contains spaces.
    return Start-Process -FilePath $LaunchSpec.CadAssistantExecutable -ArgumentList @($LaunchSpec.StepPath) -PassThru -ErrorAction Stop
}

function Invoke-OpenInCadAssistant {
    param(
        [Parameter(Mandatory = $true)] [string]$StepPath,
        [string]$CadAssistantOverride,
        [string]$BuildReportPath,
        [string]$AnalyzeReportPath,
        [switch]$SkipLaunch
    )

    $spec = Get-CadAssistantLaunchSpec -StepPath $StepPath -CadAssistantOverride $CadAssistantOverride -BuildReportPath $BuildReportPath -AnalyzeReportPath $AnalyzeReportPath
    Write-Host "CAD Assistant executable: $($spec.CadAssistantExecutable)"
    Write-Host "STEP artifact: $($spec.StepPath)"
    Write-Host "STEP SHA-256: $($spec.Sha256)"
    if ($spec.BuildReportPath) { Write-Host "Build report: $($spec.BuildReportPath)" }
    if ($spec.AnalyzeReportPath) { Write-Host "Analyze report: $($spec.AnalyzeReportPath)" }

    if ($SkipLaunch) {
        Write-Host 'Launch skipped (-NoLaunch).'
        return $spec
    }

    $process = Invoke-CadAssistantLaunch -LaunchSpec $spec
    Write-Host "CAD Assistant launched successfully (PID $($process.Id))."
    return $spec
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        if ([string]::IsNullOrWhiteSpace($Step)) {
            throw [System.IO.FileNotFoundException]::new('A STEP file is required. Supply -Step <path>.')
        }
        Invoke-OpenInCadAssistant -StepPath $Step -CadAssistantOverride $CadAssistantPath -BuildReportPath $BuildReport -AnalyzeReportPath $AnalyzeReport -SkipLaunch:$NoLaunch | Out-Null
        exit 0
    }
    catch [System.IO.FileNotFoundException] {
        [Console]::Error.WriteLine($_.Exception.Message)
        exit 2
    }
    catch [System.IO.DirectoryNotFoundException] {
        [Console]::Error.WriteLine($_.Exception.Message)
        exit 3
    }
    catch {
        [Console]::Error.WriteLine($_.Exception.Message)
        exit 4
    }
}
