param(
    [Parameter(Mandatory = $true)] [string] $Probe,
    [Parameter(Mandatory = $true)] [string] $Name
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$probePath = Resolve-Path -Path $Probe

Write-Host "probe: $Name"
Write-Host "file: $probePath"

$analyze = & dotnet run --project (Join-Path $repoRoot 'Aetheris.CLI') -- analyze $probePath --json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "analyze: failed"
    Write-Host $analyze
    exit $LASTEXITCODE
}

$volume = & dotnet run --project (Join-Path $repoRoot 'Aetheris.CLI') -- analyze volume $probePath --json 2>&1
$volumeExit = $LASTEXITCODE
if ($volumeExit -eq 0) {
    Write-Host "analyze: volume supported"
    Write-Host $volume
    exit 0
}

$message = ($volume | Out-String)
if ($message -match 'given key') {
    Write-Host 'analyze: unexpected dictionary-key failure'
    Write-Host $message
    exit $volumeExit
}

if ($message -match 'Exact volume is not supported' -and $message -match 'linear-extrusion') {
    Write-Host 'analyze: expected unsupported-volume nonzero for open linear-extrusion body'
    Write-Host $message
    exit 0
}

if ($message -match 'Exact volume is not supported' -or $message -match 'unsupported non-planar face') {
    Write-Host 'analyze: expected unsupported-volume nonzero'
    Write-Host $message
    exit 0
}

Write-Host 'analyze: unexpected failure'
Write-Host $message
exit $volumeExit
