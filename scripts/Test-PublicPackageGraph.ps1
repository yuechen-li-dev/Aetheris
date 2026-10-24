param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = (Resolve-Path -LiteralPath $PackageDirectory).Path
$order = @(Get-Content -LiteralPath (Join-Path $root 'PACKAGE_ORDER.txt'))
if ($order.Count -eq 0 -or @($order | Select-Object -Unique).Count -ne $order.Count) { throw 'Package order must contain unique package identities.' }
$available = @{}
foreach ($file in Get-ChildItem -LiteralPath $root -Filter '*.nupkg' -File) {
    $zip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
    try {
        $entry = @($zip.Entries | Where-Object FullName -Like '*.nuspec')
        if ($entry.Count -ne 1) { throw "Expected one manifest in $($file.Name)." }
        $reader = [IO.StreamReader]::new($entry[0].Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $manifest.package.metadata
        $id = [string]$metadata.id
        if ($metadata.version -ne $Version -or $file.Name -ne "$id.$Version.nupkg") { throw "Wrong identity/version in $($file.Name)." }
        if ($available.ContainsKey($id)) { throw "Duplicate package $id." }
        $available[$id] = $metadata
    }
    finally { $zip.Dispose() }
}
if ($available.Count -ne $order.Count) { throw 'Manifest count differs from package order.' }
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($id in $order) {
    if (-not $available.ContainsKey($id)) { throw "Missing public package $id." }
    foreach ($dependency in $available[$id].SelectNodes('.//dependency')) {
        if ($dependency.id -notlike 'Aetheris.*') { continue }
        if (-not $visited.Contains($dependency.id)) { throw "$id depends on missing or unordered package $($dependency.id)." }
        if ($dependency.version -ne $Version) { throw "$id has a mismatched dependency: $($dependency.id) $($dependency.version)." }
    }
    [void]$visited.Add($id)
}
Write-Host "Verified dependency-closed public graph: $($order.Count) packages at $Version."
