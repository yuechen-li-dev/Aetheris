[CmdletBinding()]
param([string]$Output='artifacts/local/demos/difference-engine-showcase')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$Output=[IO.Path]::GetFullPath($Output)
function Checked([scriptblock]$Command){& $Command;if($LASTEXITCODE){throw "Command failed ($LASTEXITCODE)"}}
$outside=Join-Path ([IO.Path]::GetTempPath()) ('aetheris-showcase-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $outside | Out-Null
Copy-Item -LiteralPath "$Output/source" -Destination "$outside/source" -Recurse
$version='2.0.0-showcase.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
Checked {dotnet pack "$repo/Aetheris.CLI" -c Release -p:Version=$version -o "$Output/packages" --nologo -v q}
Checked {dotnet tool install Aetheris.CLI --version $version --add-source "$Output/packages" --tool-path "$outside/tool" --no-cache}
Push-Location $outside
try {
 Checked {& "$outside/tool/aetheris.exe" asm inspect source/DifferenceEngine.firmament --json > "$Output/cli-inspection.json"}
 Checked {& "$outside/tool/aetheris.exe" asm export-ap242 source/DifferenceEngine.firmament --out "$Output/packaged.step" --json > "$Output/packaged-export.json"}
 Checked {& "$outside/tool/aetheris.exe" mesh source/DifferenceEngine.firmament --format assembly-json --output "$Output/packaged.mesh.json" --json > "$Output/packaged-mesh.json"}
 Checked {& "$outside/tool/aetheris.exe" asm import-step "$Output/packaged.step" --out "$Output/packaged-reimport" --json > "$Output/packaged-reimport.json"}
} finally {Pop-Location}
if((Get-FileHash "$Output/difference-engine.step").Hash -ne (Get-FileHash "$Output/packaged.step").Hash){throw 'Packaged STEP mismatch'}
if((Get-FileHash "$Output/machine.mesh.json").Hash -ne (Get-FileHash "$Output/packaged.mesh.json").Hash){throw 'Packaged mesh mismatch'}
@{passed=$true;version=$version;outsideDirectory=$outside;stepSha256=(Get-FileHash "$Output/packaged.step").Hash;meshSha256=(Get-FileHash "$Output/packaged.mesh.json").Hash}|ConvertTo-Json|Set-Content "$Output/packaged-proof.json"
Write-Host 'Fresh packaged CLI outside checkout: STEP and shared meshes match exactly; AP242 import passed.'
