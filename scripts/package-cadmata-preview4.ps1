[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/local/cadmata-distribution/release',
    [string]$WebViewCab,
    [string]$SourceRevision
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
if (-not $output.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package output must be inside the working repository.'
}
$stage = Join-Path $output 'package'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
if (-not $SourceRevision) { $SourceRevision = (git -C $repoRoot rev-parse HEAD).Trim() }
$dirty = if (Test-Path (Join-Path $repoRoot '.git')) { [bool](git -C $repoRoot status --porcelain) } else { $true }
$version = '2.0.0-preview.4'
$identity = "$version+$SourceRevision" + $(if ($dirty) { '.modified' } else { '' })

# Fixed distribution: no runtime detection, installed Edge dependency or bootstrap.
$runtimeVersion = '153.0.4234.48'
$runtimeUrl = 'https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/08cd33ee-d109-49b8-9301-9f0bea43c575/Microsoft.WebView2.FixedVersionRuntime.153.0.4234.48.x64.cab'
$runtimeHash = '11E8240CB0BC56DCD3E4498907203C251346F65107FE35A3A13E152C7D51C79E'
if (-not $WebViewCab) {
    $WebViewCab = Join-Path $output 'webview2-x64.cab'
    if (-not (Test-Path -LiteralPath $WebViewCab)) { Invoke-WebRequest $runtimeUrl -OutFile $WebViewCab }
}
$WebViewCab = [IO.Path]::GetFullPath($WebViewCab)
if ((Get-FileHash $WebViewCab -Algorithm SHA256).Hash -ne $runtimeHash) { throw 'Fixed WebView2 CAB hash mismatch.' }
$expanded = Join-Path $output 'runtime-expanded'
if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
New-Item -ItemType Directory -Force $expanded | Out-Null
expand.exe $WebViewCab '-F:*' $expanded > (Join-Path $output 'runtime-expansion.log')

# Build every asset from source, including Telos's standalone WGSL files.
Push-Location (Join-Path $repoRoot 'Aetheris.Web.Runtime/telos')
try { npm ci; npm run build } finally { Pop-Location }
Push-Location (Join-Path $repoRoot 'aetheris.client')
try {
    tspack sync --root $repoRoot
    # Materialize the explicitly declared repository-owned source dependency.
    # The locked third-party graph stays unchanged; no registry update occurs.
    $scope = Join-Path $repoRoot 'node_modules/@aetheris'
    New-Item -ItemType Directory -Force $scope | Out-Null
    $telosLink = Join-Path $scope 'three-telos'
    if (-not (Test-Path -LiteralPath $telosLink)) {
        New-Item -ItemType Junction -Path $telosLink -Target (Join-Path $repoRoot 'Aetheris.Web.Runtime/telos') | Out-Null
    }
    # A stale package-local installation must not shadow the workspace graph.
    $clientModules = Join-Path $repoRoot 'aetheris.client/node_modules'
    if (Test-Path -LiteralPath $clientModules) {
        $existing = Get-Item -LiteralPath $clientModules
        if ($existing.LinkType -ne 'Junction' -or $existing.Target -ne (Join-Path $repoRoot 'node_modules')) {
            $backup = Join-Path $output ('previous-client-modules-' + [Guid]::NewGuid().ToString('N'))
            if (-not $clientModules.StartsWith($repoPrefix) -or -not $backup.StartsWith($repoPrefix)) { throw 'Unsafe dependency move path.' }
            Move-Item -LiteralPath $clientModules -Destination $backup
        }
    }
    if (-not (Test-Path -LiteralPath $clientModules)) {
        New-Item -ItemType Junction -Path $clientModules -Target (Join-Path $repoRoot 'node_modules') | Out-Null
    }
    tspack check --root $repoRoot
    tspack run typecheck --root $repoRoot
    tspack run build --root $repoRoot
} finally { Pop-Location }
dotnet publish (Join-Path $repoRoot 'Aetheris.Cadmata.Desktop/Aetheris.Cadmata.Desktop.csproj') `
    -c Release -r win-x64 --self-contained true -m:1 -t:Rebuild `
    --artifacts-path (Join-Path $output 'build') `
    -p:Version=$version -p:InformationalVersion=$identity -p:IncludeSourceRevisionInInformationalVersion=false `
    -p:DebugType=None -p:DebugSymbols=false -o $stage
Move-Item -LiteralPath (Join-Path $stage 'Cadmata.Desktop.exe') -Destination (Join-Path $stage 'Cadmata.exe') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'aetheris.client/dist') -Destination (Join-Path $stage 'wwwroot') -Recurse -Force
# Referenced executable projects publish helper apphosts; no helper is needed.
foreach ($name in @('Aetheris.Forge.Host.exe', 'Cadmata.staticwebassets.runtime.json')) {
    Remove-Item -LiteralPath (Join-Path $stage $name) -Force -ErrorAction SilentlyContinue
}
Get-ChildItem $stage -Filter '*.pdb' -Recurse -File | Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $expanded "Microsoft.WebView2.FixedVersionRuntime.$runtimeVersion.x64") -Destination (Join-Path $stage 'webview2') -Recurse
New-Item -ItemType Directory -Force (Join-Path $stage 'samples'), (Join-Path $stage 'licenses') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'fixtures/Canonical/Integration/machined-mounting-block.firmament') -Destination (Join-Path $stage 'samples/mounting-block.firmament')
Copy-Item -LiteralPath (Join-Path $repoRoot 'fixtures/Canonical/Basics/cylinder.firmament') -Destination (Join-Path $stage 'samples/cylinder.firmament')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/public/cadmata/portable-windows.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $stage
node (Join-Path $repoRoot 'scripts/collect-cadmata-licenses.mts') $repoRoot $stage (Join-Path $output 'build/obj/Aetheris.Cadmata.Desktop/project.assets.json')

