[CmdletBinding()]
param(
    [string]$Candidate = 'artifacts/local/humanoid-x1/antonia-adoption-candidate.json',
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [switch]$SkipRender,
    [switch]$SkipFullTests
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $outDir = 'artifacts/local/humanoid-x4'
    $replayDir = "$outDir/replay"
    New-Item -ItemType Directory -Force $outDir, $replayDir | Out-Null
    if (-not (Test-Path -LiteralPath $Candidate)) { throw 'Generate the X1 Antonia candidate first.' }
    if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender is missing: $Blender" }

    & "$PSScriptRoot/inspect-antonia-deformation-source.ps1" -SkipFetch
    foreach ($target in @($outDir, $replayDir)) {
        & $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/extract-antonia-x4-source.py -- --candidate $Candidate --out-dir $target > "$target/extract.log"
        if ($LASTEXITCODE -ne 0) { throw "X4 neutral source extraction failed: $target" }
    }
    foreach ($name in @('topology-mapping.json','frame-audit.json','source-neutral.json','source-neutral.obj','source-extraction-manifest.json')) {
        if ((Get-FileHash "$outDir/$name").Hash -ne (Get-FileHash "$replayDir/$name").Hash) { throw "X4 source replay mismatch: $name" }
    }

    dotnet build tools/Aetheris.Humanoid.X0/Aetheris.Humanoid.X0.csproj > "$outDir/tool-build.log"
    if ($LASTEXITCODE -ne 0) { throw 'X4 comparison tool build failed.' }
    dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- compare-source-pose --input $Candidate --source-pose "$outDir/source-neutral.json" --output "$outDir/neutral-comparison.json" > "$outDir/compare.log"
    if ($LASTEXITCODE -ne 0) { throw 'X4 neutral comparison failed.' }
    dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- compare-source-pose --input $Candidate --source-pose "$replayDir/source-neutral.json" --output "$replayDir/neutral-comparison.json" > "$replayDir/compare.log"
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash "$outDir/neutral-comparison.json").Hash -ne (Get-FileHash "$replayDir/neutral-comparison.json").Hash) { throw 'X4 comparison replay mismatch.' }

    $manifest = Get-Content "$outDir/source-extraction-manifest.json" -Raw | ConvertFrom-Json
    if ($manifest.status -ne 'NeutralOnly-PosedSourceBlocked' -or $manifest.topology.canonicalVertices -ne 27193 -or
        $manifest.topology.admittedQuads -ne 27112 -or $manifest.topology.connectivityMatches -ne 27112 -or
        $manifest.topology.maximumNeutralErrorMm -gt .001) { throw 'X4 topology/neutral gate failed.' }
    $frames = Get-Content "$outDir/frame-audit.json" -Raw | ConvertFrom-Json
    if ($frames.comparisons.leftPoserToAetheris.distanceMm -lt 108 -or $frames.comparisons.leftPoserToAetheris.distanceMm -gt 110 -or
        $frames.comparisons.leftBlenderToPoser.distanceMm -gt 2 -or $frames.symmetry.blenderMirrorResidualMm -gt .001) {
        throw 'X4 hip frame audit regression failed.'
    }
    $comparison = Get-Content "$outDir/neutral-comparison.json" -Raw | ConvertFrom-Json
    $hipRegions = @($comparison.selectedDiff.regions | Where-Object { $_.region -in @('Pelvis','LeftThigh') })
    if (-not $comparison.aetheris.isSuccess -or $hipRegions.Count -ne 2 -or
        @($hipRegions | Where-Object { $_.statistics.maximumMm -gt .001 }).Count -ne 0) { throw 'X4 hip-region neutral gate failed.' }

    if (-not $SkipRender) {
        & $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render-humanoid-x4.py -- --out-dir $outDir > "$outDir/render.log"
        if ($LASTEXITCODE -ne 0) { throw 'X4 frame overlay rendering failed.' }
    }
    dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj --verbosity minimal > "$outDir/humanoid-tests.log"
    if ($LASTEXITCODE -ne 0) { throw 'Humanoid focused tests failed.' }
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
    Write-Host 'X4 meaningful progression reproduced: neutral topology/frame evidence is deterministic; admitted posed-source deformation remains blocked.'
}
finally { Pop-Location }
