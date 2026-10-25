param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$PortableDirectory
)
$ErrorActionPreference = 'Stop'
$release = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$portable = (Resolve-Path -LiteralPath $PortableDirectory).Path
$lines = @(Get-Content (Join-Path $release 'SHA256SUMS.txt'))
$archives = @(Get-ChildItem -LiteralPath $portable -Recurse -Filter '*.tar.gz' -File)
if ($archives.Count -ne 2) { throw 'Expected Linux x64 and macOS arm64 archives.' }
foreach ($line in $lines) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "Invalid existing checksum: $line" }
    $expected = $Matches[1]; $relative = $Matches[2]
    $file = Join-Path $release $relative
    if (-not (Test-Path -LiteralPath $file)) { $file = Join-Path $release ([IO.Path]::GetFileName($relative)) }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw "Existing release checksum failed: $relative" }
}
$rows = @()
foreach ($archive in $archives) {
    if ($archive.Name -notmatch '^Aetheris-[0-9][\w.-]+-(linux-x64|osx-arm64)\.tar\.gz$') { throw "Unexpected portable asset: $($archive.Name)" }
    $rid = $Matches[1]
    $hash = (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $receipt = Join-Path $archive.DirectoryName "SHA256SUMS-$rid.txt"
    if ((Get-Content -LiteralPath $receipt -Raw).Trim() -ne "$hash  $($archive.Name)") { throw "Portable receipt mismatch: $rid" }
    $destination = Join-Path $release $archive.Name
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { throw "Refusing to replace published binary: $($archive.Name)" }
    } else { Copy-Item -LiteralPath $archive.FullName -Destination $destination }
    if (-not ($lines | Where-Object { $_ -eq "$hash  $($archive.Name)" })) { $lines += "$hash  $($archive.Name)" }
    $rows += "| ``$($archive.Name)`` | $($archive.Length) | ``$hash`` |"
}
$lines | Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding utf8NoBOM
$inventory = Get-Content (Join-Path $release 'RELEASE-INVENTORY.md')
$table = @($inventory | Where-Object { $_.StartsWith('|') -and $_ -notmatch 'Aetheris-.+-(linux-x64|osx-arm64)\.tar\.gz' })
@('# Aetheris release inventory', '', 'SHA-256 checksums of the published binaries.', '', $table, $rows, '',
  'Bundles: Windows x64, Linux x64 (glibc), macOS arm64 (Apple Silicon). Each includes the CLI, browser viewer, examples and native Forge Host.') |
    ForEach-Object { $_ } | Set-Content (Join-Path $release 'RELEASE-INVENTORY.md') -Encoding utf8NoBOM
