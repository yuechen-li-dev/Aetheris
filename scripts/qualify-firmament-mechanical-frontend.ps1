param(
    [string]$OutputDirectory = 'artifacts/local/mechanical-frontend-migration',
    [string]$BaselineDirectory = 'artifacts/local/mechanical-frontend-migration/before'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$cli = Join-Path $repo 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
Push-Location $repo
try {
    $afterDirectory = Join-Path $OutputDirectory 'after'
    New-Item -ItemType Directory -Force $afterDirectory | Out-Null
    $baselineManifest = Join-Path $BaselineDirectory 'summary.json'
    if (Test-Path -LiteralPath $baselineManifest) {
        $baseline = Get-Content -LiteralPath $baselineManifest -Raw | ConvertFrom-Json
    } else {
        # The versioned release evidence retains the hashes when ignored pre-change
        # STEP artifacts are unavailable (for example, in a fresh checkout).
        $evidence = Get-Content 'docs/release/FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.evidence.json' -Raw | ConvertFrom-Json
        $baseline = foreach ($case in $evidence.geometryComparisons) {
            [pscustomobject]@{ source = $case.source; step = $case.artifactName; exit = $case.beforeExit; sha256 = $case.beforeSha256 }
        }
    }
    $comparison = foreach ($case in $baseline) {
        $name = [IO.Path]::GetFileName($case.step)
        $step = Join-Path $afterDirectory $name
        & dotnet $cli build $case.source --output $step --json | Set-Content (Join-Path $afterDirectory "$name.json") -Encoding utf8
        $exitCode = $LASTEXITCODE
        $hash = if ($exitCode -eq 0) { (Get-FileHash -LiteralPath $step -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
        [pscustomobject]@{ source = $case.source; beforeExit = $case.exit; afterExit = $exitCode; beforeSha256 = $case.sha256; afterSha256 = $hash; identical = $case.sha256 -eq $hash -and $exitCode -eq $case.exit }
    }
    $comparison | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'geometry-comparison.json') -Encoding utf8
    $witnesses = @(
        @{ path = 'fixtures/Regression/FrontendX0/unknown-compose-statement.firmament'; accepted = $false },
        @{ path = 'fixtures/Regression/FrontendX0/unknown-boundary-field.firmament'; accepted = $false },
        @{ path = 'fixtures/Regression/FrontendX1/slot-route-perturbation.firmament'; accepted = $true },
        @{ path = 'fixtures/Regression/CanonicalGeometry/profile-chamfer-reflex-junction-top.firmament'; accepted = $true }
    )
    $admission = foreach ($case in $witnesses) {
        $name = [IO.Path]::GetFileNameWithoutExtension($case.path)
        & dotnet $cli validate $case.path --explain --json | Set-Content (Join-Path $afterDirectory "$name-validate.json") -Encoding utf8
        $validateExit = $LASTEXITCODE
        & dotnet $cli build $case.path --output (Join-Path $afterDirectory "$name.step") --json | Set-Content (Join-Path $afterDirectory "$name-build.json") -Encoding utf8
        $buildExit = $LASTEXITCODE
        [pscustomobject]@{ source = $case.path; expectedAccepted = $case.accepted; validateExit = $validateExit; buildExit = $buildExit; consistent = $validateExit -eq $buildExit -and (($buildExit -eq 0) -eq $case.accepted) }
    }
    $admission | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'admission-comparison.json') -Encoding utf8
    $changed = @($comparison | Where-Object { -not $_.identical })
    $contradictions = @($admission | Where-Object { -not $_.consistent })
    Write-Output "Geometry: $($comparison.Count - $changed.Count)/$($comparison.Count) identical STEP hashes; admission: $($admission.Count - $contradictions.Count)/$($admission.Count) consistent."
    if ($changed.Count -gt 0 -or $contradictions.Count -gt 0) { throw 'Mechanical frontend qualification failed; inspect the JSON evidence.' }
} finally { Pop-Location }
