param(
    [string]$OutputDirectory = "artifacts/local/domain-frontend-migration/qualification",
    [string]$EvidenceFile = "docs/release/FIRMAMENT-DOMAIN-FRONTEND-MIGRATION.evidence.json"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $evidence = Get-Content -LiteralPath $EvidenceFile -Raw | ConvertFrom-Json
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $records = foreach ($witness in $evidence.geometry) {
        $target = Join-Path $OutputDirectory $witness.artifact
        $arguments = @("Aetheris.CLI/bin/Release/net10.0/aetheris.dll")
        if ($witness.command -eq "section-chain") { $arguments += "section-chain" }
        $arguments += @("build", $witness.source, "--out", $target, "--json")
        & dotnet @arguments 2>&1 | Out-File -LiteralPath "$target.log" -Encoding utf8
        $exitCode = $LASTEXITCODE
        $hash = if ($exitCode -eq 0 -and (Test-Path -LiteralPath $target)) {
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
        } else { $null }
        [pscustomobject]@{
            source = $witness.source
            expectedExitCode = $witness.expectedExitCode
            actualExitCode = $exitCode
            beforeSha256 = $witness.beforeSha256
            afterSha256 = $hash
            matched = $exitCode -eq $witness.expectedExitCode -and $hash -eq $witness.beforeSha256
        }
    }
    ConvertTo-Json -InputObject @($records) -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory "comparison.json") -Encoding utf8
    $failures = @($records | Where-Object { -not $_.matched })
    Write-Output "Domain frontend witnesses: $(@($records).Count); matched: $(@($records).Count - $failures.Count); failed: $($failures.Count)."
    if ($failures.Count -gt 0) {
        $failures | Format-Table source, expectedExitCode, actualExitCode
        throw "Frontend admission or STEP geometry changed; inspect comparison.json and the owning build log."
    }
} finally { Pop-Location }
