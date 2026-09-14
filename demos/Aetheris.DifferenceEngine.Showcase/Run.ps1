[CmdletBinding()]
param([string]$Output='artifacts/local/demos/difference-engine-showcase',[string]$Spec,[switch]$NoServe,[int]$Port=8776)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repo
try {
 $argsList=@($Output)
 if($Spec){$argsList+=@($Spec,'fixtures/DifferenceEngine')}
 dotnet run --project "$PSScriptRoot/Aetheris.DifferenceEngine.Showcase.csproj" -c Release -- @argsList
 if($LASTEXITCODE -ne 0){throw 'CAD generation failed'}
 node "$PSScriptRoot/verify.mjs" $Output
 if($LASTEXITCODE -ne 0){throw 'Presentation qualification failed'}
 & "$PSScriptRoot/Prepare-Viewer.ps1" -Output $Output
 if(!$NoServe){python "$PSScriptRoot/serve.py" "$Output/dist" $Port}
} finally {Pop-Location}
