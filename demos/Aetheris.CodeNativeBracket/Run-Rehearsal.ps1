param(
    [string]$OutputDirectory = 'artifacts/local/demos/code-native-bracket',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repository
try {
    & dotnet build Aetheris.CLI -c Release -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
    & dotnet build demos/Aetheris.CodeNativeBracket -c Release -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Bracket runner build failed.' }
    & dotnet demos/Aetheris.CodeNativeBracket/bin/Release/net10.0/Aetheris.CodeNativeBracket.dll $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Artifact generation failed.' }
    foreach ($case in @('nominal', 'variant')) {
        $caseDirectory = Join-Path $OutputDirectory $case
        & $Python fixtures/Regression/ComposeHoleFamilies/reconstruct_bracket_pdf.py `
            --pdf (Join-Path $caseDirectory 'Bracket.pdf') --reference-step (Join-Path $caseDirectory 'Bracket.step') `
            --out-dir (Join-Path $caseDirectory 'pdf-reconstruction') --cli Aetheris.CLI/bin/Release/net10.0/aetheris.dll
        if ($LASTEXITCODE -ne 0) { throw "PDF-only reconstruction failed: $case" }
        & dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate (Join-Path $caseDirectory 'bracket.firmament') --json
        if ($LASTEXITCODE -ne 0) { throw "Admission failed: $case" }
    }
}
finally { Pop-Location }
