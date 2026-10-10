[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repo = Split-Path $PSScriptRoot -Parent
$commit = (Get-Content (Join-Path $PSScriptRoot 'managed-wgsl-commit-pin.txt') -Raw).Trim()
if ($commit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid managed WGSL commit pin.' }
$source = Join-Path $repo 'artifacts/local/managed-wgsl/source'
if (-not (Test-Path (Join-Path $source '.git'))) {
    New-Item -ItemType Directory -Force $source | Out-Null
    git -C $source init
    git -C $source remote add origin https://github.com/yuechen-li-dev/Copeland.git
    git -C $source sparse-checkout init --cone
    git -C $source sparse-checkout set src/Copeland
    git -C $source fetch --depth 1 --filter=blob:none origin $commit
    git -C $source checkout --detach FETCH_HEAD
}
if ((git -C $source rev-parse HEAD).Trim() -ne $commit -or (git -C $source status --porcelain)) {
    throw "Managed WGSL checkout must be clean at ${commit}: $source"
}
& (Join-Path $PSScriptRoot 'prepare-managed-wgsl.ps1') -CopelandRoot $source
