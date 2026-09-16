[CmdletBinding()]
param(
    [string]$Candidate = 'artifacts/local/humanoid-x1/antonia-adoption-candidate.json',
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [string[]]$Fbx = @(),
    [switch]$SkipRender,
    [switch]$SkipFullTests
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $outDir = 'artifacts/local/humanoid-x2'
    if (-not (Test-Path -LiteralPath $Candidate)) { throw 'Generate the X1 Antonia candidate first using qualify-humanoid-x1.ps1.' }
    New-Item -ItemType Directory -Force $outDir | Out-Null
    dotnet build tools/Aetheris.Humanoid.X0/Aetheris.Humanoid.X0.csproj > "$outDir/tool-build.log"
    if ($LASTEXITCODE -ne 0) { throw 'Humanoid research tool build failed.' }
    foreach ($target in @($outDir, "$outDir/replay")) {
        dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- constrained-antonia --input $Candidate --out-dir $target
        if ($LASTEXITCODE -ne 0) { throw 'Constrained Antonia qualification failed.' }
    }
    $files = @('evidence.json') + @(Get-ChildItem -LiteralPath $outDir -File | Where-Object { $_.Name -like '*.obj' -or $_.Name -like '*.mechanism.json' } | Select-Object -ExpandProperty Name)
    foreach ($file in $files) {
        if ((Get-FileHash "$outDir/$file").Hash -ne (Get-FileHash "$outDir/replay/$file").Hash) { throw "Replay mismatch: $file" }
    }
    $evidence = Get-Content "$outDir/evidence.json" -Raw | ConvertFrom-Json
    if ($evidence.cases.Count -ne 20 -or @($evidence.cases | Where-Object isSolved).Count -ne 17) { throw 'Unexpected constrained corpus outcomes.' }
    if (@($evidence.cases | Where-Object { $_.isSolved -and -not $_.deterministic }).Count -ne 0) { throw 'Nondeterministic solve.' }
    if (-not $SkipRender) {
        $renderArgs = @('--background', '--factory-startup', '--disable-autoexec', '--python-exit-code', '1', '--python', 'scripts/render-humanoid-x2.py', '--')
        foreach ($path in $Fbx) { $renderArgs += @('--fbx', $path) }
        & $Blender @renderArgs > "$outDir/render.log"
        if ($LASTEXITCODE -ne 0) { throw 'X2 diagnostic rendering failed.' }
    }
    dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj --verbosity minimal > "$outDir/focused-tests.log"
    if ($LASTEXITCODE -ne 0) { throw 'Humanoid tests failed.' }
    if (-not $SkipFullTests) {
        dotnet build Aetheris.slnx -m:1 > "$outDir/build.log"
        if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
        dotnet test Aetheris.slnx --no-build --filter 'Category!=SlowCorpus' --verbosity minimal > "$outDir/full-tests.log"
        if ($LASTEXITCODE -ne 0) { throw 'Solution tests failed; inspect full-tests.log.' }
    }
    Write-Host 'Constrained progression reproduced. X2 is NOT Accepted; surface/shoulder/migration gates remain unmet.'
}
finally { Pop-Location }
