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
    $outDir = 'artifacts/local/humanoid-x5'
    $replayDir = "$outDir/replay"
    $rig = 'fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json'
    New-Item -ItemType Directory -Force $outDir, $replayDir | Out-Null
    if (-not (Test-Path -LiteralPath $Candidate)) { throw 'Generate the X1 Antonia candidate first.' }
    if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender is missing: $Blender" }

    & $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/extract-antonia-x5-rig.py -- --candidate $Candidate --output "$replayDir/antonia-reference-skeleton-v1.json" > "$replayDir/extract.log"
    if ($LASTEXITCODE -ne 0) { throw 'X5 deterministic rig extraction failed.' }
    if ((Get-FileHash $rig).Hash -ne (Get-FileHash "$replayDir/antonia-reference-skeleton-v1.json").Hash) { throw 'X5 checked reference-rig artifact is not reproducible.' }

    dotnet build tools/Aetheris.Humanoid.X0/Aetheris.Humanoid.X0.csproj > "$outDir/tool-build.log"
    if ($LASTEXITCODE -ne 0) { throw 'X5 qualification tool build failed.' }
    dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- reference-rig-antonia --input $Candidate --rig $rig --out-dir $outDir > "$outDir/qualify.log"
    if ($LASTEXITCODE -ne 0) { throw 'X5 qualification execution failed.' }
    dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- reference-rig-antonia --input $Candidate --rig $rig --out-dir $replayDir > "$replayDir/qualify.log"
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash "$outDir/evidence.json").Hash -ne (Get-FileHash "$replayDir/evidence.json").Hash) { throw 'X5 qualification replay mismatch.' }

    $evidence = Get-Content "$outDir/evidence.json" -Raw | ConvertFrom-Json
    if ($evidence.mappedJoints -ne 55 -or $evidence.sourceJoints -ne 182 -or
        $evidence.evidence.bindReconstructionMaximumMm -gt .001 -or
        $evidence.evidence.maximumSourceCenterDeltaMm -gt .001 -or
        $evidence.evidence.maximumSourceOrientationDeltaDegrees -gt .001 -or
        $evidence.evidence.maximumSymmetryCenterResidualMm -gt .001) { throw 'X5 frame/bind qualification gate failed.' }
    if (@($evidence.cases).Count -ne 16 -or @($evidence.cases | Where-Object { $_.maximumCenterResidualMm -gt .001 -or $_.maximumLinkResidualMm -gt .001 }).Count -ne 0) { throw 'X5 pose corpus mechanism gate failed.' }

    if (-not $SkipRender) {
        & $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render-humanoid-x5.py -- --out-dir $outDir --rig $rig > "$outDir/render.log"
        if ($LASTEXITCODE -ne 0) { throw 'X5 render failed.' }
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
    Write-Host 'X5 meaningful progression reproduced: reference frames are adopted and deterministic; unchanged skinning remains the isolated blocker.'
}
finally { Pop-Location }
