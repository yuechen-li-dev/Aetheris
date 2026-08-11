[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
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
Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $output 'packages') | Out-Null

Push-Location (Join-Path $repoRoot 'aetheris.client')
try {
    tspack sync
    tspack check
    tspack run typecheck
    tspack run test
    tspack run build
    tspack run lint
}
finally { Pop-Location }

dotnet publish (Join-Path $repoRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -o (Join-Path $stage 'Aetheris-win-x64')
dotnet publish (Join-Path $repoRoot 'Aetheris.Server/Aetheris.Server.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -o (Join-Path $stage 'Aetheris-win-x64/cadmata')
$endpoints = Join-Path $stage 'Aetheris-win-x64/cadmata/Cadmata.staticwebassets.endpoints.json'
if (Test-Path -LiteralPath $endpoints) {
    $canonicalEndpoints = [Regex]::Replace([IO.File]::ReadAllText($endpoints),
        '"Last-Modified","Value":"[^"]+"', '"Last-Modified","Value":"Mon, 01 Jan 2024 00:00:00 GMT"')
    [IO.File]::WriteAllText($endpoints, $canonicalEndpoints, [Text.UTF8Encoding]::new($false))
}
# Build the framework-dependent tool payload explicitly. Reusing the preceding
# RID-specific publish output can package stale non-RID bytes with a new filename.
dotnet pack (Join-Path $repoRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $output 'packages')
Get-ChildItem -LiteralPath (Join-Path $output 'packages') -Filter '*.nupkg' -File | ForEach-Object { Normalize-ZipArchive $_.FullName -NuGetPackage }
Get-ChildItem -LiteralPath $stage -Recurse -Filter '*.pdb' -File | Remove-Item -Force

$extensionRoot = Join-Path $repoRoot 'tools/vscode-firmament'
Push-Location $extensionRoot
try {
    npm install --no-save typescript esbuild @vscode/vsce @types/vscode @types/node vscode-textmate vscode-oniguruma
    npx tsc --noEmit
    node --test tests/*.test.ts
    npx esbuild src/extension.ts --bundle --platform=node --format=cjs --external:vscode --outfile=dist/extension.cjs
    npx vsce package --no-dependencies --out (Join-Path $output 'aetheris-firmament-0.2.0-preview.2.vsix')
}
finally { Pop-Location }
Normalize-ZipArchive (Join-Path $output 'aetheris-firmament-0.2.0-preview.2.vsix')

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
$releaseFiles = @($zip, (Join-Path $output 'aetheris-firmament-0.2.0-preview.2.vsix')) + (Get-ChildItem -LiteralPath (Join-Path $output 'packages') -Filter '*.nupkg' -File | Select-Object -ExpandProperty FullName)
$releaseFiles | Get-FileHash -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), $_.Path.Substring($output.Length + 1) } | Set-Content (Join-Path $output 'SHA256SUMS.txt')
