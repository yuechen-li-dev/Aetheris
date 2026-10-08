param(
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    [switch]$SkipFullTests
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $bundle = "artifacts/local/humanoid-production"
    $candidate = "artifacts/local/humanoid-x1/antonia-adoption-candidate.json"
    $rig = "fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json"
    $sourceWeights = "artifacts/local/humanoid-rerig-x1/golden-weights.json"
    $required = @(
        $Blender, $candidate, $rig, $sourceWeights,
        "artifacts/local/humanoid-rest-x1/canonical-apose.obj",
        "artifacts/local/humanoid-rest-x1/evidence.json",
        "artifacts/local/humanoid-rest-x2/selected-weights.json",
        "artifacts/local/humanoid-x1/source/Antonia-1.2.obj"
    )
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Missing retained input: $path. Reproduce the documented X1/RERIG/REST-X2 source pipeline first. No downloads are performed by this script."
        }
    }
    function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
        & $Executable @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed ($LASTEXITCODE): $Executable $($Arguments -join ' ')"
        }
    }
    if (-not $SkipFullTests) {
        Invoke-Checked "dotnet" @("build", "Aetheris.slnx", "-c", "Release", "-m:1")
        Invoke-Checked "dotnet" @("test", "Aetheris.slnx", "-c", "Release", "--no-build", "-m:1",
            "--", "RunConfiguration.MaxCpuCount=1")
    }
    $blenderArguments = @("--background", "--factory-startup", "--disable-autoexec",
        "--python-exit-code", "1", "--python", "scripts/build-antonia-gameplay.py", "--")
    Invoke-Checked $Blender ($blenderArguments + @("--phase", "inputs"))
    $qualificationArguments = @("run", "--project", "tools/Aetheris.Humanoid.X0", "-c", "Release", "--",
        "qualify-gameplay-body", "--input", $candidate, "--rig", $rig, "--source-weights", $sourceWeights,
        "--bundle", $bundle)
    Invoke-Checked "dotnet" ($qualificationArguments + @("--seed"))
    Invoke-Checked $Blender ($blenderArguments + @("--phase", "replay"))
    Invoke-Checked $Blender ($blenderArguments + @("--phase", "verify"))
    Invoke-Checked "dotnet" $qualificationArguments
    Copy-Item -LiteralPath "fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt" -Destination "$bundle/ANTONIA-LICENSE.txt"
    Copy-Item -LiteralPath "docs/public/humanoid-gameplay-body.md" -Destination "$bundle/README.md"
    Write-Output "ANTONIA_GAMEPLAY_QUALIFIED $bundle"
}
finally {
    Pop-Location
}
