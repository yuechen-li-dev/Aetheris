param([switch]$IncludeSlowCorpus)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$filter = if ($IncludeSlowCorpus) { $null } else { 'Category!=SlowCorpus' }
$projects = @(
    'Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj',
    'Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj',
    'Aetheris.Modules.Tests/Aetheris.Modules.Tests.csproj'
)

Push-Location $repoRoot
try {
    foreach ($project in $projects) {
        $arguments = @('test', $project, '--no-restore', '-m:1', '--verbosity', 'quiet')
        if ($filter) { $arguments += @('--filter', $filter) }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "Kernel iteration test failed: $project (exit $LASTEXITCODE)" }
    }
}
finally {
    Pop-Location
}
