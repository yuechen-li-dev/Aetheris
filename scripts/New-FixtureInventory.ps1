[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath = "artifacts/local/a5e-fixture-inventory.csv"
)

$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path $RepositoryRoot 'fixtures'
$resolvedOutput = Join-Path $RepositoryRoot $OutputPath

function Get-Classification {
    param(
        [string]$RelativePath,
        [string]$Extension,
        [string]$Text
    )

    $normalized = $RelativePath.Replace('\', '/')
    $futureMetadata = $Text -match '(?im)^\s*(implementation|status)\s*:\s*(not-implemented|future|speculative)\s*$' -or
        $Text -match '(?im)^\s*//\s*(implementation|status)\s*:\s*(not-implemented|future|speculative)\s*$'
    $invalidMetadata = $Text -match '(?im)^\s*(expected|status)\s*:\s*(failure|invalid|reject|rejected)\b' -or
        $Text -match '(?im)^\s*//\s*(expected|status)\s*:\s*(failure|invalid|reject|rejected)\b'
    $legacySyntax = $Text -cmatch '(?m)^\s*(model|units|solid|template|analysis)\b' -or
        $Text -cmatch '(?m)^\s*Firmament\s+1\b'
    $milestoneName = [IO.Path]::GetFileName($normalized) -match '^(a|m|e|x|edge-x)\d+[a-z]?-'

    if ($futureMetadata -or $normalized.StartsWith('fixtures/Speculative/')) {
        return @('Speculative', 'future/not-implemented metadata or speculative quarantine')
    }
    if ($normalized.StartsWith('fixtures/Compatibility/') -or $Extension -eq '.firmasm') {
        return @('Compatibility', 'legacy root, compatibility format, or .firmasm input')
    }
    if ($normalized -match '/invalid/' -or $invalidMetadata) {
        return @('InvalidDiagnostic', 'invalid path or expected-rejection metadata')
    }
    if ($normalized.StartsWith('fixtures/Canonical/')) {
        if ($legacySyntax -or $milestoneName -or $normalized -match '(compat|low-level|chimera|reflex|collision)') {
            return @('CurrentButAwkward', 'current source with legacy spelling, milestone naming, or regression-specific intent')
        }
        return @('CurrentCanonical', 'current executable source in canonical teaching surface')
    }
    if ($normalized -match '/Canonical/' -and $Extension -eq '.firmament') {
        return @('CurrentButAwkward', 'current source outside the single canonical teaching root')
    }
    if ($legacySyntax) {
        return @('Compatibility', 'content contains accepted historical Firmament spelling')
    }
    if ($Extension -eq '.step' -or $normalized -match '(Regression|M\d|evidence|reconstruct|decompile)') {
        return @('HistoricalRegression', 'test dependency or milestone/regression evidence')
    }
    if ($Extension -eq '.firmfixture') {
        return @('HistoricalRegression', 'self-describing implementation/corpus witness')
    }
    if ($Extension -eq '.firmament') {
        return @('UnknownNeedsReview', 'executable current-format source outside an explicit teaching category')
    }
    return @('UnknownNeedsReview', 'supporting file requires owner review')
}

function Get-Owner {
    param([string]$RelativePath)

    $path = $RelativePath.Replace('\', '/')
    switch -Regex ($path) {
        '/SheetMetal/' { return 'Aetheris.SheetMetal.Tests' }
        '/FEA/|/PublicDogfood/ai-fea' { return 'Aetheris.FEA.Tests' }
        '/Assembly|\.firmasm$' { return 'Aetheris.Kernel.Firmament.Tests (Assembly)' }
        '/Drawing' { return 'Aetheris.CLI.Tests (Drawing)' }
        '/PMI/|pmi-|hole.*pmi|counterbore.*pmi' { return 'Aetheris.CLI.Tests / Aetheris.Kernel.Firmament.Tests (PMI/AP242)' }
        default { return 'Aetheris.Kernel.Firmament.Tests' }
    }
}

$referenceFiles = Get-ChildItem $RepositoryRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '[\\/](\.git|bin|obj|artifacts|\.vs)[\\/]' -and
        $_.Extension -in '.cs', '.ps1', '.sh', '.md', '.json', '.yml', '.yaml', '.xml'
    }
$referenceCounts = @{}
foreach ($referenceFile in $referenceFiles) {
    try {
        $contents = [IO.File]::ReadAllText($referenceFile.FullName).Replace('\', '/')
        foreach ($match in [regex]::Matches($contents, 'fixtures/[A-Za-z0-9_./+\-]+')) {
            $candidate = $match.Value.TrimEnd(' ', '.', ',', ':', ';')
            if (-not $referenceCounts.ContainsKey($candidate)) {
                $referenceCounts[$candidate] = 0
            }
            $referenceCounts[$candidate]++
        }
    } catch {
        # Binary-looking or concurrently generated support files are not fixture owners.
    }
}

$rows = foreach ($file in Get-ChildItem $fixtureRoot -Recurse -File | Sort-Object FullName) {
    $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName).Replace('\', '/')
    $text = if ($file.Extension -in '.firmament', '.firmfixture', '.firmasm', '.md', '.json') {
        try { [IO.File]::ReadAllText($file.FullName) } catch { '' }
    } else { '' }
    $classification = Get-Classification -RelativePath $relative -Extension $file.Extension -Text $text
    $referenceCount = if ($referenceCounts.ContainsKey($relative)) { $referenceCounts[$relative] } else { 0 }

    [pscustomobject]@{
        Path = $relative
        Extension = $file.Extension
        Classification = $classification[0]
        Evidence = $classification[1]
        ReferenceCount = $referenceCount
        PrimaryOwner = Get-Owner $relative
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$rows | Export-Csv -LiteralPath $resolvedOutput -NoTypeInformation -Encoding utf8
$rows | Group-Object Classification | Sort-Object Name | ForEach-Object {
    '{0,-22} {1,4}' -f $_.Name, $_.Count
}
"Inventory: $resolvedOutput"
