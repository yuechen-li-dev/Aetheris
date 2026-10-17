[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$NoBuild,
    [string]$OutputDirectory = 'artifacts/local/canonical-qualification'
)

$ErrorActionPreference = 'Stop'
$canonicalRoot = Join-Path $RepositoryRoot 'fixtures/Canonical'
$artifactRoot = Join-Path $RepositoryRoot $OutputDirectory
$manifest = Get-Content -Raw -LiteralPath (Join-Path $canonicalRoot 'qualification.json') | ConvertFrom-Json
$coverage = Get-Content -Raw -LiteralPath (Join-Path $canonicalRoot 'coverage.json') | ConvertFrom-Json

if (-not $NoBuild) {
    & dotnet build (Join-Path $RepositoryRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release --nologo -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Aetheris.CLI Release build failed.' }
}

$cli = Join-Path $RepositoryRoot 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
if (-not (Test-Path -LiteralPath $cli)) { throw "CLI not found: $cli" }
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$forbidden = @(
    @{ Name = 'lowercase Firmament-owned declaration'; Pattern = '(?m)^\s*(model|units|solid|record|static|template|analysis|fixed|force|results|lattice)\b(?!\s*:)' },
    @{ Name = 'bare Template declaration'; Pattern = '(?m)^\s*Template\s+[A-Za-z_]' },
    @{ Name = 'legacy V1 header'; Pattern = '(?m)^\s*Firmament\s+1\b' },
    @{ Name = 'legacy arbitrary Boolean authoring'; Pattern = '\b(Union|Subtract|Intersect)\b' },
    @{ Name = 'legacy explicit assembly placement'; Pattern = '\bLegacyExplicit\b' }
)

$failures = [Collections.Generic.List[string]]::new()
$requiredFeatures = @('Box', 'Cylinder', 'Sphere', 'Cone', 'Torus', 'Profile', 'Sweep', 'WireForm', 'Paperclip', 'Boss', 'Pocket', 'Hole', 'BlindHole', 'Counterbore', 'Countersink', 'Slot', 'Pattern', 'Chamfer', 'Fillet', 'Material', 'PMI', 'Template', 'SheetMetal', 'FEA', 'inlineSTEP', 'Assembly', 'Piping', 'Gear', 'Interface<Gear>', 'ConstructionStateReplay', 'AddSectionChain', 'RemoveSectionChain', 'PostSectionChainHole')
foreach ($feature in $requiredFeatures) {
    $entry = $coverage.features.$feature
    if ([string]::IsNullOrWhiteSpace($entry)) { $failures.Add("coverage: missing $feature"); continue }
    if (-not (Test-Path -LiteralPath (Join-Path $canonicalRoot $entry) -PathType Leaf)) { $failures.Add("coverage: $feature points to missing $entry") }
}
$qualified = 0
$records = [Collections.Generic.List[object]]::new()
$consumers = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath $canonicalRoot -Recurse -File | Where-Object { $_.Extension -in @('.firmament', '.firmasm') } | Sort-Object FullName) {
    $relative = [IO.Path]::GetRelativePath($canonicalRoot, $file.FullName).Replace('\', '/')
    if ($file.Name -match '^(?:a|m|edge-x)\d+[a-z]?-') {
        $failures.Add("${relative}: milestone-prefixed filename")
    }
    $source = [IO.File]::ReadAllText($file.FullName)
    foreach ($rule in $forbidden) {
        if ($source -cmatch $rule.Pattern) { $failures.Add("${relative}: $($rule.Name)") }
    }

    $module = $manifest.modules | Where-Object { $_.path -eq $relative } | Select-Object -First 1
    if ($null -ne $module) {
        if (-not (Test-Path -LiteralPath (Join-Path $canonicalRoot $module.consumer) -PathType Leaf)) { $failures.Add("${relative}: missing module consumer $($module.consumer)"); continue }
        $consumers.Add($module.consumer) | Out-Null
        $records.Add([pscustomobject]@{ path = $relative; operation = 'module'; consumer = $module.consumer; level = 'QualifiedByConsumer' })
        Write-Host "MODULE $relative -> $($module.consumer)"
        continue
    }
    $action = $manifest.actions | Where-Object { $_.path -eq $relative } | Select-Object -First 1
    if ($null -eq $action) { $action = $manifest.actions | Where-Object { $relative.StartsWith($_.path + '/', [StringComparison]::Ordinal) } | Select-Object -First 1 }
    if ($null -eq $action) { $failures.Add("${relative}: no qualification action"); continue }
    $stem = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    $outputDirectory = Join-Path $artifactRoot ($relative -replace '\.firmament$', '')
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    $arguments = switch ($action.operation) {
        'build-step' { @('build', $file.FullName, '--output', (Join-Path $outputDirectory "$stem.step"), '--json') }
        'section-chain-build' { @('section-chain', 'build', $file.FullName, '--out', (Join-Path $outputDirectory "$stem.step"), '--json') }
        'sheet-metal-flatten' { @('sheetmetal', 'flatten', $file.FullName, '--step', (Join-Path $outputDirectory "$stem-flat.step"), '--svg', (Join-Path $outputDirectory "$stem-flat.svg"), '--json') }
        'fea-solve' { @('fea', $file.FullName, '--out-dir', $outputDirectory, '--json') }
        'assembly-inspect' { @('asm', 'inspect', $file.FullName, '--json') }
        'scene-inspect' { @('scene', 'inspect', $file.FullName, '--json') }
        'inspect' { @('inspect', $file.FullName, '--json') }
        'drawing-compile' { @('drawing', 'compile', $file.FullName, '--out-dir', $outputDirectory, '--json') }
        'validate' { @('validate', $file.FullName, '--json') }
        default { $failures.Add("${relative}: unknown operation $($action.operation)"); continue }
    }
    $output = & dotnet $cli @arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $boundary = $manifest.expectedRejections | Where-Object { $_.path -eq $relative } | Select-Object -First 1
    if ($null -ne $boundary) {
        $result = $output | ConvertFrom-Json
        $codes = @($result.diagnostics.code)
        if ($exitCode -ne $boundary.exitCode -or @($boundary.codes | Where-Object { $_ -notin $codes }).Count -gt 0 -or @($codes | Where-Object { $_ -notin $boundary.codes }).Count -gt 0) {
            $failures.Add("${relative}: expected rejection contract changed`n$output"); continue
        }
        [IO.File]::WriteAllText((Join-Path $outputDirectory 'result.json'), $output)
        $records.Add([pscustomobject]@{ path = $relative; operation = $action.operation; level = $boundary.level; codes = $codes })
        Write-Host "BOUNDARY $relative ($($boundary.level))"
        continue
    }
    if ($exitCode -ne 0) {
        $failures.Add("${relative}: $($action.operation) exited $exitCode`n$output")
        continue
    }
    try {
        $result = $output | ConvertFrom-Json
    }
    catch {
        $failures.Add("${relative}: qualification did not emit valid JSON`n$output")
        continue
    }
    [IO.File]::WriteAllText((Join-Path $outputDirectory 'result.json'), $output)
    $expectedEntry = $manifest.expectedDiagnostics | Where-Object { $_.path -eq $relative } | Select-Object -First 1
    $expectedCodes = @($expectedEntry.codes)
    $emitted = @($result.diagnostics | Where-Object { $_.severity -in @('warning', 'error', 'fatal') })
    $unexpected = @($emitted | Where-Object { $_.code -notin $expectedCodes })
    $missing = @($expectedCodes | Where-Object { $_ -notin @($emitted.code) })
    if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
        $unexpectedCodes = @($unexpected.code) -join ', '
        $missingCodes = $missing -join ', '
        $failures.Add("${relative}: diagnostic contract mismatch (unexpected: [$unexpectedCodes]; missing: [$missingCodes])`n$output")
        continue
    }
    $qualified++
    $records.Add([pscustomobject]@{ path = $relative; operation = $action.operation; level = 'OwnerQualified' })
    Write-Host "PASS $($action.operation) $relative"
}

foreach ($consumer in $consumers) {
    if (-not ($records | Where-Object { $_.path -eq $consumer -and $_.level -eq 'OwnerQualified' })) { $failures.Add("module consumer did not qualify: $consumer") }
}
$records | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $artifactRoot 'qualification.json') -Encoding utf8

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL $_" }
    throw "Canonical qualification failed: $($failures.Count) failure(s), $qualified passed."
}

Write-Host "Canonical qualification passed: $qualified owner operations, $(@($records | Where-Object operation -eq 'module').Count) consumed modules, $(@($records | Where-Object level -eq 'ExternalBodyRequired').Count) external-body boundaries."
