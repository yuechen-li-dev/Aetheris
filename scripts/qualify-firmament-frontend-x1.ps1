param([string]$OutputDirectory = 'artifacts/local/frontend-x1')
$ErrorActionPreference = 'Stop'
$cli = Join-Path $PSScriptRoot '../Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
    dotnet $cli frontend-shadow --corpus fixtures --output-dir (Join-Path $OutputDirectory 'corpus')
    if ($LASTEXITCODE -ne 0) { throw 'Shadow corpus collection did not complete.' }
    $witnesses = @(
        'Regression/FrontendX0/unknown-compose-statement.firmament',
        'Regression/FrontendX0/unknown-boundary-field.firmament',
        'Regression/FrontendX1/slot-route-perturbation.firmament',
        'Regression/CanonicalGeometry/profile-chamfer-reflex-junction-top.firmament',
        'Regression/ProfileComposition/valid/semantic-split-compose-chamfer.firmament'
    )
    foreach ($relative in $witnesses) {
        $name = [IO.Path]::GetFileNameWithoutExtension($relative)
        dotnet $cli frontend-shadow (Join-Path 'fixtures' $relative) --json --validate > (Join-Path $OutputDirectory "$name-shadow.json")
        # Rejection is evidence. Each file preserves both statuses and located diagnostics.
        dotnet $cli build (Join-Path 'fixtures' $relative) --output (Join-Path $OutputDirectory "$name.step") --json > (Join-Path $OutputDirectory "$name-build.json")
    }
    $benchmarks = @(
        'fixtures/Canonical/Basics/box.firmament',
        'fixtures/Regression/FrontendX0/unknown-compose-statement.firmament',
        'fixtures/Canonical/Templates/nested-template-pmi.firmament',
        'fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar-x0.firmament',
        'fixtures/Canonical/Scene/WarmModernHouse/house.firmament'
    )
    $largest = Get-ChildItem fixtures -Recurse -File -Filter '*.firmament' | Sort-Object Length -Descending | Select-Object -First 1
    $benchmarks += $largest.FullName
    $results = foreach ($source in $benchmarks) {
        $raw = dotnet $cli frontend-shadow $source --json --benchmark
        if ([string]::IsNullOrWhiteSpace($raw)) { throw "No shadow evidence for $source" }
        $entry = $raw | ConvertFrom-Json
        [pscustomobject]@{ source = [IO.Path]::GetRelativePath($repo, [IO.Path]::GetFullPath($source)); shadowStatus = $entry.shadow.shadowStatus; benchmark = $entry.benchmark }
    }
    $results | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $OutputDirectory 'benchmarks.json') -Encoding utf8
} finally { Pop-Location }
