param(
    [string]$Version = "2.0.0-preview.2",
    [string]$OutputDirectory = "artifacts/packages/public-libraries"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))

if (Test-Path -LiteralPath $outputPath) {
    $existing = @(Get-ChildItem -LiteralPath $outputPath -Force)
    if ($existing.Count -gt 0) {
        throw "Package output directory must be empty: $outputPath"
    }
} else {
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}

# Topological order is also the intended NuGet publication order.
$projects = @(
    "Aetheris.Kernel.Core/Aetheris.Kernel.Core.csproj",
    "Aetheris.Modules/Aetheris.Modules.csproj",
    "Aetheris.Semantics/Aetheris.Semantics.csproj",
    "Aetheris.Forge/Aetheris.Forge.csproj",
    "Aetheris.Collaboration/Aetheris.Collaboration.csproj",
    "Aetheris.Kernel.StandardLibrary/Aetheris.Kernel.StandardLibrary.csproj",
    "Aetheris.Continuum/Aetheris.Continuum.csproj",
    "Aetheris.Surfacing/Aetheris.Surfacing.csproj",
    "Aetheris.Piping/Aetheris.Piping.csproj",
    "Aetheris.SheetMetal/Aetheris.SheetMetal.csproj",
    "Aetheris.Modules.BuiltIn/Aetheris.Modules.BuiltIn.csproj",
    "Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj",
    "Aetheris.FEA/Aetheris.FEA.csproj",
    "Aetheris.Forge.Host/Aetheris.Forge.Host.csproj",
    "Aetheris.Forge.KernelSDK/Aetheris.Forge.KernelSDK.csproj"
)

Push-Location $repoRoot
try {
    foreach ($project in $projects) {
        dotnet pack $project --configuration Release --output $outputPath -p:PackageVersion=$Version
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed for $project"
        }
    }

    $packageIds = $projects | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) }
    $packageIds | Set-Content -LiteralPath (Join-Path $outputPath "PACKAGE_ORDER.txt") -Encoding utf8NoBOM

    $packages = @(Get-ChildItem -LiteralPath $outputPath -Filter "*.nupkg" -File)
    if ($packages.Count -ne $projects.Count) {
        throw "Expected $($projects.Count) packages but found $($packages.Count)."
    }
} finally {
    Pop-Location
}

Write-Output "Packed $($projects.Count) public Aetheris libraries to $outputPath"
