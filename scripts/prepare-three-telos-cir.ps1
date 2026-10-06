param([Parameter(Mandatory=$true)][string]$CopelandRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'artifacts/local/three-telos/cir'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$continuum = [Security.SecurityElement]::Escape((Join-Path $repo 'Aetheris.Continuum/Aetheris.Continuum.csproj'))
$backend = [Security.SecurityElement]::Escape((Join-Path $CopelandRoot 'src/Copeland/Copeland.TS.Backend.Wgsl/Copeland.TS.Backend.Wgsl.csproj'))
if (-not (Test-Path -LiteralPath $backend)) { throw 'CopelandRoot must contain the direct managed WGSL backend.' }
@"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
 <ItemGroup><ProjectReference Include="$continuum"/><ProjectReference Include="$backend"/></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Qualification.csproj') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'qualify-three-telos-cir.cs') -Destination (Join-Path $output 'Program.cs')
dotnet run --project (Join-Path $output 'Qualification.csproj') -c Release -- $output (Join-Path $repo 'Aetheris.Kernel.Firmament/Display/telos-field.v.ts')
if ($LASTEXITCODE -ne 0) { throw 'Direct CIR WGSL compilation failed.' }
