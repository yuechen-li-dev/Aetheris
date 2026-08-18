[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $tracked = @(git ls-files)
    if ($LASTEXITCODE -ne 0) { throw "git ls-files failed." }

    $errors = [System.Collections.Generic.List[string]]::new()
    $retiredPrefixes = @(
        "references/firmament/",
        "testdata/firmament/fixtures/",
        "fixtures/Firmament/",
        "fixtures/FirmamentV2/",
        "demo-output/",
        "artifacts/verification/",
        "artifacts/local/",
        "tests/Aetheris.TestForgePack/",
        "tests/Aetheris.TestForgePack.Duplicate/",
        "tests/PublicPackageSmoke/"
    )

    foreach ($prefix in $retiredPrefixes) {
        if ($tracked.Where({ $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) {
            $errors.Add("Retired content location contains tracked files: $prefix")
        }
    }

    $allowedDocRoots = @("development", "legal", "public", "release", "roadmap")
    foreach ($path in $tracked.Where({ $_.StartsWith("docs/", [StringComparison]::OrdinalIgnoreCase) })) {
        $parts = $path.Split('/')
        if ($parts.Length -lt 3 -or $allowedDocRoots -notcontains $parts[1]) {
            $errors.Add("Documentation is outside an approved kind-based root: $path")
        }
    }

    foreach ($path in $tracked.Where({ $_.StartsWith("testdata/", [StringComparison]::OrdinalIgnoreCase) })) {
        if ([IO.Path]::GetExtension($path) -in @(".firmament", ".firmfixture", ".firmasm")) {
            $errors.Add("Firmament-family test source belongs under fixtures/: $path")
        }
    }

    $largeArtifactLimit = 20000
    $largeArtifactAllowlistPath = "scripts/tracked-large-artifact-allowlist.txt"
    $largeArtifactAllowlist = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if (Test-Path -LiteralPath $largeArtifactAllowlistPath -PathType Leaf) {
        foreach ($line in Get-Content -LiteralPath $largeArtifactAllowlistPath) {
            $entry = $line.Trim()
            if ($entry.Length -gt 0 -and -not $entry.StartsWith("#", [StringComparison]::Ordinal)) {
                [void]$largeArtifactAllowlist.Add($entry)
            }
        }
    }

    $diagnosticExtensions = @(".json", ".jsonl", ".csv", ".log")
    foreach ($path in $tracked.Where({
        $_.StartsWith("docs/development/", [StringComparison]::OrdinalIgnoreCase) -and
        $diagnosticExtensions -contains [IO.Path]::GetExtension($_)
    })) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $lineCount = 0
        $reader = [IO.File]::OpenText((Resolve-Path -LiteralPath $path))
        try {
            while ($null -ne $reader.ReadLine()) {
                $lineCount++
                if ($lineCount -gt $largeArtifactLimit) { break }
            }
        }
        finally {
            $reader.Dispose()
        }

        if ($lineCount -gt $largeArtifactLimit -and -not $largeArtifactAllowlist.Contains($path)) {
            $errors.Add("Tracked development diagnostic exceeds $largeArtifactLimit lines and is not allowlisted: $path")
        }
    }

    foreach ($path in $largeArtifactAllowlist) {
        if (-not $tracked.Contains($path)) {
            $errors.Add("Large-artifact allowlist entry is stale or untracked: $path")
        }
    }

    foreach ($required in @(
        "docs/public/README.md",
        "docs/development/README.md",
        "fixtures/README.md",
        "testdata/README.md",
        "samples/README.md",
        "demos/README.md",
        "test-support/README.md",
        "docs/development/GENERATED-ARTIFACT-POLICY.md",
        "scripts/tracked-large-artifact-allowlist.txt"
    )) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            $errors.Add("Required repository-map file is missing: $required")
        }
    }

    if ($errors.Count -gt 0) {
        $errors | ForEach-Object { Write-Error $_ }
        throw "Repository information-architecture guard failed with $($errors.Count) violation(s)."
    }

    Write-Host "Repository information-architecture guard passed ($($tracked.Count) tracked files inspected)."
}
finally {
    Pop-Location
}
