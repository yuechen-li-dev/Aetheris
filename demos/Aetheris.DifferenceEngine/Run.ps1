[CmdletBinding()]
param([string]$Output='', [switch]$Packaged)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$Output){$Output=Join-Path $repo 'artifacts/local/demos/difference-engine'}
$Output=[IO.Path]::GetFullPath($Output)
function Checked([scriptblock]$Command){ & $Command; if($LASTEXITCODE){throw "Command failed ($LASTEXITCODE): $Command"} }
Checked { dotnet run --project "$PSScriptRoot/Aetheris.DifferenceEngine.csproj" -c Release -- $Output "$repo/fixtures/DifferenceEngine" }
Checked { dotnet test "$repo/Aetheris.Kernel.Core.Tests" -c Release --filter 'FullyQualifiedName~IndexedRotaryStorageTests' --nologo -v q }
Checked { dotnet test "$repo/Aetheris.Kernel.Firmament.Tests" -c Release --filter 'FullyQualifiedName~DifferenceEngineStorageGateTests' --nologo -v q }
Checked { dotnet build "$repo/Aetheris.CLI" -c Release --nologo -v q }
$cli="$repo/Aetheris.CLI/bin/Release/net10.0/aetheris.dll"
Checked { dotnet $cli asm inspect "$Output/source/StorageGate.firmament" --json > "$Output/cli-inspection.json" }
Checked { dotnet $cli asm export-ap242 "$Output/source/StorageGate.firmament" --out "$Output/cli-storage.step" --json > "$Output/cli-export.json" }
Checked { dotnet $cli asm import-step "$Output/cli-storage.step" --out "$Output/reimport" --json > "$Output/cli-reimport.json" }
if((Get-FileHash "$Output/storage-gate.step").Hash -ne (Get-FileHash "$Output/cli-storage.step").Hash){throw 'CLI/demo STEP parity failed'}
Checked { dotnet $cli mesh "$Output/source/StorageGate.firmament" --format assembly-json --output "$Output/cli-storage.mesh.json" --json > "$Output/cli-mesh.json" }
if((Get-FileHash "$Output/storage-gate.mesh.json").Hash -ne (Get-FileHash "$Output/cli-storage.mesh.json").Hash){throw 'CLI/demo mesh parity failed'}
# Use this import's manifest, never a wildcard that can select stale component files.
# Component STEP is inspected here; it is never fed back into assembly construction.
$meshDocument=Get-Content "$Output/storage-gate.mesh.json" -Raw|ConvertFrom-Json
$plate=@($meshDocument.definitions|Where-Object {$_.identity.StartsWith('DecimalIndexPlate<')})
if($plate.Count -ne 1){throw 'Expected one indexing plate definition'}
$componentPackage=Get-Content "$Output/reimport/component-package.json" -Raw|ConvertFrom-Json
$plateComponent=@($componentPackage.components|Where-Object {$_.DefinitionStableId -eq $plate[0].id})
if($plateComponent.Count -ne 1){throw 'Indexing plate missing from current STEP import manifest'}
$plateStep=Join-Path "$Output/reimport" $plateComponent[0].RelativePath
if((Get-FileHash $plateStep).Hash -ne $plateComponent[0].Sha256){throw 'Indexing plate component hash mismatch'}
Checked { dotnet $cli mesh $plateStep --format obj --output "$Output/index-plate.obj" --debug-ir "$Output/index-plate.surface-mesh.json" --json > "$Output/index-plate.obj-report.json" }
$objReport=Get-Content "$Output/index-plate.obj-report.json" -Raw|ConvertFrom-Json
if(!$objReport.watertight -or !$objReport.connected -or !$objReport.outwardOriented -or $objReport.crackCount -or $objReport.nonManifoldEdgeCount -or $objReport.duplicateTriangleCount -or $objReport.zeroAreaTriangleCount){throw 'OBJ plate topology qualification failed'}
if($Packaged){
    # A fresh tool installation and source copy, explicitly requested for qualification.
    # It is deliberately retained for inspection; this script deletes no temp/user files.
    $outside=Join-Path ([IO.Path]::GetTempPath()) ('aetheris-diff-engine-x0-'+[guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($outside) | Out-Null
    Copy-Item -LiteralPath "$Output/source" -Destination "$outside/source" -Recurse
    Copy-Item -LiteralPath "$repo/fixtures/Canonical/AssemblyInterfaces/gear-with-ordinary-part.firmament" -Destination "$outside/mixed.firmament"
    $version='2.0.0-diffengine-x0.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    Checked { dotnet pack "$repo/Aetheris.CLI" -c Release -p:Version=$version -o "$Output/packages" --nologo -v q }
    Checked { dotnet tool install Aetheris.CLI --version $version --add-source "$Output/packages" --tool-path "$outside/tool" --no-cache }
    Push-Location $outside
    try {
        Checked { & "$outside/tool/aetheris.exe" asm export-ap242 source/StorageGate.firmament --out "$Output/packaged-storage.step" --json > "$Output/packaged-export.json" }
        Checked { & "$outside/tool/aetheris.exe" asm export-ap242 mixed.firmament --out "$Output/packaged-mixed.step" --json > "$Output/packaged-mixed-export.json" }
        Checked { & "$outside/tool/aetheris.exe" asm import-step "$Output/packaged-storage.step" --out "$Output/packaged-reimport" --json > "$Output/packaged-reimport.json" }
        Checked { & "$outside/tool/aetheris.exe" mesh source/StorageGate.firmament --format assembly-json --output "$Output/packaged-storage.mesh.json" --json > "$Output/packaged-mesh.json" }
        Checked { & "$outside/tool/aetheris.exe" mesh $plateStep --format obj --output "$outside/index-plate.obj" --json > "$Output/packaged-obj-report.json" }
    } finally { Pop-Location }
    if((Get-FileHash "$Output/storage-gate.step").Hash -ne (Get-FileHash "$Output/packaged-storage.step").Hash){throw 'Packaged STEP parity failed'}
    if((Get-FileHash "$Output/storage-gate.mesh.json").Hash -ne (Get-FileHash "$Output/packaged-storage.mesh.json").Hash){throw 'Packaged mesh parity failed'}
    if((Get-FileHash "$Output/index-plate.obj").Hash -ne (Get-FileHash "$outside/index-plate.obj").Hash){throw 'Packaged OBJ parity failed'}
    @{version=$version;outsideDirectory=$outside;storageStepSha256=(Get-FileHash "$Output/packaged-storage.step").Hash}|ConvertTo-Json|Set-Content "$Output/packaged-run.json"
}
Write-Host 'Storage gate artifacts and focused qualification complete. The adder, carry chain and full Difference Engine are not yet qualified.'
