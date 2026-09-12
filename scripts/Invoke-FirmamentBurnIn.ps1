[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/local/lang-burn-x1-replay',
    [string[]]$Case = @(),
    [switch]$NoBuild
)

# Evidence collector, not a release gate: negative cases intentionally fail.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$corpus = Join-Path $repo 'fixtures/Regression/LanguageBurnIn'
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$cli = Join-Path $repo 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
if (-not $NoBuild) {
    & dotnet build (Join-Path $repo 'Aetheris.CLI') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
}
New-Item -ItemType Directory -Force $output | Out-Null
$manifest = Get-Content (Join-Path $corpus 'manifest.json') -Raw | ConvertFrom-Json
$results = [Collections.Generic.List[object]]::new()
function Invoke-Evidence([string]$Directory, [string]$Name, [string[]]$Arguments) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $text = & dotnet $cli @Arguments 2>&1 | Out-String
    $code = $LASTEXITCODE
    $watch.Stop()
    [IO.File]::WriteAllText((Join-Path $Directory "$Name.json"), $text)
    return @{ exit = $code; seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 3) }
}
# Import witnesses depend on this generated stock; copy all sources before running.
foreach ($entry in $manifest) {
    $directory = Join-Path $output $entry.name
    New-Item -ItemType Directory -Force $directory | Out-Null
    Copy-Item -LiteralPath (Join-Path $corpus "$($entry.name)/$($entry.name).firmament") -Destination $directory
}
Copy-Item -LiteralPath (Join-Path $repo 'testdata/firmament/inline-step/canonical-through-hole.step') -Destination (Join-Path $output 'import-fea/input.step')
$ordered = @($manifest | Sort-Object @{ Expression = { if ($_.name -eq 'stock-block') { 0 } else { 1 } } }, name)
foreach ($entry in $ordered) {
    if ($Case.Count -gt 0 -and $entry.name -notin $Case -and $entry.name -ne 'stock-block') { continue }
    $directory = Join-Path $output $entry.name
    $source = Join-Path $directory "$($entry.name).firmament"
    $step = Join-Path $directory "$($entry.name).step"
    $result = [ordered]@{ name = $entry.name }
    foreach ($operation in @('validate', 'inspect')) {
        $result[$operation] = Invoke-Evidence $directory $operation @($operation, $source, '--json')
    }
    $arguments = switch ($entry.operation) {
        'section-chain' { @('section-chain', 'build', $source, '--out', $step, '--json') }
        'fea' { @('fea', $source, '--out-dir', (Join-Path $directory 'fea'), '--json') }
        default { @('build', $source, '--out', $step, '--json') }
    }
    $result['build'] = Invoke-Evidence $directory 'build' $arguments
    if ($result.build.exit -eq 0 -and (Test-Path -LiteralPath $step)) {
        foreach ($operation in @('analyze', 'verify')) {
            $result[$operation] = Invoke-Evidence $directory $operation @($operation, $step, '--json')
        }
        $result['wireframe'] = Invoke-Evidence $directory 'wireframe' @('wireframe', $step, '--out', (Join-Path $directory 'iso.svg'), '--density', '3', '--json')
        $result['sha256'] = (Get-FileHash -LiteralPath $step -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($entry.name -in @('frame', 'pipe-obstacle', 'pipe-reroute')) {
            $result['assemblyImport'] = Invoke-Evidence $directory 'assembly-import' @('asm', 'import-step', $step, '--out', (Join-Path $directory 'imported'), '--json')
        }
    }
    if ($entry.name.StartsWith('sheet-')) {
        $result['flatten'] = Invoke-Evidence $directory 'flatten' @('sheetmetal', 'flatten', $source, '--step', (Join-Path $directory 'flat.step'), '--svg', (Join-Path $directory 'flat.svg'), '--json')
    }
    $results.Add($result)
    Write-Host "$($entry.name): build exit $($result.build.exit)"
}
$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'results.json')
Write-Host "Evidence: $output. Read report statuses; an exit-zero command is not geometry acceptance."
