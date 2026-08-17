param(
    [string]$Version = "2.0.0-preview.3",
    [string]$OutputDirectory = "artifacts/packages/public-libraries"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))

function Normalize-NuGetPackage([string]$Path) {
    $temporary = "$Path.deterministic"
    $source = [IO.Compression.ZipFile]::OpenRead($Path)
    $target = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($sourceEntry in ($source.Entries | Sort-Object FullName)) {
            $entryName = $sourceEntry.FullName
            if ($entryName -match '^package/services/metadata/core-properties/.+\.psmdcp$') {
                $entryName = 'package/services/metadata/core-properties/aetheris.psmdcp'
            }

            $targetEntry = $target.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
            $targetEntry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            if ($sourceEntry.FullName.EndsWith('/')) { continue }

            $input = $sourceEntry.Open()
            $outputStream = $targetEntry.Open()
            try {
                if ($sourceEntry.FullName -eq '_rels/.rels') {
                    $reader = [IO.StreamReader]::new($input)
                    try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
                    $content = [Regex]::Replace($content, 'package/services/metadata/core-properties/[^"'']+\.psmdcp', 'package/services/metadata/core-properties/aetheris.psmdcp')
                    $content = [Regex]::Replace($content, '(manifest" Target="/[^"]+\.nuspec" Id=")[^"]+', '${1}RManifest')
                    $content = [Regex]::Replace($content, '(core-properties" Target="/package/services/metadata/core-properties/aetheris\.psmdcp" Id=")[^"]+', '${1}RCoreProperties')
                    $writer = [IO.StreamWriter]::new($outputStream, [Text.UTF8Encoding]::new($false))
                    try { $writer.Write($content) } finally { $writer.Dispose() }
                }
                else { $input.CopyTo($outputStream) }
            }
            finally { $outputStream.Dispose(); $input.Dispose() }
        }
    }
    finally { $target.Dispose(); $source.Dispose() }

    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

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
    "Aetheris.Geometry/Aetheris.Geometry.csproj",
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
        # The package payload must not inherit assemblies from an earlier commit
        # or a RID-specific publish. Rebuild every public package payload and
        # exclude debug symbols so the staged bytes reflect this invocation.
        dotnet pack $project --configuration Release --output $outputPath -t:Rebuild `
            -p:PackageVersion=$Version -p:DebugType=None -p:DebugSymbols=false
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed for $project"
        }
    }

    $packageIds = $projects | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) }
    [IO.File]::WriteAllLines(
        (Join-Path $outputPath "PACKAGE_ORDER.txt"),
        [string[]]$packageIds,
        [Text.UTF8Encoding]::new($false))

    $packages = @(Get-ChildItem -LiteralPath $outputPath -Filter "*.nupkg" -File)
    if ($packages.Count -ne $projects.Count) {
        throw "Expected $($projects.Count) packages but found $($packages.Count)."
    }
    $packages | ForEach-Object { Normalize-NuGetPackage $_.FullName }
} finally {
    Pop-Location
}

Write-Output "Packed $($projects.Count) public Aetheris libraries to $outputPath"
