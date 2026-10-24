param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$hostExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh Forge qualification directory.' }
New-Item -ItemType Directory -Path $output | Out-Null

function Invoke-Forge([string[]]$HostArguments, [string]$LogPath) {
    $PSNativeCommandUseErrorActionPreference = $false
    & $hostExecutable @HostArguments > $LogPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Forge failed ($LASTEXITCODE): $($HostArguments -join ' '). See $LogPath" }
    return Get-Content -LiteralPath $LogPath -Raw | ConvertFrom-Json
}

$catalog = Invoke-Forge @('list') (Join-Path $output 'catalog.json')
if ($catalog.protocolVersion -ne 1 -or $catalog.templates.Count -eq 0) { throw 'Forge did not expose the public template catalog.' }
$rows = @()
foreach ($template in $catalog.templates) {
    $directory = Join-Path $output "template-$($rows.Count)"
    New-Item -ItemType Directory -Path $directory | Out-Null
    $description = Invoke-Forge @('describe', $template.id) (Join-Path $directory 'description.json')
    if (@($description.parameters | Where-Object required).Count -ne 0) {
        throw "Add an explicit qualification request for required parameters in $($template.id)."
    }
    $request = Join-Path $directory 'request.json'
    @{ protocolVersion = 1; arguments = @{}; artifacts = @($description.artifacts) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $request
    $result = Invoke-Forge @('invoke', $template.id, '--request', $request, '--out', $directory) (Join-Path $directory 'result.json')
    if (-not $result.success -or $result.artifacts.Count -ne $description.artifacts.Count) { throw "Forge did not build all advertised artifacts for $($template.id). See $directory/result.json" }
    foreach ($artifact in $result.artifacts) {
        $path = Join-Path $directory $artifact.path
        $file = Get-Item -LiteralPath $path
        if ($file.Length -eq 0 -or $file.Length -ne $artifact.size -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256) {
            throw "Forge artifact receipt mismatch for $($template.id): $($artifact.path)"
        }
        if ($artifact.kind -eq 'StepAp242' -and [IO.File]::ReadAllText($path) -notmatch '^ISO-10303-21;') {
            throw "Forge did not emit STEP for $($template.id)."
        }
    }
    $rows += [pscustomobject]@{ template = $template.id; artifacts = $result.artifacts; identity = $result.identity }
    Write-Output "Qualified $($template.id)"
}
$rows | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'summary.json')

# A complete but physically invalid record must reach semantic rejection, not
# crash on string binding or emit plausible fallback geometry.
$invalidRequest = Join-Path $output 'invalid-standoff.json'
[IO.File]::WriteAllText($invalidRequest, '{"protocolVersion":1,"arguments":{"P":{"OuterDiameter":"1mm","BoreDiameter":"4.2mm","Length":"25mm","Material":"Standard.Materials.StainlessSteel.304_Annealed"}},"artifacts":["StepAp242"]}')
$invalidLog = Join-Path $output 'invalid-standoff-result.json'
& $hostExecutable invoke Standard.Products.Mechanical.Standoff --request $invalidRequest --out (Join-Path $output 'invalid-artifacts') > $invalidLog 2>&1
if ($LASTEXITCODE -ne 4) { throw "Invalid standoff did not return semantic rejection. See $invalidLog" }
$invalid = Get-Content -LiteralPath $invalidLog -Raw | ConvertFrom-Json
if ($invalid.success -or $invalid.artifacts.Count -ne 0 -or -not ($invalid.diagnostics.code -contains 'firmament-concept-requirement-failed')) {
    throw "Invalid standoff did not preserve its semantic diagnostic and artifact refusal. See $invalidLog"
}
Write-Output "Qualified $($rows.Count) public Forge templates with their declared defaults."
