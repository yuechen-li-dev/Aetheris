[CmdletBinding()]
param([switch]$SkipFetch)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $outDir = 'artifacts/local/humanoid-x3'
    $sourceDir = "$outDir/source"
    New-Item -ItemType Directory -Force $sourceDir | Out-Null
    $revision = '08c9767691daad1382dfc6980ee83e31514b4879'
    $relative = 'Runtime/libraries/Character/Antonia/Antonia-1.2.cr2'
    $expected = 'ff97bcbff1a0f8f86fbc39122196787fd6b46dec914b6d55574ed764f3142d62'
    $path = "$sourceDir/Antonia-1.2.cr2"
    if (-not (Test-Path -LiteralPath $path)) {
        if ($SkipFetch) { throw 'Missing pinned original CR2 audit input.' }
        Invoke-WebRequest "https://raw.githubusercontent.com/odf/Antonia.Polygon/$revision/$relative" -OutFile $path
    }
    if ((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $expected) { throw 'Original CR2 hash mismatch.' }
    $text = Get-Content -LiteralPath $path -Raw
    $channels = foreach ($axis in @('X','Z')) {
        $label = 'lThigh_joint' + $axis.ToLowerInvariant()
        $pattern = '(?ms)^\t\tjoint' + $axis + ' ' + $label + '\r?\n\t\t\t\{.*?^\t\t\t\}'
        $match = [regex]::Match($text, $pattern)
        if (-not $match.Success) { throw "Missing expected pinned channel: $label" }
        $declarations = @($match.Value -split '\r?\n' | Where-Object { $_ -match '^\s*(angles|center|doBulge|posBulgeLeft|posBulgeRight|negBulgeLeft|negBulgeRight|jointMult|calcWeights)\b' } | ForEach-Object { $_.Trim() })
        [ordered]@{channel=$label;declarations=$declarations;hasSphereMatsRaw=$match.Value.Contains('sphereMatsRaw')}
    }
    $candidate = Get-Content artifacts/local/humanoid-x1/antonia-adoption-candidate.json -Raw | ConvertFrom-Json
    $centerText = @($channels[0].declarations | Where-Object { $_.StartsWith('center ') })
    if ($centerText.Count -ne 1) { throw 'Expected exactly one center declaration in the inspected channel.' }
    $center = @($centerText[0].Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries) | Select-Object -Skip 1 | ForEach-Object { [double]::Parse($_,[System.Globalization.CultureInfo]::InvariantCulture) })
    $authored = @((-$center[0] * $candidate.scaleToMm), (($center[2] - $candidate.sourcePelvisZ) * $candidate.scaleToMm), (($center[1] - $candidate.sourceSoleY) * $candidate.scaleToMm))
    $joint = $candidate.preparedSkeleton.joints | Where-Object kind -eq 'LeftHip'
    $existing = @($joint.globalBind.m41, $joint.globalBind.m42, $joint.globalBind.m43)
    $squared = 0.0
    for ($i=0; $i -lt 3; $i++) { $squared += [Math]::Pow($authored[$i] - $existing[$i],2) }
    [ordered]@{
        schema='aetheris.humanoid.antonia-source-deformation-audit.v1'
        revision=$revision;sourcePath=$relative;sha256=$expected
        status='Pinned declaration inspection only; not a source deformation evaluator or runtime rig admission'
        originalNotice='fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt'
        channels=$channels
        hasJointControlledThighMorphs=$text.Contains('targetGeom JCM-lThighIn')
        authoredHipCenterCanonicalMm=$authored
        candidateHipCenterCanonicalMm=$existing
        centerDifferenceMm=[Math]::Sqrt($squared)
        sourceBehaviorSampled=$false
        runtimeImported=$false
        blocker='Original CR2 uses Poser joint falloff matrices, bulge controls and driven morphs. No qualified Poser evaluator/correspondence-preserving posed export is present; no weights or behavior are inferred from declarations alone.'
    } | ConvertTo-Json -Depth 8 | Set-Content "$outDir/source-deformation-audit.json"
}
finally { Pop-Location }
