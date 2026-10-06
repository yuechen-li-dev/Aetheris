param([Parameter(Mandatory=$true)][string]$CopelandRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'artifacts/local/telos-taa/compiler'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$backend = [Security.SecurityElement]::Escape((Join-Path $CopelandRoot 'src/Copeland/Copeland.TS.Backend.Wgsl/Copeland.TS.Backend.Wgsl.csproj'))
@"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
 <ItemGroup><ProjectReference Include="$backend"/></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Compiler.csproj')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'compile-telos-temporal-policy.cs') -Destination (Join-Path $output 'Program.cs')
dotnet run --project (Join-Path $output 'Compiler.csproj') -c Release -- (Join-Path $repo 'fixtures/three-telos/temporal-policy.v.ts') (Join-Path $repo 'Aetheris.Web.Runtime/telos/src/shaders/temporal-policy.wgsl')
if ($LASTEXITCODE -ne 0) { throw 'Temporal policy compilation failed.' }
