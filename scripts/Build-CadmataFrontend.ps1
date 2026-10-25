$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command tspack -ErrorAction SilentlyContinue)) {
    throw 'TSPack is required to build Cadmata. Restore the repository build prerequisite; do not substitute an npm lockfile.'
}

# Telos exports ignored dist files. Build its producer before hydrating the
# consuming package, so a clean checkout follows the same dependency graph.
Push-Location (Join-Path $repoRoot 'Aetheris.Web.Runtime/telos')
try {
    npm ci --ignore-scripts
    npm run build
}
finally { Pop-Location }

Push-Location (Join-Path $repoRoot 'aetheris.client')
try {
    tspack sync --root $repoRoot
    # Path dependencies are immutable lockfile snapshots. A successful sync must
    # not let a newly compiled producer build against yesterday's consumer bytes.
    $producer = Join-Path $repoRoot 'Aetheris.Web.Runtime/telos/dist'
    $consumer = Join-Path $repoRoot 'aetheris.client/node_modules/@aetheris/three-telos/dist'
    $producerFiles = @(Get-ChildItem -LiteralPath $producer -Recurse -File)
    $consumerFiles = @(Get-ChildItem -LiteralPath $consumer -Recurse -File)
    $stale = $producerFiles.Count -ne $consumerFiles.Count
    foreach ($file in $producerFiles) {
        $relative = [IO.Path]::GetRelativePath($producer, $file.FullName)
        $materialized = Join-Path $consumer $relative
        if (-not (Test-Path -LiteralPath $materialized) -or
            (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $materialized).Hash) {
            $stale = $true
            break
        }
    }
    if ($stale) {
        throw 'The locked Telos path dependency is stale. Run tspack update at the repository root, review and commit ts-lock.toml, then rebuild. Do not qualify a stale renderer.'
    }
    tspack check --root $repoRoot
    tspack run typecheck --root $repoRoot
    tspack run test --root $repoRoot
    tspack run build --root $repoRoot
    tspack run lint --root $repoRoot
}
finally { Pop-Location }
