[CmdletBinding()]
param(
    [string]$AssetsPath = "Aetheris.CLI/obj/project.assets.json",
    [string]$PublishDirectory,
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Reflection.Metadata

function Is-RuntimeOwner([string]$Name) {
    return $Name -match '^(runtimepack\.)?Microsoft\.NETCore\.App\.Runtime\.'
}

function Get-NativePaths($Library) {
    if ($Library.native) { $Library.native.Keys }
    if ($Library.runtimeTargets) { $Library.runtimeTargets.GetEnumerator() | Where-Object { $_.Value.assetType -eq 'native' } | ForEach-Object Key }
}

function Test-ResolvedGraph([string]$JsonPath) {
    $assets = Get-Content -LiteralPath $JsonPath -Raw | ConvertFrom-Json -AsHashtable
    if (!$assets.targets -or !$assets.targets.Count) { throw "No dependency targets found in $JsonPath" }
    foreach ($target in $assets.targets.Values) {
        foreach ($library in $target.GetEnumerator()) {
            if (Is-RuntimeOwner $library.Key) { continue }
            $native = @(Get-NativePaths $library.Value)
            if (@($native | Where-Object { $_ -and $_ -notlike '*/_._' }).Count) {
                throw "Default CLI resolves third-party native assets from $($library.Key): $($native -join ', ')"
            }
        }
    }
}

function Test-Payload($Files, $Open) {
    $deps = @($Files | Where-Object { $_ -match '(^|/)aetheris\.deps\.json$' })
    if ($deps.Count -ne 1) { throw 'Payload must contain exactly one aetheris.deps.json.' }
    $allowedRuntimeFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $stream = & $Open $deps[0]
    $reader = [IO.StreamReader]::new($stream)
    try { $graph = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
    if (!$graph.targets -or !$graph.targets.Count) { throw 'Payload dependency receipt has no targets.' }
    foreach ($target in $graph.targets.Values) {
        foreach ($library in $target.GetEnumerator()) {
            $native = @(Get-NativePaths $library.Value)
            foreach ($file in ($native | Where-Object { $_ -and $_ -notlike '*/_._' })) {
                if (!(Is-RuntimeOwner $library.Key)) { throw "Payload contains third-party native dependency $($library.Key): $file" }
                [void]$allowedRuntimeFiles.Add([IO.Path]::GetFileName($file))
            }
        }
    }
    foreach ($file in $Files) {
        $name = [IO.Path]::GetFileName($file)
        if ($allowedRuntimeFiles.Contains($name)) { continue }
        if ($file -match '\.(so(\.\d+)*|dylib|node)$') { throw "Unowned native asset in CLI payload: $file" }
        if ($file -notmatch '\.(dll|exe)$') { continue }
        # Our own framework-dependent apphost is executable bootstrap code, like .NET itself.
        if ($name -in @('aetheris.exe', 'aetheris')) { continue }
        $input = & $Open $file
        $memory = [IO.MemoryStream]::new()
        try {
            $input.CopyTo($memory); $memory.Position = 0
            $pe = [Reflection.PortableExecutable.PEReader]::new($memory)
            try { if (!$pe.HasMetadata) { throw "Unowned native PE in CLI payload: $file" } } finally { $pe.Dispose() }
        }
        finally { $input.Dispose(); $memory.Dispose() }
    }
}

Test-ResolvedGraph $AssetsPath
if ($PublishDirectory) {
    $root = [IO.Path]::GetFullPath($PublishDirectory)
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/') })
    Test-Payload $files { param($name) [IO.File]::OpenRead((Join-Path $root $name)) }
}
if ($PackagePath) {
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
    try {
        $files = @($archive.Entries | Where-Object { !$_.FullName.EndsWith('/') } | ForEach-Object FullName)
        Test-Payload $files { param($name) $archive.GetEntry($name).Open() }
    }
    finally { $archive.Dispose() }
}
Write-Output 'Core CLI dependency graph and supplied payloads contain no third-party OS-native libraries (.NET runtime/apphost excluded).'
