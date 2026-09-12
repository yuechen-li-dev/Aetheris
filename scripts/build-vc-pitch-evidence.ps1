param([string]$OutputRoot = 'artifacts/local/vc-pitch')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$output = [IO.Path]::GetFullPath($OutputRoot, $repo)
$evidence = Join-Path $output 'evidence'
$engineering = Join-Path $output 'engineering'
New-Item -ItemType Directory -Force $evidence,$engineering | Out-Null
$cli = Join-Path $repo 'Aetheris.CLI/bin/Debug/net10.0/aetheris.dll'
dotnet build Aetheris.CLI -f net10.0 -m:1
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed' }
$commands = [Collections.Generic.List[object]]::new()
function Invoke-Evidence([string]$Name, [string[]]$Arguments, [int]$ExpectedExit = 0) {
    $result = & dotnet $cli @Arguments 2>&1
    $code = $LASTEXITCODE
    $result | Set-Content (Join-Path $evidence "$Name.json") -Encoding utf8
    $commands.Add(@{ name=$Name; executable='dotnet'; arguments=@('Aetheris.CLI/bin/Debug/net10.0/aetheris.dll')+$Arguments; exitCode=$code })
    $commands | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $evidence 'commands.json') -Encoding utf8
    if ($code -ne $ExpectedExit) { throw "$Name failed: $($result -join [Environment]::NewLine)" }
    Write-Host "$Name passed"
}
$sources = Join-Path $repo 'fixtures/InvestorPitch'
$names = @('paperclip-reference','paperclip-longer','paperclip-wider','bolt-reference','bolt-longer','ergonomic-g0','ergonomic-g1','cnc-policy','loaded-plate')
foreach ($name in $names) {
    $source = Join-Path $sources "$name.firmament"
    $step = Join-Path $engineering "$name.step"
    $buildArgs = if ($name.StartsWith('ergonomic')) { @('section-chain','build',$source,'--out',$step,'--json') } else { @('build',$source,'--output',$step,'--json') }
    Invoke-Evidence "$name-build" $buildArgs
    Invoke-Evidence "$name-analyze" @('analyze',$step,'--json')
    Invoke-Evidence "$name-verify" @('verify',$step,'--evidence-dir',(Join-Path $evidence "$name-verification"),'--json')
    $view = if ($name.StartsWith('paperclip')) { 'top' } elseif ($name.StartsWith('ergonomic')) { 'front' } else { 'iso' }
    Invoke-Evidence "$name-wireframe" @('wireframe',$step,'--out',(Join-Path $engineering "$name.svg"),'--view',$view,'--density','4','--json')
    # G1 B-spline surfaces use the exact-support wireframe renderer. The CLI's
    # analytic-only SurfaceMeshIR exporter does not support that surface family.
    if ($name -ne 'ergonomic-g1') {
        Invoke-Evidence "$name-mesh" @('mesh',$step,'--format','obj','--output',(Join-Path $engineering "$name.obj"),'--json')
    }
    $before = (Get-FileHash $step -Algorithm SHA256).Hash
    Invoke-Evidence "$name-repeat" $buildArgs
    if ((Get-FileHash $step -Algorithm SHA256).Hash -ne $before) { throw "$name STEP is not deterministic" }
}
$analysis = (Get-Content (Join-Path $sources 'loaded-plate-analysis.firmament') -Raw).Replace('../../artifacts/local/vc-pitch/engineering/loaded-plate.step', (Join-Path $engineering 'loaded-plate.step').Replace('\','/'))
$analysisPath = Join-Path $engineering 'loaded-plate.analysis.firmament'
Set-Content $analysisPath $analysis -Encoding utf8
Invoke-Evidence 'fea-base' @('fea',$analysisPath,'--out-dir',(Join-Path $engineering 'fea-base'),'--json')
Invoke-Evidence 'fea-fine' @('fea',$analysisPath,'--lattice','24,8,4','--out-dir',(Join-Path $engineering 'fea-fine'),'--json')
Invoke-Evidence 'fea-sanity' @('fea',(Join-Path $sources 'fea-sanity.firmament'),'--out-dir',(Join-Path $engineering 'fea-sanity'),'--json')
Invoke-Evidence 'fea-native' @('fea',(Join-Path $sources 'loaded-plate-native.firmament'),'--out-dir',(Join-Path $engineering 'fea-native'),'--json')
Invoke-Evidence 'cnc-policy-rejected' @('build',(Join-Path $sources 'cnc-policy-rejected.firmament'),'--output',(Join-Path $engineering 'cnc-rejected.step'),'--json') 1
if ((Get-Content (Join-Path $evidence 'cnc-policy-rejected.json') -Raw) -notmatch 'minimum-tool-radius-violation') { throw 'Expected CNC rejection reason missing' }
Get-ChildItem $engineering -File -Recurse | ForEach-Object {
    @{ file=[IO.Path]::GetRelativePath($output,$_.FullName); sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash }
} | ConvertTo-Json | Set-Content (Join-Path $evidence 'artifact-hashes.json') -Encoding utf8
git rev-parse HEAD | Set-Content (Join-Path $evidence 'repository-commit.txt')
dotnet --version | Set-Content (Join-Path $evidence 'dotnet-version.txt')
