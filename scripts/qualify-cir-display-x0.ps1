param(
    [Parameter(Mandatory = $true)][string]$CopelandRoot,
    [Parameter(Mandatory = $true)][string]$NagaExecutable,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repository 'artifacts/local/cir-display-x0'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$CopelandRoot = [IO.Path]::GetFullPath($CopelandRoot)
$shaderProject = Join-Path $CopelandRoot 'src/Aurelian/Aurelian.Shaders/Aurelian.Shaders.csproj'
if (-not (Test-Path -LiteralPath $shaderProject)) {
    throw 'CopelandRoot must contain the real Aurelian.Shaders project.'
}
if (-not (Test-Path -LiteralPath $NagaExecutable)) {
    throw 'NagaExecutable must identify the installed Naga CLI.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$continuumProject = Join-Path $repository 'Aetheris.Continuum/Aetheris.Continuum.csproj'
$escapedShader = [Security.SecurityElement]::Escape($shaderProject)
$escapedContinuum = [Security.SecurityElement]::Escape($continuumProject)
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$escapedShader" />
    <ProjectReference Include="$escapedContinuum" />
  </ItemGroup>
</Project>
"@
Set-Content -LiteralPath (Join-Path $OutputDirectory 'Qualification.csproj') -Value $project -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'qualify-cir-display-x0.cs') -Destination (Join-Path $OutputDirectory 'Program.cs')
dotnet run --project (Join-Path $OutputDirectory 'Qualification.csproj') -c Release -- $OutputDirectory ([IO.Path]::GetFullPath($NagaExecutable))
if ($LASTEXITCODE -ne 0) { throw 'CIR shader qualification failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'cir-display-x0-witness.html') -Destination (Join-Path $OutputDirectory 'index.html')
