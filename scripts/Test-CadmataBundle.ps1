param(
    [Parameter(Mandatory)][string]$BundleDirectory,
    [Parameter(Mandatory)][string]$StepPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$bundle = (Resolve-Path -LiteralPath $BundleDirectory).Path
if (Test-Path -LiteralPath (Join-Path $bundle 'cadmata')) { throw 'Obsolete duplicated server publish directory is present.' }
if (-not (Test-Path -LiteralPath (Join-Path $bundle 'Cadmata.Desktop.dll'))) { throw 'The actual desktop host is missing.' }
$metadata = Get-Content -LiteralPath (Join-Path $bundle 'BUILD-METADATA.json') -Raw | ConvertFrom-Json
if ($env:GITHUB_ACTIONS -eq 'true' -and $metadata.modifiedSource) { throw 'Hosted release must identify clean product source.' }
if (-not (Test-Path -LiteralPath (Join-Path $bundle 'CORRESPONDING-SOURCE.md'))) { throw 'The desktop corresponding-source notice is missing.' }
$licenseAudit = Get-Content -LiteralPath (Join-Path $bundle 'licenses/AUDIT.json') -Raw | ConvertFrom-Json
if (-not $licenseAudit.redistributionNoticesComplete) { throw 'Desktop redistribution notices are incomplete.' }
$step = (Resolve-Path -LiteralPath $StepPath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh smoke output directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$stdout = Join-Path $output 'cadmata.stdout.log'
$stderr = Join-Path $output 'cadmata.stderr.log'
$hostPath = Join-Path $bundle 'Cadmata.Server.exe'

# This test owns the child lifetime. It binds a temporary loopback port and
# verifies the extracted production assets and STEP API without a dev server.
$hostProcess = Start-Process -FilePath $hostPath -ArgumentList @('--no-browser', '--urls', 'http://127.0.0.1:0', "`"$step`"") `
    -WorkingDirectory $output -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $address = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($hostProcess.HasExited) { throw "Cadmata exited with $($hostProcess.ExitCode). See $stderr" }
        $log = Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue
        if ($log -match 'Cadmata ready: (http://127\.0\.0\.1:\d+)') {
            $address = $Matches[1]
            break
        }
        Start-Sleep -Milliseconds 200
    }
    if (-not $address) { throw "Cadmata did not become ready. See $stdout and $stderr" }

    $page = Invoke-WebRequest "$address/" -TimeoutSec 10
    if ($page.Content -notmatch '<div id="root">') { throw 'Packaged Cadmata index is missing the application root.' }
    $assets = [regex]::Matches($page.Content, '(?:src|href)="(/assets/[^"<>]+\.(?:js|css))"')
    if ($assets.Count -lt 2) { throw 'Packaged Cadmata index has no production JavaScript/CSS references.' }
    foreach ($asset in $assets) {
        $response = Invoke-WebRequest "$address$($asset.Groups[1].Value)" -TimeoutSec 10
        if ($response.Headers['Content-Type'] -match 'text/html' -or $response.RawContentLength -eq 0) {
            throw "Missing production asset: $($asset.Groups[1].Value)"
        }
    }

    $startup = Invoke-RestMethod "$address/api/v1/startup/step" -Method Post -TimeoutSec 10
    if (-not $startup.success -or $startup.data.stepText -ne [IO.File]::ReadAllText($step)) {
        throw 'Cadmata did not preserve the requested startup STEP source.'
    }
    $document = Invoke-RestMethod "$address/api/v1/documents" -Method Post -ContentType 'application/json' -Body '{"name":"Release smoke"}' -TimeoutSec 10
    if (-not $document.success) { throw 'Packaged Cadmata could not create a document.' }
    $request = @{ stepText = $startup.data.stepText; name = 'Release smoke' } | ConvertTo-Json -Compress
    $imported = Invoke-RestMethod "$address/api/v1/documents/$($document.data.documentId)/import/step" -Method Post -ContentType 'application/json' -Body $request -TimeoutSec 60
    if (-not $imported.success -or $imported.data.importStatus -eq 'Failed') {
        throw 'Packaged Cadmata rejected the qualified STEP witness.'
    }
    if ($imported.data.occurrenceId -eq [Guid]::Empty.ToString() -and $imported.data.assemblyPresentation.occurrences.Count -eq 0) {
        throw 'STEP import returned no body or assembly occurrences.'
    }
    $imported | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $output 'import-result.json')
    Write-Output "Packaged Cadmata served $($assets.Count) production assets and imported the STEP witness."
}
finally {
    if (-not $hostProcess.HasExited) {
        $hostProcess.Kill($true)
        $hostProcess.WaitForExit()
    }
    $hostProcess.Dispose()
}
