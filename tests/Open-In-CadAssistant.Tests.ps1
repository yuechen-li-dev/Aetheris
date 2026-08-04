$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot '..\tools\Open-In-CadAssistant.ps1')

$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("aetheris-cad-assistant-tests-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox | Out-Null

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

try {
    $spaceDir = Join-Path $sandbox 'path with spaces'
    New-Item -ItemType Directory -Path $spaceDir | Out-Null
    $step = Join-Path $spaceDir 'model.step'
    Set-Content -LiteralPath $step -Value 'ISO-10303-21;' -NoNewline
    $viewer = Join-Path $spaceDir 'CADAssistant.exe'
    New-Item -ItemType File -Path $viewer | Out-Null

    $spec = Get-CadAssistantLaunchSpec -StepPath $step -CadAssistantOverride $viewer
    Assert-True ($spec.StepPath -eq (Resolve-Path -LiteralPath $step).Path) 'STEP path is resolved exactly.'
    Assert-True ($spec.CadAssistantExecutable -eq (Resolve-Path -LiteralPath $viewer).Path) 'Explicit executable override resolves correctly.'
    Assert-True ($spec.Sha256 -eq (Get-FileHash -LiteralPath $step -Algorithm SHA256).Hash) 'SHA-256 is reported correctly.'
    Assert-True ($spec.StepPath -like '*path with spaces*') 'Paths with spaces remain a single resolved path.'

    $fromDirectory = Resolve-CadAssistantExecutable -OverridePath $spaceDir
    Assert-True ($fromDirectory -eq (Resolve-Path -LiteralPath $viewer).Path) 'Directory discovery resolves one obvious executable.'

    $otherViewer = Join-Path $spaceDir 'other-viewer.exe'
    New-Item -ItemType File -Path $otherViewer | Out-Null
    $priorViewerEnvironment = $env:AETHERIS_CAD_ASSISTANT_EXE
    try {
        $env:AETHERIS_CAD_ASSISTANT_EXE = $viewer
        Assert-True ((Resolve-CadAssistantExecutable) -eq (Resolve-Path -LiteralPath $viewer).Path) 'Environment executable override resolves correctly.'
        Assert-True ((Resolve-CadAssistantExecutable -OverridePath $otherViewer) -eq (Resolve-Path -LiteralPath $otherViewer).Path) 'Explicit executable override takes precedence over the environment.'
    }
    finally {
        $env:AETHERIS_CAD_ASSISTANT_EXE = $priorViewerEnvironment
    }

    $script:capturedFilePath = $null
    $script:capturedArgumentList = $null
    function Start-Process {
        param($FilePath, $ArgumentList, [switch]$PassThru, [switch]$ErrorAction)
        $script:capturedFilePath = $FilePath
        $script:capturedArgumentList = @($ArgumentList)
        return [pscustomobject]@{ Id = 12345 }
    }
    Invoke-CadAssistantLaunch -LaunchSpec $spec | Out-Null
    Assert-True ($script:capturedFilePath -eq $spec.CadAssistantExecutable) 'Launch command receives the discovered executable.'
    Assert-True ($script:capturedArgumentList.Count -eq 1 -and $script:capturedArgumentList[0] -eq $spec.StepPath) 'Launch command receives the exact STEP path as one argument.'
    Remove-Item -LiteralPath Function:\Start-Process

    $missingStepFailed = $false
    try { Get-CadAssistantLaunchSpec -StepPath (Join-Path $sandbox 'missing.step') -CadAssistantOverride $viewer | Out-Null } catch { $missingStepFailed = $true }
    Assert-True $missingStepFailed 'Missing STEP path fails.'

    $missingViewerFailed = $false
    try { Resolve-CadAssistantExecutable -OverridePath (Join-Path $sandbox 'missing.exe') | Out-Null } catch { $missingViewerFailed = $true }
    Assert-True $missingViewerFailed 'Missing CAD Assistant executable fails clearly.'

    if (Test-Path -LiteralPath $script:DefaultCadAssistantRoot -PathType Container) {
        $actualDefault = Resolve-CadAssistantExecutable
        Assert-True ($actualDefault -eq 'C:\Program Files\CAD Assistant\CADAssistant.exe') 'Current-machine default installation discovery resolves CADAssistant.exe.'
    }
    else {
        Write-Host 'Default installation discovery test skipped: CAD Assistant is not installed in the default location.'
    }

    Write-Host 'Open-In-CadAssistant tooling tests passed.'
}
finally {
    Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
}
