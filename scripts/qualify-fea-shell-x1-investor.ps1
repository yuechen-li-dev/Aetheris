param([string]$OutputDirectory = "artifacts/local/fea-shell-x1/investor-bracket")
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$output = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$nativeSource = Get-Content -LiteralPath (Join-Path $repo "fixtures/InvestorPitch/sheet-metal-bracket.firmament") -Raw
$analysis = @'

Analysis LinearElastic NativeInvestorBracketServiceLoad {
    Body: InvestorBracket
    Mode: ExperimentalShell
    MasterGrid: [2, 2]
    Order: 2
    MaxSubdivisionDepth: 4
    Fixed BaseFront { Region: InvestorBracket.BasePlate.s-min }
    Force UprightServiceLoad {
        Region: InvestorBracket.Upright.s-max
        Vector: [0N, -250N, 0N]
    }
    Results: [Displacement, Strain, Stress, ReactionForce, StrainEnergy]
}
'@
$casePath = Join-Path $output "native-investor-bracket-analysis.firmament"
Set-Content -LiteralPath $casePath -Value ($nativeSource + $analysis) -NoNewline
$raw = & dotnet run --project (Join-Path $repo "Aetheris.CLI") -c Release --no-build -- fea $casePath --out-dir $output --json 2>&1 | Out-String
$exitCode = $LASTEXITCODE
Set-Content -LiteralPath (Join-Path $output "investor-attempt.json") -Value $raw.TrimEnd()
$raw.TrimEnd()
exit $exitCode
