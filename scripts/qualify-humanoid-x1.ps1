[CmdletBinding()]
param(
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    [switch]$SkipFetch,
    [switch]$SkipRender,
    [switch]$SkipFullTests
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $outDir = 'artifacts/local/humanoid-x1'
    $sourceDir = Join-Path $outDir 'source'
    New-Item -ItemType Directory -Force $sourceDir | Out-Null
    $sidecar = Get-Content fixtures/Canonical/Humanoid/antonia-original.sidecar.json -Raw | ConvertFrom-Json
    foreach ($file in $sidecar.files) {
        $target = Join-Path $sourceDir $file.localName
        if (-not (Test-Path -LiteralPath $target)) {
            if ($SkipFetch) { throw "Missing pinned input: $target" }
            $url = 'https://raw.githubusercontent.com/odf/Antonia.Polygon/' + $sidecar.revision + '/' + $file.path
            Invoke-WebRequest $url -OutFile $target
        }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) {
            throw "Pinned source hash mismatch: $target"
        }
    }
    dotnet run --project tools/Aetheris.Humanoid.X0 -- adopt-antonia --source "$sourceDir/Antonia-1.2.obj" --notice "$sourceDir/README" --out-dir $outDir > "$outDir/run.log"
    if ($LASTEXITCODE -ne 0) { throw "Antonia adoption command failed: $LASTEXITCODE" }
    dotnet run --no-build --project tools/Aetheris.Humanoid.X0 -- adopt-antonia --source "$sourceDir/Antonia-1.2.obj" --notice "$sourceDir/README" --out-dir "$outDir/replay" > "$outDir/replay.log"
    if ($LASTEXITCODE -ne 0) { throw "Antonia replay failed: $LASTEXITCODE" }
    $replayFiles = @('antonia-adoption-candidate.json', 'adoption-evidence.json') + @(Get-ChildItem -LiteralPath $outDir -Filter '*.obj' -File | Select-Object -ExpandProperty Name)
    foreach ($name in $replayFiles) {
        if ((Get-FileHash "$outDir/$name").Hash -ne (Get-FileHash "$outDir/replay/$name").Hash) {
            throw "Deterministic replay mismatch: $name"
        }
    }
    $evidence = Get-Content "$outDir/adoption-evidence.json" -Raw | ConvertFrom-Json
    if ($evidence.selectedVertices -ne 27193 -or $evidence.selectedQuads -ne 27112 -or $evidence.joints -ne 55 -or
        $evidence.connectivityHash -ne '2e2f787499ba2da1f02bab14f352b86070938fbb5d7cae60e8bd7d59edb2b7a2' -or
        $evidence.symmetryMaximumMm -gt .001 -or $evidence.preparedSymmetryMaximumMm -gt .001 -or $evidence.maximumWeightSumError -gt .00001 -or
        $evidence.uncoveredVertices -ne 0 -or $evidence.preparationEvidence.bindReconstructionMaximumMm -gt .001) {
        throw 'Admitted-source topology/skin/bind regression failed.'
    }
    if (-not $SkipRender) {
        & $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render-humanoid-x1.py > "$outDir/render.log"
        if ($LASTEXITCODE -ne 0) { throw "Evidence render failed: $LASTEXITCODE" }
    }
    dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj --verbosity minimal > "$outDir/focused-tests.log"
    if ($LASTEXITCODE -ne 0) { throw "Humanoid tests failed: $LASTEXITCODE" }
    if (-not $SkipFullTests) {
        dotnet build Aetheris.slnx -m:1 > "$outDir/build.log"
        if ($LASTEXITCODE -ne 0) { throw "Solution build failed: $LASTEXITCODE" }
        dotnet test Aetheris.slnx --no-build --filter 'Category!=SlowCorpus' --verbosity minimal > "$outDir/full-tests.log"
        if ($LASTEXITCODE -ne 0) { throw "Solution tests failed: $LASTEXITCODE" }
    }
    Write-Host 'Reproduction passed. Canonical promotion is BLOCKED; inspect adoption-evidence.json and HUMANOID-X1.md. Successful reproduction does not mean deformation qualification passed.'
}
finally { Pop-Location }
