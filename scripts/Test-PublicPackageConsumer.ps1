param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Version = '2.0.0'
)
$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'
$root = Split-Path $PSScriptRoot -Parent
$feed = (Resolve-Path -LiteralPath $PackageDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh consumer directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
foreach ($name in @('PublicPackageSmoke.csproj', 'Program.cs')) {
    Copy-Item -LiteralPath (Join-Path $root "test-support/PublicPackageSmoke/$name") -Destination $output
}
$escaped = [Security.SecurityElement]::Escape($feed)
[IO.File]::WriteAllText((Join-Path $output 'NuGet.Config'), "<configuration><packageSources><clear/><add key='candidate' value='$escaped'/><add key='nuget.org' value='https://api.nuget.org/v3/index.json'/></packageSources></configuration>")
$cache = Join-Path $output 'packages'
$project = Join-Path $output 'PublicPackageSmoke.csproj'
dotnet restore $project --configfile (Join-Path $output 'NuGet.Config') --packages $cache --no-cache -m:1 -nr:false -p:AetherisReleaseVersion=$Version
if ($LASTEXITCODE -ne 0) { throw 'Empty-cache public consumer restore failed.' }
dotnet build $project -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RestorePackagesPath=$cache -p:AetherisReleaseVersion=$Version
if ($LASTEXITCODE -ne 0) { throw 'Public consumer build failed.' }
$previousVersion = $env:AETHERIS_RELEASE_VERSION
try {
    $env:AETHERIS_RELEASE_VERSION = $Version
    dotnet (Join-Path $output 'bin/Release/net10.0/PublicPackageSmoke.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Public consumer execution failed.' }
}
finally { $env:AETHERIS_RELEASE_VERSION = $previousVersion }
