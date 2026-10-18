<#
.SYNOPSIS
Previews obsolete intermediate output from the October 2026 corpus pass.
.DESCRIPTION
Only the fixed paths below are eligible. Final qualification outputs, reports,
source, fixtures and baseline evidence are retained. Review without arguments;
use -Apply to remove the listed intermediates, or -Apply -WhatIf to preview
PowerShell's deletion operations. This script does not stop runtime processes.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Aetheris.slnx') -PathType Leaf)) {
    throw 'Run the checked-in script from its Aetheris scripts directory.'
}
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/local/corpus-modernization'))
$prefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$relativeTargets = @(
    'after',
    'after-structural',
    'accepted-canonical',
    'editor/bin',
    'editor/obj',
    'baseline-structural/entries.jsonl',
    'accepted-structural/entries.jsonl'
)

# Resolve and check every target before the first deletion. Reject junctions and
# symlinks both above and within a target, so recursion cannot escape the workspace.
$targets = foreach ($relative in $relativeTargets) {
    $target = [IO.Path]::GetFullPath((Join-Path $artifactRoot $relative))
    if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target escapes the intended artifact directory: $target"
    }
    $ancestor = $target
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing a reparse point in target ancestry: $ancestor"
            }
        }
        $ancestor = Split-Path $ancestor -Parent
    }
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $item = Get-Item -LiteralPath $target -Force
    $children = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $target -Recurse -Force) } else { @($item) }
    if ($children | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw "Refusing target containing a junction or symlink: $target"
    }
    [pscustomobject]@{
        Path = $target
        Bytes = [long](($children | Where-Object { -not $_.PSIsContainer } | Measure-Object Length -Sum).Sum)
        Directory = $item.PSIsContainer
    }
}
$targets | Format-Table Path, Bytes -AutoSize
$total = ($targets | Measure-Object Bytes -Sum).Sum
Write-Output ("Eligible intermediates: {0}; size: {1:N1} MiB" -f @($targets).Count, ($total / 1MB))
if (-not $Apply) {
    Write-Output 'Preview only. Review the fixed list, then run with -Apply to delete it.'
    return
}
foreach ($target in $targets) {
    if ($PSCmdlet.ShouldProcess($target.Path, 'Delete obsolete corpus intermediate')) {
        Remove-Item -LiteralPath $target.Path -Recurse:$target.Directory -Force
    }
}
