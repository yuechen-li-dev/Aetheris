[CmdletBinding()]
param(
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    [switch]$SkipFetch,
    [switch]$SkipFullTests
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "The required .NET SDK is not available on PATH." }
    if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender was not found at: $Blender" }
    if (-not $SkipFetch) { python scripts/fetch-humanoid-recon-x0.py }
    dotnet run --project tools/Aetheris.Humanoid.X0 -- generate --out-dir artifacts/local/humanoid-x0
    dotnet run --project tools/Aetheris.Humanoid.X0 -- inspect --input artifacts/local/humanoid-x0/canonical-adult-standard-v1.json | Set-Content artifacts/local/humanoid-x0/inspect.json
    & $Blender --background --factory-startup --disable-autoexec --python scripts/qualify-humanoid-x0.py -- --repo $repo
    if ($LASTEXITCODE -ne 0) { throw "Blender registration qualification failed with exit code $LASTEXITCODE." }
    dotnet run --project tools/Aetheris.Humanoid.X0 -- sweep --input artifacts/local/humanoid-x0/canonical-adult-standard-v1.json --output artifacts/local/humanoid-x0/pose-sweep-evidence.json
    dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj --no-restore --verbosity minimal
    if (-not $SkipFullTests) {
        dotnet build Aetheris.slnx -m:1
        dotnet test Aetheris.slnx --no-build --verbosity minimal
    }
    Write-Host "HUMANOID-X0 replay complete. See artifacts/local/humanoid-x0 and docs/release/HUMANOID-X0.md."
}
finally { Pop-Location }
