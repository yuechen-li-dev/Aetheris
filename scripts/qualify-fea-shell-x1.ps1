param(
    [string]$OutputDirectory = "artifacts/local/fea-shell-x1/conditioning"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$output = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$sourcePath = Join-Path $repo "fixtures/Canonical/FEA/ExperimentalShell/flat-cantilever.firmament"
$source = Get-Content -LiteralPath $sourcePath -Raw
New-Item -ItemType Directory -Force -Path $output | Out-Null

function Invoke-Case([string]$name,[string]$caseSource,[string[]]$arguments) {
    $casePath = Join-Path $output "$name.firmament"
    Set-Content -LiteralPath $casePath -Value $caseSource -NoNewline
    $raw = & dotnet run --project (Join-Path $repo "Aetheris.CLI") -c Release --no-build -- fea $casePath --json @arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    Set-Content -LiteralPath (Join-Path $output "$name.json") -Value $raw.TrimEnd()
    try { $report = $raw | ConvertFrom-Json } catch { throw "Case '$name' did not return JSON: $raw" }
    $reference = $null
    $referenceError = $null
    if ($name -match '^lt(?<ratio>\d+)-') {
        $ratio = [double]$Matches.ratio
        $thickness = 0.1 / $ratio
        $reference = 10.0 * [Math]::Pow(0.1,3) / (3.0 * 200e9 * (0.02 * [Math]::Pow($thickness,3) / 12.0))
        if ($null -ne $report.maximumDisplacementMeters) { $referenceError = [Math]::Abs($report.maximumDisplacementMeters - $reference) / $reference }
    }
    [pscustomobject]@{
        name = $name
        success = if ($null -ne $report.success) { [bool]$report.success } else { $exitCode -eq 0 }
        iterations = $report.solver.iterations
        finalSolverResidual = $report.solver.finalResidual
        finalPhysicalResidual = $report.experimentalShell.solver.finalPhysicalResidual
        preconditioner = $report.experimentalShell.solver.preconditioner
        equilibration = $report.experimentalShell.solver.symmetricDiagonalEquilibration
        diagonalShift = $report.experimentalShell.solver.diagonalShift
        factorizationRetries = $report.experimentalShell.solver.factorizationRetries
        minimumScale = $report.experimentalShell.solver.minimumScale
        maximumScale = $report.experimentalShell.solver.maximumScale
        degreesOfFreedom = $report.system.degreesOfFreedom
        minimumDiagonal = $report.system.minimumDiagonal
        maximumDiagonal = $report.system.maximumDiagonal
        diagonalRatio = $report.system.diagonalRatio
        minimumRowNorm = $report.system.minimumRowNorm
        maximumRowNorm = $report.system.maximumRowNorm
        rowNormRatio = $report.system.rowNormRatio
        displacementMeters = $report.maximumDisplacementMeters
        referenceDisplacementMeters = $reference
        relativeReferenceError = $referenceError
        equilibriumResidualNewton = $report.equilibrium.residualNewton.length
        energyJoule = $report.strainEnergy.algebraicJoule
        diagnosticCodes = @($report.diagnostics | ForEach-Object { $_.code } | Where-Object { $null -ne $_ })
    }
}

$policies = @(
    @{ Name = "identity"; Arguments = @("--experimental-preconditioner","identity","--experimental-equilibration","false") },
    @{ Name = "jacobi"; Arguments = @("--experimental-preconditioner","jacobi","--experimental-equilibration","false") },
    @{ Name = "equil"; Arguments = @("--experimental-preconditioner","identity","--experimental-equilibration","true") },
    @{ Name = "equil-jacobi"; Arguments = @("--experimental-preconditioner","jacobi","--experimental-equilibration","true") },
    @{ Name = "equil-block-jacobi"; Arguments = @("--experimental-preconditioner","block-jacobi","--experimental-equilibration","true") },
    @{ Name = "equil-ic0"; Arguments = @("--experimental-preconditioner","ic0","--experimental-equilibration","true") }
)
$matrix = @()
foreach ($ratio in @(50,100,200,500)) {
    $thicknessMillimeters = 100.0 / $ratio
    $token = $thicknessMillimeters.ToString("0.############",[System.Globalization.CultureInfo]::InvariantCulture) + "mm"
    $caseSource = $source.Replace("2mm]",$token + "]").Replace("Thickness: 2mm","Thickness: $token")
    foreach ($policy in $policies) {
        $matrix += Invoke-Case "lt$ratio-$($policy.Name)" $caseSource $policy.Arguments
    }
}

$pStudy = @()
$thinSource = $source.Replace("2mm]","1mm]").Replace("Thickness: 2mm","Thickness: 1mm")
foreach ($order in 2..6) {
    $caseSource = $thinSource.Replace("Order: 3","Order: $order")
    $pStudy += Invoke-Case "p$order-lt100-equil-ic0" $caseSource @("--experimental-preconditioner","ic0","--experimental-equilibration","true")
}

$evidence = [ordered]@{
    schema = "fea-shell-x1-conditioning-v1"
    source = "flat-cantilever.firmament with only semantic thickness/body-thickness and order substitutions"
    fixedMasterGrid = @(4,1)
    maximumIterations = 10000
    matrix = $matrix
    pStudy = $pStudy
}
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output "conditioning-matrix.json")
$evidence | ConvertTo-Json -Depth 8
