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
    tspack check --root $repoRoot
    tspack run typecheck --root $repoRoot
    tspack run test --root $repoRoot
    tspack run build --root $repoRoot
    tspack run lint --root $repoRoot
}
finally { Pop-Location }
