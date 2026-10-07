param(
    [Parameter(Mandatory = $true)][string]$CorpusDirectory,
    [string]$OutputDirectory = 'artifacts/local/step-legacy-x0/baseline',
    [string]$Manifest = 'testdata/step242/manifests/mcmaster-legacy-x0.json',
    [ValidateRange(1, 2147483)][int]$TimeoutSeconds = 180,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'test-support/Aetheris.StepLegacyCorpus'
if (-not $NoBuild) {
    & dotnet build $project -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Corpus runner build failed.' }
}
$runner = Join-Path $project 'bin/Release/net10.0/Aetheris.StepLegacyCorpus.dll'
$out = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repo $OutputDirectory }))
[IO.Directory]::CreateDirectory($out) | Out-Null
$manifestPath = if ([IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repo $Manifest }
$specimens = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).specimens
$results = @()
foreach ($specimen in $specimens) {
    $path = Join-Path $CorpusDirectory $specimen.filename
    $destination = Join-Path $out $specimen.id
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    if (-not (Test-Path -LiteralPath $path)) {
        $results += [pscustomobject]@{ SpecimenId = $specimen.id; Filename = $specimen.filename; ImportStatus = 'MissingLocalSpecimen' }
        continue
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $specimen.sha256) {
        $results += [pscustomobject]@{ SpecimenId = $specimen.id; Filename = $specimen.filename; ImportStatus = 'HashMismatch'; ActualSha256 = $hash }
        continue
    }
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.ArgumentList.Add($runner)
    $start.ArgumentList.Add($path)
    $start.ArgumentList.Add($destination)
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        $results += [pscustomobject]@{ SpecimenId = $specimen.id; Filename = $specimen.filename; ImportStatus = 'Failed'; Failure = "Timeout after $TimeoutSeconds seconds" }
    } elseif ($process.ExitCode -ne 0 -or -not (Test-Path (Join-Path $destination 'diagnostics.json'))) {
        $results += [pscustomobject]@{ SpecimenId = $specimen.id; Filename = $specimen.filename; ImportStatus = 'Failed'; Failure = "Worker exit $($process.ExitCode)" }
    } else {
        $result = Get-Content (Join-Path $destination 'diagnostics.json') -Raw | ConvertFrom-Json
        $result | Add-Member SpecimenId $specimen.id
        $results += $result
    }
    $process.Dispose()
    # Preserve each completed specimen even if the caller interrupts the sweep.
    $results | ConvertTo-Json -Depth 80 | Set-Content (Join-Path $out 'corpus-summary.json') -Encoding utf8
}
$results | ConvertTo-Json -Depth 80 | Set-Content (Join-Path $out 'corpus-summary.json') -Encoding utf8
$table = @('# STEP legacy corpus', '', '| Specimen | Schema | Import status | Bound / parsed / displayed faces |', '|---|---|---|---|')
foreach ($result in $results) { $table += "| $($result.SpecimenId) | $($result.Schema) | $($result.ImportStatus) | $($result.BoundFaces) / $($result.ParsedFaces) / $($result.DisplayedFaces) |" }
$table | Set-Content (Join-Path $out 'corpus-summary.md') -Encoding utf8
