[CmdletBinding()]
param(
    [string]$Candidate = 'artifacts/local/humanoid-x1/antonia-adoption-candidate.json',
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [switch]$SkipFetch,
    [switch]$SkipRender,
    [switch]$SkipFullTests
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $outDir = 'artifacts/local/humanoid-x3'
    New-Item -ItemType Directory -Force $outDir | Out-Null
    if (-not (Test-Path -LiteralPath $Candidate)) { throw 'Generate the X1 Antonia candidate first.' }
    & "$PSScriptRoot/inspect-antonia-deformation-source.ps1" -SkipFetch:$SkipFetch
    dotnet build tools/Aetheris.Humanoid.X0/Aetheris.Humanoid.X0.csproj > "$outDir/tool-build.log"
    if ($LASTEXITCODE -ne 0) { throw 'Judgment research tool build failed.' }
    foreach ($target in @($outDir, "$outDir/replay")) {
        dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- judge-antonia --input $Candidate --out-dir $target
        if ($LASTEXITCODE -ne 0) { throw 'Judgment qualification command failed.' }
    }
    $e = Get-Content "$outDir/evidence.json" -Raw | ConvertFrom-Json
    if ($e.cases.Count -ne 9) { throw 'Unexpected hip pose corpus.' }
    foreach ($case in $e.cases) {
        if ($case.metrics.Count -ne 6) { throw "Unexpected candidate set: $($case.name)" }
        if ($case.isSuccess -and -not $case.finalSurfaceEvidence.isAdmissible) { throw 'Winner failed final screen.' }
        if (@($case.trace | Where-Object { -not $_.admissible -and $_.scores.Count -gt 0 }).Count -gt 0) { throw 'Inadmissible candidate was scored.' }
    }
    $files = @('evidence.json') + @(Get-ChildItem -LiteralPath $outDir -File | Where-Object { $_.Name -like '*.obj' -or $_.Name -like '*.trace.json' } | Select-Object -ExpandProperty Name)
    foreach ($file in $files) {
        if ((Get-FileHash "$outDir/$file").Hash -ne (Get-FileHash "$outDir/replay/$file").Hash) { throw "Replay mismatch: $file" }
    }
    if (-not $SkipRender) {
        & $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render-humanoid-x3.py > "$outDir/render.log"
        if ($LASTEXITCODE -ne 0) { throw 'Judgment diagnostic rendering failed.' }
    }
    dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj --verbosity minimal > "$outDir/humanoid-tests.log"
    if ($LASTEXITCODE -ne 0) { throw 'Humanoid tests failed.' }
    dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter 'FullyQualifiedName~Judgment|FullyQualifiedName~Chamfer|FullyQualifiedName~Fillet' --verbosity minimal > "$outDir/cad-core-tests.log"
    if ($LASTEXITCODE -ne 0) { throw 'Core Judgment/CAD regression failed.' }
    dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter 'FullyQualifiedName~SurfX2BlendBoundary|FullyQualifiedName~Fillet|FullyQualifiedName~Chamfer' --verbosity minimal > "$outDir/cad-firmament-tests.log"
    if ($LASTEXITCODE -ne 0) { throw 'Firmament/surfacing CAD regression failed.' }
    if (-not $SkipFullTests) {
        dotnet build Aetheris.slnx -m:1 > "$outDir/build.log"
        if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
        dotnet test Aetheris.slnx --no-build --filter 'Category!=SlowCorpus' --verbosity minimal > "$outDir/full-tests.log"
        if ($LASTEXITCODE -ne 0) { throw 'Full active suite failed; inspect full-tests.log.' }
    }
    Write-Host 'X3 judgment progression reproduced. Hip70/90 and abduction45 remain rejected; no acceptance claim.'
}
finally { Pop-Location }
