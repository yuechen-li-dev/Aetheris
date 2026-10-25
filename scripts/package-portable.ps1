param(
    [Parameter(Mandatory)][ValidateSet('linux-x64', 'osx-arm64')][string]$Runtime,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$ReferenceBundleDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:MSBUILDDISABLENODEREUSE = '1'
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$reference = (Resolve-Path -LiteralPath $ReferenceBundleDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$work = "$output.work"
if ((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $work)) { throw 'Use fresh portable output/work directories.' }
$architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
if (($Runtime -eq 'osx-arm64' -and (-not $IsMacOS -or $architecture -ne 'Arm64')) -or
    ($Runtime -eq 'linux-x64' -and (-not $IsLinux -or $architecture -ne 'X64'))) {
    throw "Build and qualify $Runtime on its native OS/architecture, not $([Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier)."
}
$revision = (git -C $source rev-parse HEAD).Trim()
$referenceMetadata = Get-Content (Join-Path $reference 'BUILD-METADATA.json') -Raw | ConvertFrom-Json
if ($referenceMetadata.sourceRevision -ne $revision -or $referenceMetadata.modifiedSource -or $referenceMetadata.version -ne $Version) {
    throw 'Portable code and qualified reference frontend must have the same clean source/version.'
}
if (@(git -C $source status --porcelain).Count) { throw 'Portable product source is modified.' }
$bundleName = "Aetheris-$Runtime"
$bundle = Join-Path $work $bundleName
New-Item -ItemType Directory -Path $bundle, $output | Out-Null
$publishArgs = @('-c', 'Release', '-r', $Runtime, '--self-contained', 'true', '-m:1', '-nr:false',
    '-p:UseSharedCompilation=false', '-t:Rebuild', "-p:Version=$Version", '-p:DebugType=None', '-p:DebugSymbols=false')
dotnet publish (Join-Path $source 'Aetheris.CLI/Aetheris.CLI.csproj') @publishArgs -o $bundle
& (Join-Path $PSScriptRoot 'Test-CoreCliPayload.ps1') -AssetsPath (Join-Path $source 'Aetheris.CLI/obj/project.assets.json') -PublishDirectory $bundle
dotnet publish (Join-Path $source 'Aetheris.Forge.Host/Aetheris.Forge.Host.csproj') @publishArgs -p:PublishAot=true -o (Join-Path $bundle 'forge-host')
$server = Join-Path $work 'server'
dotnet publish (Join-Path $source 'Aetheris.Server/Aetheris.Server.csproj') @publishArgs -o $server
foreach ($file in Get-ChildItem -LiteralPath $server -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($server, $file.FullName)
    if ($relative -eq 'Aetheris.Forge.Host') { continue } # Referenced executable; native host lives under forge-host/.
    $destination = Join-Path $bundle $relative
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            throw "CLI and browser server disagree on shared file: $relative"
        }
    } else {
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
foreach ($directory in @('wwwroot', 'fixtures', 'testdata', 'samples')) {
    Copy-Item -LiteralPath (Join-Path $reference $directory) -Destination (Join-Path $bundle $directory) -Recurse
}
New-Item -ItemType Directory -Path (Join-Path $bundle 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/public') -Destination (Join-Path $bundle 'docs/public') -Recurse
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'CORRESPONDING-SOURCE.md')) {
    Copy-Item -LiteralPath (Join-Path $reference $name) -Destination (Join-Path $bundle $name)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/public/release-bundle.md') -Destination (Join-Path $bundle 'README.md')
$readme = Join-Path $bundle 'README.md'
[IO.File]::WriteAllText($readme, [regex]::Replace([IO.File]::ReadAllText($readme), '\]\((?!https?://|#)([^)]+)\)', '](docs/public/$1)'))
$packagingRevision = (git -C (Split-Path $PSScriptRoot -Parent) rev-parse HEAD).Trim()
Add-Content -LiteralPath (Join-Path $bundle 'CORRESPONDING-SOURCE.md') -Value "`nPortable packaging scripts: https://github.com/yuechen-li-dev/Aetheris/tree/$packagingRevision/scripts"
@{ version=$Version; identity="$Version+$revision"; sourceRevision=$revision; modifiedSource=$false;
   runtime=$Runtime; defaultPresentation='Browser'; webviewMode=$null; packagingRevision=$packagingRevision } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'BUILD-METADATA.json')
node (Join-Path $PSScriptRoot 'collect-cadmata-licenses.mts') $source $bundle (Join-Path $source 'Aetheris.Server/obj/project.assets.json') (Join-Path $reference 'licenses')
if ($IsMacOS) {
    $launcher = Join-Path $bundle 'Open Cadmata.command'
    [IO.File]::WriteAllText($launcher, "#!/bin/sh`ncd `"`$(dirname `"`$0`")`" || exit 1`nexec ./Cadmata `"`$@`"`n", [Text.UTF8Encoding]::new($false))
    chmod +x $launcher
}
foreach ($executable in @('aetheris', 'Cadmata', 'forge-host/Aetheris.Forge.Host')) { chmod +x (Join-Path $bundle $executable) }
$archive = Join-Path $output "Aetheris-$Version-$Runtime.tar.gz"
tar -czf $archive -C $work $bundleName
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archive))" | Set-Content (Join-Path $output "SHA256SUMS-$Runtime.txt")
