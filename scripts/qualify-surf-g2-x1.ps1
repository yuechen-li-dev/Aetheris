param(
    [string]$Cli = (Join-Path $PSScriptRoot '../Aetheris.CLI/bin/Release/net10.0/aetheris.exe'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/local/surf-g2-x1/qualification')
)
$ErrorActionPreference = 'Stop'
$surfRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Cli = [IO.Path]::GetFullPath($Cli)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$surfSource = Join-Path $surfRoot 'fixtures/Canonical/SectionChain/normalized-rounded-sections.firmament'
function Invoke-SurfCli([string[]]$Arguments, [string]$Log) {
    $surfText = & $Cli @Arguments
    $surfExit = $LASTEXITCODE
    $surfText | Set-Content -LiteralPath (Join-Path $OutputDirectory $Log)
    if ($surfExit -ne 0) { throw "CLI failed ($surfExit): $($Arguments -join ' ')" }
    return ($surfText -join "`n" | ConvertFrom-Json)
}
$surfBuild = Invoke-SurfCli @('section-chain','build',$surfSource,'--out',(Join-Path $OutputDirectory 'rounded.step'),'--json') 'build.json'
$surfRepeat = Invoke-SurfCli @('section-chain','build',$surfSource,'--out',(Join-Path $OutputDirectory 'repeat.step'),'--json') 'repeat.json'
$surfInspect = Invoke-SurfCli @('section-chain','inspect',$surfSource,'--json') 'inspect.json'
$surfValidate = Invoke-SurfCli @('section-chain','validate',$surfSource,'--json') 'validate.json'
$surfAnalysis = Invoke-SurfCli @('analyze',(Join-Path $OutputDirectory 'rounded.step'),'--json') 'analysis.json'
if ($surfBuild.sha256 -ne $surfRepeat.sha256) { throw 'STEP is not deterministic.' }
if ($surfAnalysis.summary.structuralAssessment -ne 'enclosed-manifold') { throw 'Reimport is not enclosed-manifold.' }
if (!$surfBuild.stepReimport.success) { throw 'STEP reimport failed.' }
if (($surfBuild.profileNormalization.curves | Where-Object { $_.certifiedDeviationBound -gt $_.requestedTolerance }).Count) { throw 'Normalization bound failed.' }
if (($surfBuild.geometricJoins | Where-Object { $_.boundaryKind -eq 'NeighboringProfileSpans' -and $_.status -ne 'G1' }).Count) { throw 'Expected line/arc curvature jump is not diagnosed.' }
foreach ($surfInvalid in @('normalization-tolerance-unmet','g2-not-admitted')) {
    $surfInput = Join-Path $surfRoot "fixtures/Invalid/SectionChain/$surfInvalid.firmament"
    $surfInvalidOutput = Join-Path $OutputDirectory "$surfInvalid.step"
    if (Test-Path -LiteralPath $surfInvalidOutput) { throw "Invalid-output path already exists: $surfInvalidOutput" }
    & $Cli section-chain build $surfInput --out $surfInvalidOutput --json | Set-Content -LiteralPath (Join-Path $OutputDirectory "$surfInvalid.json")
    if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath $surfInvalidOutput)) { throw 'Invalid construction did not fail transactionally.' }
}
[pscustomobject]@{
    verdict = 'Meaningful progression; no new G2 transition'
    stepSha256 = $surfBuild.sha256
    manifold = $surfAnalysis.summary.structuralAssessment
    normalizedArcs = $surfBuild.profileNormalization.curves.Count
    maxBoundMm = ($surfBuild.profileNormalization.curves.certifiedDeviationBound | Measure-Object -Maximum).Maximum
    maxSampleErrorMm = ($surfBuild.profileNormalization.curves.maximumSampledDeviation | Measure-Object -Maximum).Maximum
    lineArcCurvatureJumpPerMm = ($surfBuild.geometricJoins.maximumShapeOperatorResidual | Measure-Object -Maximum).Maximum
    faces = $surfBuild.topology.faces
    stepBytes = (Get-Item -LiteralPath (Join-Path $OutputDirectory 'rounded.step')).Length
} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $OutputDirectory 'summary.json')
