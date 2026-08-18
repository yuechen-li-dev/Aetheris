[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$stage = Join-Path $output 'stage'

function Normalize-ZipArchive([string]$Path, [switch]$NuGetPackage) {
    $temporary = "$Path.deterministic"
    $source = [System.IO.Compression.ZipFile]::OpenRead($Path)
    $target = [System.IO.Compression.ZipFile]::Open($temporary, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($sourceEntry in ($source.Entries | Sort-Object FullName)) {
            $entryName = $sourceEntry.FullName
            if ($NuGetPackage -and $entryName -match '^package/services/metadata/core-properties/.+\.psmdcp$') {
                $entryName = 'package/services/metadata/core-properties/aetheris.psmdcp'
            }
            $targetEntry = $target.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $targetEntry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            if ($sourceEntry.FullName.EndsWith('/')) { continue }
            $input = $sourceEntry.Open()
            $outputStream = $targetEntry.Open()
            try {
                if ($NuGetPackage -and $sourceEntry.FullName -eq '_rels/.rels') {
                    $reader = [System.IO.StreamReader]::new($input)
                    try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
                    $content = [Regex]::Replace($content, 'package/services/metadata/core-properties/[^"'']+\.psmdcp', 'package/services/metadata/core-properties/aetheris.psmdcp')
                    $content = [Regex]::Replace($content, '(manifest" Target="/Aetheris\.CLI\.nuspec" Id=")[^"]+', '${1}RManifest')
                    $content = [Regex]::Replace($content, '(core-properties" Target="/package/services/metadata/core-properties/aetheris\.psmdcp" Id=")[^"]+', '${1}RCoreProperties')
                    $writer = [System.IO.StreamWriter]::new($outputStream, [System.Text.UTF8Encoding]::new($false))
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

function Normalize-PeTimestamps([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    if ([BitConverter]::ToUInt32($bytes, $peOffset) -ne 0x00004550) {
        throw "Not a PE executable: $Path"
    }

    [BitConverter]::GetBytes([uint32]0).CopyTo($bytes, $peOffset + 8)
    $sectionCount = [BitConverter]::ToUInt16($bytes, $peOffset + 6)
    $optionalHeaderSize = [BitConverter]::ToUInt16($bytes, $peOffset + 20)
    $optionalHeaderOffset = $peOffset + 24
    $magic = [BitConverter]::ToUInt16($bytes, $optionalHeaderOffset)
    $dataDirectoryOffset = if ($magic -eq 0x20b) { 112 } elseif ($magic -eq 0x10b) { 96 } else { throw "Unsupported PE optional-header magic in $Path" }
    $debugDirectoryEntry = $optionalHeaderOffset + $dataDirectoryOffset + (6 * 8)
    $debugRva = [BitConverter]::ToUInt32($bytes, $debugDirectoryEntry)
    $debugSize = [BitConverter]::ToUInt32($bytes, $debugDirectoryEntry + 4)
    $sectionHeadersOffset = $optionalHeaderOffset + $optionalHeaderSize

    if ($debugRva -ne 0 -and $debugSize -ne 0) {
        for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++) {
            $sectionOffset = $sectionHeadersOffset + ($sectionIndex * 40)
            $virtualSize = [BitConverter]::ToUInt32($bytes, $sectionOffset + 8)
            $virtualAddress = [BitConverter]::ToUInt32($bytes, $sectionOffset + 12)
            $rawSize = [BitConverter]::ToUInt32($bytes, $sectionOffset + 16)
            $rawOffset = [BitConverter]::ToUInt32($bytes, $sectionOffset + 20)
            $mappedSize = [Math]::Max($virtualSize, $rawSize)
            if ($debugRva -lt $virtualAddress -or $debugRva -ge ($virtualAddress + $mappedSize)) { continue }

            $debugOffset = $rawOffset + ($debugRva - $virtualAddress)
            for ($entryOffset = $debugOffset; $entryOffset -lt ($debugOffset + $debugSize); $entryOffset += 28) {
                [BitConverter]::GetBytes([uint32]0).CopyTo($bytes, [int]$entryOffset + 4)
            }
            break
        }
    }

    [IO.File]::WriteAllBytes($Path, $bytes)
}

function Get-ReleaseRelativePath([string]$Root, [string]$Path) {
    $canonicalRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $canonicalPath = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $canonicalRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $canonicalPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release file is outside the staging root: $canonicalPath"
    }

    return $canonicalPath.Substring($rootPrefix.Length)
}

Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $output 'packages') | Out-Null

Push-Location (Join-Path $repoRoot 'aetheris.client')
try {
    if (-not (Get-Command tspack -ErrorAction SilentlyContinue)) {
        throw 'TSPack is required to build Cadmata. Restore the repository build prerequisite; do not substitute an npm lockfile.'
    }
    tspack sync
    tspack check
    tspack run typecheck
    tspack run test
    tspack run build
    tspack run lint
}
finally { Pop-Location }

# Release binaries must not inherit compiler state from an earlier publish with
# different properties (notably PublishAot). Start the managed release graph
# from a clean configuration so identical inputs produce identical archives.
dotnet clean (Join-Path $repoRoot 'Aetheris.slnx') -c Release

dotnet publish (Join-Path $repoRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release -r win-x64 --self-contained true -t:Rebuild -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $stage 'Aetheris-win-x64')
dotnet publish (Join-Path $repoRoot 'Aetheris.Forge.Host/Aetheris.Forge.Host.csproj') -c Release -r win-x64 --self-contained true -t:Rebuild -p:PublishAot=true -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $stage 'Aetheris-win-x64/forge-host')
Normalize-PeTimestamps (Join-Path $stage 'Aetheris-win-x64/forge-host/Aetheris.Forge.Host.exe')
dotnet publish (Join-Path $repoRoot 'Aetheris.Server/Aetheris.Server.csproj') -c Release -r win-x64 --self-contained true -t:Rebuild -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $stage 'Aetheris-win-x64/cadmata')
$endpoints = Join-Path $stage 'Aetheris-win-x64/cadmata/Cadmata.staticwebassets.endpoints.json'
if (Test-Path -LiteralPath $endpoints) {
    $canonicalEndpoints = [Regex]::Replace([IO.File]::ReadAllText($endpoints),
        '"Last-Modified","Value":"[^"]+"', '"Last-Modified","Value":"Mon, 01 Jan 2024 00:00:00 GMT"')
    [IO.File]::WriteAllText($endpoints, $canonicalEndpoints, [Text.UTF8Encoding]::new($false))
}

$bundleRoot = Join-Path $stage 'Aetheris-win-x64'
New-Item -ItemType Directory -Force -Path (Join-Path $bundleRoot 'docs'), (Join-Path $bundleRoot 'samples') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/public') -Destination (Join-Path $bundleRoot 'docs/public') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/public/release-bundle.md') -Destination (Join-Path $bundleRoot 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $bundleRoot 'LICENSE')
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $bundleRoot 'THIRD_PARTY_NOTICES.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'samples/forge-interop-x1') -Destination (Join-Path $bundleRoot 'samples/forge-interop-x1') -Recurse
$publicExamples = @(
    'fixtures/Canonical/valid/a4-machined-mounting-block.firmament',
    'fixtures/Canonical/valid/boss-pocket-mounting-block.firmament',
    'fixtures/Canonical/valid/box-hole-pmi.firmament',
    'fixtures/Canonical/valid/box-holes-pmi-chamfer.firmament',
    'fixtures/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament',
    'fixtures/Canonical/valid/record-array-pattern-holes.firmament',
    'fixtures/Canonical/valid/table-template-concept-path-compose.firmament',
    'fixtures/FEA/cantilever.firmament',
    'fixtures/FEA/inline-step-through-hole.firmament',
    'fixtures/InlineStep/testdata/canonical-through-hole.step',
    'fixtures/Materials/catalog-material-coupon.firmament',
    'fixtures/Primitive/valid/a3-pointed-cone-step-qualified.firmament',
    'fixtures/Primitive/valid/a3-sphere-step-qualified.firmament',
    'fixtures/Primitive/valid/a3-torus-step-qualified.firmament',
    'fixtures/PublicDogfood/ai-fea-a36-cantilever.firmament',
    'fixtures/PublicDogfood/ai-model.firmament',
    'fixtures/PublicDogfood/ai-sheet-metal.firmament',
    'fixtures/SheetMetal/m5-four-wall-tray-template.firmament',
    'fixtures/SheetMetal/preview3-l-bracket-hole.firmament'
)
foreach ($relativeExample in $publicExamples) {
    $sourceExample = Join-Path $repoRoot $relativeExample
    $destinationExample = Join-Path $bundleRoot $relativeExample
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destinationExample) | Out-Null
    Copy-Item -LiteralPath $sourceExample -Destination $destinationExample
}
# Build the framework-dependent tool payload explicitly. Reusing the preceding
# RID-specific publish output can package stale non-RID bytes with a new filename.
dotnet pack (Join-Path $repoRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release -t:Rebuild -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $output 'packages')
Get-ChildItem -LiteralPath (Join-Path $output 'packages') -Filter '*.nupkg' -File | ForEach-Object { Normalize-ZipArchive $_.FullName -NuGetPackage }
Get-ChildItem -LiteralPath $stage -Recurse -Filter '*.pdb' -File | Remove-Item -Force