if (-not (Test-Path (Join-Path $stage 'wwwroot/index.html'))) { throw 'Production frontend missing.' }
foreach ($shader in @('mesh', 'line')) {
    if (-not (Get-ChildItem (Join-Path $stage 'wwwroot') -Recurse -File -Filter "$shader*.wgsl")) {
        throw "Telos $shader shader missing from production assets."
    }
}
$files = @(Get-ChildItem $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\','/')
    $purpose = if ($relative.StartsWith('webview2/')) { 'Fixed WebView2 browser runtime' }
        elseif ($relative.StartsWith('wwwroot/')) { 'Production frontend and shader assets' }
        elseif ($relative.StartsWith('licenses/') -or $relative -match 'LICENSE|NOTICES|README') { 'Redistribution notice or release instructions' }
        elseif ($relative.StartsWith('samples/')) { 'Bundled authored sample' }
        elseif ($relative -like 'Aetheris*' -or $relative -like 'Cadmata*') { 'Cadmata and existing CAD backend' }
        else { 'Self-contained .NET/ASP.NET/WindowsDesktop runtime or dependency' }
    [PSCustomObject][ordered]@{ path=$relative; bytes=$_.Length; purpose=$purpose; sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash }
})
$licenseAudit = Get-Content (Join-Path $stage 'licenses/AUDIT.json') -Raw | ConvertFrom-Json
$metadata = [ordered]@{ version=$version; identity=$identity; sourceRevision=$SourceRevision; modifiedSource=$dirty
    qualificationStatus='Local candidate; acceptance requires packaged qualification and complete redistribution audit'
    redistributionNoticesComplete=$licenseAudit.redistributionNoticesComplete
    source="https://github.com/yuechen-li-dev/Aetheris/tree/$SourceRevision"; webviewVersion=$runtimeVersion
    webviewCabUrl=$runtimeUrl; webviewCabSha256=$runtimeHash; inventoryBytes=($files | Measure-Object bytes -Sum).Sum; files=$files }
$metadata | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $stage 'BUILD-METADATA.json') -Encoding utf8
$zip = Join-Path $output 'Cadmata-Preview4-win-x64.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in (Get-ChildItem $stage -File -Recurse | Sort-Object FullName)) {
        $entry = $archive.CreateEntry([IO.Path]::GetRelativePath($stage, $file.FullName).Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
        $input = [IO.File]::OpenRead($file.FullName)
        $stream = $entry.Open()
        try { $input.CopyTo($stream) } finally { $stream.Dispose(); $input.Dispose() }
    }
} finally { $archive.Dispose() }
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  Cadmata-Preview4-win-x64.zip" | Set-Content (Join-Path $output 'SHA256SUMS') -Encoding ascii
[ordered]@{zip=$zip; sha256=$hash; packageBytes=(Get-Item $zip).Length;
    extractedBytes=(Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum; identity=$identity} |
    ConvertTo-Json | Set-Content (Join-Path $output 'package-summary.json')
Write-Output "Package: $zip ($((Get-Item $zip).Length) bytes), SHA256 $hash"
