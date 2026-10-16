[CmdletBinding()]
param([string]$Output = '', [string]$Spec = '', [switch]$NoServe)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Output) { $Output = Join-Path $repo 'artifacts/local/demos/editable-v8' }
$Output = [IO.Path]::GetFullPath($Output)
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
try {
    $env:MSBUILDDISABLENODEREUSE = '1'
    & dotnet build "$PSScriptRoot/Aetheris.EditableV8.csproj" -c Release -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE) { throw 'Engine build failed.' }
} finally { $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse }
$arguments = @("$PSScriptRoot/bin/Release/net10.0/Aetheris.EditableV8.dll", $Output)
if ($Spec) { $arguments += [IO.Path]::GetFullPath($Spec) }
& dotnet @arguments
if ($LASTEXITCODE) { throw 'Engine generation failed.' }
if (!(Test-Path -LiteralPath "$repo/aetheris.client/node_modules/three/build/three.module.js")) {
    & npm ci --prefix "$repo/aetheris.client"
    if ($LASTEXITCODE) { throw 'Viewer dependency installation failed.' }
}
& "$PSScriptRoot/Prepare-Viewer.ps1" -Output $Output
& node "$PSScriptRoot/Verify-Browser.mjs" $Output
if ($LASTEXITCODE) { throw 'Browser evaluator parity failed.' }
if (!$NoServe) {
    Write-Host 'Open http://127.0.0.1:8765/ in your browser. Ctrl+C stops this foreground preview.'
    & python "$PSScriptRoot/serve.py" $Output
}
