[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$canonicalRoot = Join-Path $RepositoryRoot 'fixtures/Canonical'
$artifactRoot = Join-Path $RepositoryRoot 'artifacts/local/canonical-qualification'
$manifest = Get-Content -Raw -LiteralPath (Join-Path $canonicalRoot 'qualification.json') | ConvertFrom-Json
$coverage = Get-Content -Raw -LiteralPath (Join-Path $canonicalRoot 'coverage.json') | ConvertFrom-Json

if (-not $NoBuild) {
    & dotnet build (Join-Path $RepositoryRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Aetheris.CLI Release build failed.' }
}

$cli = Join-Path $RepositoryRoot 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
if (-not (Test-Path -LiteralPath $cli)) { throw "CLI not found: $cli" }
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$forbidden = @(
    @{ Name = 'lowercase Firmament-owned declaration'; Pattern = '(?m)^\s*(model|units|solid|record|static|template|analysis|fixed|force|results|lattice)\b' },
    @{ Name = 'bare Template declaration'; Pattern = '(?m)^\s*Template\s+[A-Za-z_]' },
    @{ Name = 'legacy V1 header'; Pattern = '(?m)^\s*Firmament\s+1\b' },
    @{ Name = 'legacy arbitrary Boolean authoring'; Pattern = '\b(Union|Subtract|Intersect)\b' },
    @{ Name = 'legacy explicit assembly placement'; Pattern = '\bLegacyExplicit\b' }
)

$failures = [Collections.Generic.List[string]]::new()
$requiredFeatures = @('Box', 'Cylinder', 'Sphere', 'Cone', 'Torus', 'Profile', 'Boss', 'Pocket', 'Hole', 'BlindHole', 'Counterbore', 'Countersink', 'Slot', 'Pattern', 'Chamfer', 'Fillet', 'Material', 'PMI', 'Template', 'SheetMetal', 'FEA', 'inlineSTEP', 'Assembly')
foreach ($feature in $requiredFeatures) {
    $entry = $coverage.features.$feature
    if ([string]::IsNullOrWhiteSpace($entry)) { $failures.Add("coverage: missing $feature"); continue }
    if (-not (Test-Path -LiteralPath (Join-Path $canonicalRoot $entry) -PathType Leaf)) { $failures.Add("coverage: $feature points to missing $entry") }
}
$qualified = 0
foreach ($file in Get-ChildItem -LiteralPath $canonicalRoot -Recurse -Filter '*.firmament' | Sort-Object FullName) {
    $relative = [IO.Path]::GetRelativePath($canonicalRoot, $file.FullName).Replace('\', '/')
    if ($file.Name -match '^(?:a|m|edge-x)\d+[a-z]?-') {
        $failures.Add("${relative}: milestone-prefixed filename")
    }
    $source = [IO.File]::ReadAllText($file.FullName)
    foreach ($rule in $forbidden) {
        if ($source -cmatch $rule.Pattern) { $failures.Add("${relative}: $($rule.Name)") }
    }

    $action = $manifest.actions | Where-Object { $relative.StartsWith($_.path + '/', [StringComparison]::Ordinal) } | Select-Object -First 1
    if ($null -eq $action) { $failures.Add("${relative}: no qualification action"); continue }
    $stem = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    $outputDirectory = Join-Path $artifactRoot ($relative -replace '\.firmament$', '')
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    $arguments = switch ($action.operation) {
        'build-step' { @('build', $file.FullName, '--output', (Join-Path $outputDirectory "$stem.step"), '--json') }
        'sheet-metal-flatten' { @('sheetmetal', 'flatten', $file.FullName, '--step', (Join-Path $outputDirectory "$stem-flat.step"), '--svg', (Join-Path $outputDirectory "$stem-flat.svg"), '--json') }
        'fea-solve' { @('fea', $file.FullName, '--out-dir', $outputDirectory, '--json') }
        'assembly-inspect' { @('asm', 'inspect', $file.FullName, '--json') }
        'drawing-compile' { @('drawing', 'compile', $file.FullName, '--out-dir', $outputDirectory, '--json') }
        default { $failures.Add("${relative}: unknown operation $($action.operation)"); continue }
    }
    $output = & dotnet $cli @arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("${relative}: $($action.operation) exited $LASTEXITCODE`n$output")
        continue
    }
    if ($output -match '"severity"\s*:\s*"(?:warning|error|fatal)"') {
        $failures.Add("${relative}: qualification emitted a warning/error diagnostic`n$output")
        continue
    }
    $qualified++
    Write-Host "PASS $($action.operation) $relative"
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Canonical qualification failed: $($failures.Count) failure(s), $qualified passed."
}

Write-Host "Canonical qualification passed: $qualified fixture(s)."