$extensionRoot = Join-Path $repoRoot 'tools/vscode-firmament'
$extensionArtifact = Join-Path $output 'aetheris-firmament-0.3.0-preview.3.vsix'
Push-Location $extensionRoot
try {
    tspack sync
    tspack check
    tspack run typecheck
    tspack run test
    tspack run build
    tspack run package
    Move-Item -LiteralPath (Join-Path $extensionRoot 'dist/aetheris-firmament-0.3.0-preview.3.vsix') -Destination $extensionArtifact -Force
}
finally { Pop-Location }
Normalize-ZipArchive $extensionArtifact

$zip = Join-Path $output "Aetheris-$Version-win-x64.zip"
$sourceRoot = Join-Path $stage 'Aetheris-win-x64'
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = (Get-ReleaseRelativePath $sourceRoot $_.FullName).Replace('\', '/')
        $entry = $archive.CreateEntry("Aetheris-win-x64/$relative", [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $input = $_.OpenRead()
        $entryStream = $entry.Open()
        try { $input.CopyTo($entryStream) } finally { $entryStream.Dispose(); $input.Dispose() }
    }
}
finally { $archive.Dispose() }
$releaseFiles = @($zip, $extensionArtifact) + (Get-ChildItem -LiteralPath (Join-Path $output 'packages') -Filter '*.nupkg' -File | Select-Object -ExpandProperty FullName)
$releaseHashes = @($releaseFiles | ForEach-Object {
    $item = Get-Item -LiteralPath $_
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    [pscustomobject]@{
        Path = $item.FullName.Substring($output.Length + 1).Replace('\', '/')
        Bytes = $item.Length
        Sha256 = $hash.Hash.ToLowerInvariant()
    }
})
$releaseHashes | ForEach-Object { "{0}  {1}" -f $_.Sha256, $_.Path } | Set-Content (Join-Path $output 'SHA256SUMS.txt')

$inventory = @(
    "# Aetheris $Version publication inventory",
    "",
    "Qualified release target: Windows x64 (`win-x64`). Forge Host Protocol v1 and the VS Code extension version are independently versioned.",
    "",
    "| Artifact | Bytes | SHA-256 |",
    "|---|---:|---|"
)
$inventory += $releaseHashes | ForEach-Object { "| ``$($_.Path)`` | $($_.Bytes) | ``$($_.Sha256)`` |" }
$inventory += @(
    "",
    "The 16 public library packages are generated separately by ``scripts/package-public-libraries.ps1``. Public documentation is in ``docs/public`` and is also included in the Windows ZIP.")
[IO.File]::WriteAllLines((Join-Path $output 'RELEASE-INVENTORY.md'), [string[]]$inventory, [Text.UTF8Encoding]::new($false))
