[CmdletBinding()]
param(
    [string]$OutputRoot = "artifacts/local/sheet-metal-bracket-demo"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$engineeringPath = Join-Path $outputPath "engineering"
$evidencePath = Join-Path $outputPath "evidence"
$sourcePath = Join-Path $repoRoot "fixtures/InvestorPitch/sheet-metal-bracket.firmament"
$analysisPath = Join-Path $repoRoot "fixtures/InvestorPitch/sheet-metal-bracket-analysis.firmament"
$counterboreSource = Join-Path $repoRoot "fixtures/Canonical/PMI/counterbore-shaft-diameter.firmament"
$cliProject = Join-Path $repoRoot "Aetheris.CLI"

New-Item -ItemType Directory -Force -Path $engineeringPath, $evidencePath | Out-Null

function Invoke-AetherisJson {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string[]]$CliArguments,
        [switch]$AllowFailure
    )

    $priorLocation = Get-Location
    try {
        Set-Location $repoRoot
        $jsonText = (& dotnet run --project $cliProject -- @CliArguments 2>&1 | Out-String).Trim()
        $exitCode = $LASTEXITCODE
    }
    finally {
        Set-Location $priorLocation
    }
    $jsonFile = Join-Path $evidencePath "$Name.json"
    [IO.File]::WriteAllText($jsonFile, $jsonText + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Aetheris command '$Name' failed with exit code $exitCode. See $jsonFile."
    }
    return $jsonFile
}

$formedStep = Join-Path $engineeringPath "investor-bracket-formed.step"
$flatStep = Join-Path $engineeringPath "investor-bracket-flat.step"
$flatSvg = Join-Path $engineeringPath "investor-bracket-flat.svg"
$isoSvg = Join-Path $engineeringPath "investor-bracket-iso.svg"
$rightSvg = Join-Path $engineeringPath "investor-bracket-right.svg"
$counterboreStep = Join-Path $engineeringPath "counterbore-semantic-witness.step"
$counterboreSvg = Join-Path $engineeringPath "counterbore-semantic-witness.svg"

Invoke-AetherisJson -Name "validate" -CliArguments @("validate", $sourcePath, "--json") | Out-Null
Invoke-AetherisJson -Name "build-formed" -CliArguments @("build", $sourcePath, "--output", $formedStep, "--json") | Out-Null
Invoke-AetherisJson -Name "flatten" -CliArguments @("sheetmetal", "flatten", $sourcePath, "--step", $flatStep, "--svg", $flatSvg, "--json") | Out-Null
Invoke-AetherisJson -Name "analyze-formed" -CliArguments @("analyze", $formedStep, "--json") | Out-Null
Invoke-AetherisJson -Name "analyze-flat" -CliArguments @("analyze", $flatStep, "--json") | Out-Null
Invoke-AetherisJson -Name "wireframe-iso" -CliArguments @("wireframe", $formedStep, "--out", $isoSvg, "--view", "iso", "--density", "12", "--samples", "96", "--json") | Out-Null
Invoke-AetherisJson -Name "wireframe-right" -CliArguments @("wireframe", $formedStep, "--out", $rightSvg, "--view", "right", "--density", "12", "--samples", "96", "--json") | Out-Null
Invoke-AetherisJson -Name "counterbore-build" -CliArguments @("build", $counterboreSource, "--output", $counterboreStep, "--json") | Out-Null
Invoke-AetherisJson -Name "counterbore-wireframe" -CliArguments @("wireframe", $counterboreStep, "--out", $counterboreSvg, "--view", "iso", "--density", "10", "--samples", "96", "--json") | Out-Null
Invoke-AetherisJson -Name "counterbore-analyze" -CliArguments @("analyze", $counterboreStep, "--json") | Out-Null
Invoke-AetherisJson -Name "fea-attempt" -CliArguments @("fea", $analysisPath, "--out-dir", (Join-Path $engineeringPath "fea"), "--json") -AllowFailure | Out-Null

$hashes = Get-ChildItem -LiteralPath $engineeringPath -File | Sort-Object Name | ForEach-Object {
    [ordered]@{
        file = $_.Name
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$hashJson = $hashes | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $evidencePath "artifact-hashes.json"), $hashJson + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

Write-Host "Aetheris sheet-metal bracket evidence written to $outputPath"
