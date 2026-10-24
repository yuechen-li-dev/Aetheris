param(
    [Parameter(Mandatory)][string]$BundleDirectory,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [Parameter(Mandatory)][string]$Version,
    [string]$WebViewCab
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repo = Split-Path $PSScriptRoot -Parent
$sourceRevision = git -C $repo rev-parse HEAD
$sourceStatus = @(git -C $repo status --porcelain)
$modified = $sourceStatus.Count -gt 0
if ($env:GITHUB_ACTIONS -eq 'true' -and $modified) {
    throw "Hosted desktop publication requires clean product source. Unexpected paths:`n$($sourceStatus -join "`n")"
}
$bundle = (Resolve-Path -LiteralPath $BundleDirectory).Path
$work = [IO.Path]::GetFullPath($WorkDirectory)
if (Test-Path -LiteralPath $work) { throw 'Use a fresh desktop publish work directory.' }
if (Test-Path -LiteralPath (Join-Path $bundle 'webview2')) { throw 'Use a fresh CLI bundle directory; desktop runtime is already present.' }
New-Item -ItemType Directory -Path $work | Out-Null
$publish = Join-Path $work 'publish'
dotnet publish (Join-Path $repo 'Aetheris.Cadmata.Desktop/Aetheris.Cadmata.Desktop.csproj') -c Release -r win-x64 --self-contained true -m:1 -nr:false -p:UseSharedCompilation=false -t:Rebuild -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o $publish

# Keep the server entry point for explicit headless use; the public apphost is
# the existing WinForms/WebView2 desktop, not the server's similarly named exe.
Move-Item -LiteralPath (Join-Path $publish 'Cadmata.exe') -Destination (Join-Path $publish 'Cadmata.Server.exe')
Move-Item -LiteralPath (Join-Path $publish 'Cadmata.Desktop.exe') -Destination (Join-Path $publish 'Cadmata.exe')
Remove-Item -LiteralPath (Join-Path $publish 'Aetheris.Forge.Host.exe') -ErrorAction SilentlyContinue
$desktopDeps = Get-Content -LiteralPath (Join-Path $publish 'Cadmata.Desktop.deps.json') -Raw | ConvertFrom-Json
$runtimeTarget = $desktopDeps.targets.PSObject.Properties[$desktopDeps.runtimeTarget.name].Value
$desktopRuntime = @($runtimeTarget.PSObject.Properties | Where-Object Name -Like 'runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/*')
if ($desktopRuntime.Count -ne 1) { throw 'Expected one SDK-resolved WindowsDesktop runtime pack.' }
$desktopRuntimeFiles = @($desktopRuntime[0].Value.runtime.PSObject.Properties.Name)
foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($publish, $file.FullName)
    $destination = Join-Path $bundle $relative
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            # WindowsDesktop supplies the desktop-compatible framework facades
            # (for example System.Drawing) in place of the Core-only facades.
            # Use the SDK's resolved pack ownership, never override application
            # dependencies or guess compatibility from an assembly's name.
            if ($relative -notin $desktopRuntimeFiles) { throw "CLI and desktop publish incompatible shared files: $relative" }
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
        continue
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}

# Same pinned offline browser as the existing portable desktop distribution.
# WebView2 is the desktop UI runtime; it is not included in the CLI NuGet tool.
$runtimeVersion = '153.0.4234.48'
$runtimeUrl = 'https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/08cd33ee-d109-49b8-9301-9f0bea43c575/Microsoft.WebView2.FixedVersionRuntime.153.0.4234.48.x64.cab'
$runtimeHash = '11E8240CB0BC56DCD3E4498907203C251346F65107FE35A3A13E152C7D51C79E'
if (-not $WebViewCab) {
    $WebViewCab = Join-Path $work 'webview2-x64.cab'
    Invoke-WebRequest $runtimeUrl -OutFile $WebViewCab
}
if ((Get-FileHash -LiteralPath $WebViewCab -Algorithm SHA256).Hash -ne $runtimeHash) { throw 'Fixed WebView2 CAB hash mismatch.' }
$expanded = Join-Path $work 'runtime-expanded'
New-Item -ItemType Directory -Path $expanded | Out-Null
expand.exe $WebViewCab '-F:*' $expanded > (Join-Path $work 'runtime-expansion.log')
Copy-Item -LiteralPath (Join-Path $expanded "Microsoft.WebView2.FixedVersionRuntime.$runtimeVersion.x64") -Destination (Join-Path $bundle 'webview2') -Recurse
New-Item -ItemType Directory -Force -Path (Join-Path $bundle 'samples') | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'fixtures/Canonical/Integration/machined-mounting-block.firmament') -Destination (Join-Path $bundle 'samples/mounting-block.firmament')
$frontend = Join-Path $repo 'aetheris.client/dist'
if (-not (Test-Path -LiteralPath (Join-Path $frontend 'bundled-packages.json'))) { throw 'Build the production frontend before publishing the desktop.' }
New-Item -ItemType Directory -Force -Path (Join-Path $bundle 'wwwroot') | Out-Null
Get-ChildItem -LiteralPath $frontend | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $bundle 'wwwroot') -Recurse -Force }
$compilerRevision = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'managed-wgsl-commit-pin.txt') -Raw).Trim()
@{version=$Version;identity="$Version+$sourceRevision" + $(if ($modified) { '.modified' } else { '' });sourceRevision=$sourceRevision;modifiedSource=$modified;webviewVersion=$runtimeVersion;webviewCabUrl=$runtimeUrl;webviewCabSha256=$runtimeHash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'BUILD-METADATA.json')
@"
# Corresponding source

Aetheris / Cadmata (AGPL-3.0-only):
https://github.com/yuechen-li-dev/Aetheris/tree/$sourceRevision
https://github.com/yuechen-li-dev/Aetheris/archive/$sourceRevision.zip

Copeland managed WGSL compiler (GPL-3.0):
https://github.com/yuechen-li-dev/Copeland/tree/$compilerRevision
https://github.com/yuechen-li-dev/Copeland/archive/$compilerRevision.zip

Build scripts and exact compiler source pins are in the Aetheris revision above.
The projects retain their licenses and are combined under GPLv3 / AGPLv3
section 13. Full dependency notices are under licenses/.
"@ | Set-Content -LiteralPath (Join-Path $bundle 'CORRESPONDING-SOURCE.md') -Encoding utf8
node (Join-Path $repo 'scripts/collect-cadmata-licenses.mts') $repo $bundle (Join-Path $repo 'Aetheris.Cadmata.Desktop/obj/project.assets.json')
