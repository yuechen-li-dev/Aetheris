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
        $relative = [System.IO.Path]::GetRelativePath($sourceRoot, $_.FullName).Replace('\', '/')
        $entry = $archive.CreateEntry("Aetheris-win-x64/$relative", [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $input = $_.OpenRead()
        $entryStream = $entry.Open()
        try { $input.CopyTo($entryStream) } finally { $entryStream.Dispose(); $input.Dispose() }
    }
}
finally { $archive.Dispose() }
$releaseFiles = @($zip, $extensionArtifact) + (Get-ChildItem -LiteralPath (Join-Path $output 'packages') -Filter '*.nupkg' -File | Select-Object -ExpandProperty FullName)
$releaseFiles | Get-FileHash -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), $_.Path.Substring($output.Length + 1) } | Set-Content (Join-Path $output 'SHA256SUMS.txt')
