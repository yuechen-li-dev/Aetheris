param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,

    [Parameter(Mandatory = $true)]
    [string]$PublicLibrariesDirectory
)

$ErrorActionPreference = "Stop"

$releaseRoot = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$publicLibrariesRoot = (Resolve-Path -LiteralPath $PublicLibrariesDirectory).Path
$publicPackageDestination = Join-Path $releaseRoot "packages/public-libraries"

New-Item -ItemType Directory -Force -Path $publicPackageDestination | Out-Null

$publicPackages = @(Get-ChildItem -LiteralPath $publicLibrariesRoot -Filter "*.nupkg" -File | Sort-Object Name)
if ($publicPackages.Count -ne 16) {
    throw "Expected 16 public library packages in '$publicLibrariesRoot', found $($publicPackages.Count)."
}

foreach ($package in $publicPackages) {
    Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $publicPackageDestination $package.Name) -Force
}

$packageOrder = Join-Path $publicLibrariesRoot "PACKAGE_ORDER.txt"
if (-not (Test-Path -LiteralPath $packageOrder -PathType Leaf)) {
    throw "Public library package order is missing: $packageOrder"
}
Copy-Item -LiteralPath $packageOrder -Destination (Join-Path $publicPackageDestination "PACKAGE_ORDER.txt") -Force

$binaryArtifacts = @(
    Get-ChildItem -LiteralPath $releaseRoot -File -Recurse |
        Where-Object { $_.Extension -in @(".zip", ".vsix", ".nupkg") } |
        Sort-Object FullName
)

$releaseHashes = foreach ($artifact in $binaryArtifacts) {
    $relativePath = [System.IO.Path]::GetRelativePath($releaseRoot, $artifact.FullName).Replace("\", "/")
    $hash = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject]@{
        Path = $relativePath
        Bytes = $artifact.Length
        Sha256 = $hash
    }
}

$checksumLines = $releaseHashes | ForEach-Object { "$($_.Sha256)  $($_.Path)" }
Set-Content -LiteralPath (Join-Path $releaseRoot "SHA256SUMS.txt") -Value $checksumLines -Encoding utf8NoBOM

$inventoryLines = @(
    "# Preview 3 release inventory"
    ""
    "Generated from the staged publication directory. Hashes use SHA-256."
    ""
    "| Artifact | Bytes | SHA-256 |"
    "| --- | ---: | --- |"
)
$inventoryLines += $releaseHashes | ForEach-Object { "| ``$($_.Path)`` | $($_.Bytes) | ``$($_.Sha256)`` |" }
$inventoryLines += @(
    ""
    "The inventory contains the Windows x64 bundle, Firmament VSIX, CLI package, and all 16 public library packages."
)
Set-Content -LiteralPath (Join-Path $releaseRoot "RELEASE-INVENTORY.md") -Value $inventoryLines -Encoding utf8NoBOM

Write-Host "Finalized $($releaseHashes.Count) publication artifacts in $releaseRoot"
Write-Host "Checksums: $(Join-Path $releaseRoot 'SHA256SUMS.txt')"
Write-Host "Inventory: $(Join-Path $releaseRoot 'RELEASE-INVENTORY.md')"
