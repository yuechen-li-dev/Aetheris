[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/local/display-projection-closeout' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$projectDirectory = Join-Path $output 'compiler-audit'
New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
$kernelProject = [Security.SecurityElement]::Escape((Join-Path $repo 'Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$kernelProject" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $projectDirectory 'Audit.csproj') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'audit-display-projection-closeout.cs') -Destination (Join-Path $projectDirectory 'Program.cs')
dotnet run --project (Join-Path $projectDirectory 'Audit.csproj') -c Release -- $repo $output
if ($LASTEXITCODE -ne 0) { throw "Display projection compiler audit failed (exit $LASTEXITCODE)." }
