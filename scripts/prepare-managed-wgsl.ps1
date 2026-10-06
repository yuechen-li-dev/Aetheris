param([Parameter(Mandatory=$true)][string]$CopelandRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$backend = Join-Path $CopelandRoot 'src/Copeland/Copeland.TS.Backend.Wgsl/Copeland.TS.Backend.Wgsl.csproj'
$projects = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Visit([string]$path) {
    $path = [IO.Path]::GetFullPath($path)
    if (-not $projects.Add($path)) { return }
    [xml]$project = Get-Content -LiteralPath $path -Raw
    foreach ($reference in $project.Project.ItemGroup.ProjectReference) {
        if ($reference.Include) { Visit (Join-Path (Split-Path $path -Parent) $reference.Include) }
    }
}
Visit $backend
$files = @($projects | ForEach-Object {
    Get-ChildItem -LiteralPath (Split-Path $_ -Parent) -Recurse -File |
        Where-Object { $_.Extension -in '.cs','.csproj' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
}) + @(Get-ChildItem -LiteralPath $CopelandRoot -Filter 'Directory.*.props')
$manifest = ($files | Sort-Object FullName -Unique | ForEach-Object {
    [IO.Path]::GetRelativePath($CopelandRoot,$_.FullName).Replace('\','/') + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
}) -join "`n"
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($manifest))).ToLowerInvariant()
$version = '0.1.0-preview.1-a3.' + $hash.Substring(0,16)
$pin = Join-Path $repo 'scripts/managed-wgsl-source-pin.txt'
if ((Test-Path $pin) -and (Get-Content $pin -Raw).Trim() -ne $hash) { throw 'Managed WGSL producer source differs from the reviewed source pin.' }
$output = Join-Path $repo 'artifacts/local/cir-artifact-binding/packages'
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($project in $projects | Sort-Object) {
    dotnet pack $project -c Release -o $output "-p:PackageVersion=$version" "-p:Version=$version" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Managed WGSL dependency pack failed: $project" }
}
Write-Output "Source pin: $hash"
Write-Output "Package version: $version"
