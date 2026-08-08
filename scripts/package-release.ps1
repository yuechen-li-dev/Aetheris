[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$stage = Join-Path $output 'stage'
Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $output 'packages') | Out-Null

Push-Location (Join-Path $repoRoot 'aetheris.client')
try {
    npm ci
    npm test
    npm run build
}
finally { Pop-Location }

dotnet publish (Join-Path $repoRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -o (Join-Path $stage 'Aetheris-win-x64')
dotnet publish (Join-Path $repoRoot 'Aetheris.Server/Aetheris.Server.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -o (Join-Path $stage 'Aetheris-win-x64/cadmata')
dotnet pack (Join-Path $repoRoot 'Aetheris.CLI/Aetheris.CLI.csproj') -c Release --no-build -p:Version=$Version -o (Join-Path $output 'packages')
Get-ChildItem -LiteralPath $stage -Recurse -Filter '*.pdb' -File | Remove-Item -Force

$vsix = Join-Path $repoRoot 'artifacts/vscode/aetheris-firmament-0.1.0-preview.1.vsix'
if (-not (Test-Path -LiteralPath $vsix)) { throw "Validated VSIX not found: $vsix" }
Copy-Item -LiteralPath $vsix -Destination $output

$zip = Join-Path $output "Aetheris-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $stage 'Aetheris-win-x64') -DestinationPath $zip -CompressionLevel Optimal
$releaseFiles = @($zip, (Join-Path $output 'aetheris-firmament-0.1.0-preview.1.vsix')) + (Get-ChildItem -LiteralPath (Join-Path $output 'packages') -Filter '*.nupkg' -File | Select-Object -ExpandProperty FullName)
$releaseFiles | Get-FileHash -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), $_.Path.Substring($output.Length + 1) } | Set-Content (Join-Path $output 'SHA256SUMS.txt')
