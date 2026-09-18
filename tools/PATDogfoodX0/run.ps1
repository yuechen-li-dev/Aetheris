param(
    [ValidateSet('all', 'prepare', 'demo', 'benchmark')]
    [string]$Phase = 'all'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$local = Join-Path $repo 'artifacts\local\pat-x0'
$upstream = Join-Path $local 'upstream'
$python = Join-Path $local 'venv\Scripts\python.exe'
$revision = '253bf9fc5e79831a03f0486e0c42feeb67c1c546'
$modelHash = 'AAA2F3882C36AC32D57361377A73AC830C47D03D6167D51249127A6C40EF7FF9'

function Prepare-Upstream {
    New-Item -ItemType Directory -Force $local | Out-Null
    if (-not (Test-Path (Join-Path $upstream '.git'))) {
        git clone --recursive https://github.com/nzfeng/points-as-tori.git $upstream
    }
    git -C $upstream checkout --detach $revision
    git -C $upstream submodule update --init --recursive

    $actualRevision = (git -C $upstream rev-parse HEAD).Trim()
    if ($actualRevision -ne $revision) { throw "Unexpected PAT revision $actualRevision" }
    $weight = Join-Path $upstream 'src\pointsastori\models\FundamentalFormPredictor.pkl'
    $actualHash = (Get-FileHash $weight -Algorithm SHA256).Hash
    if ($actualHash -ne $modelHash) { throw "Unexpected model hash $actualHash" }

    # Upstream commit 253bf9f needs two source-only MSVC compatibility fixes.
    $shape2d = Join-Path $upstream 'src\cpp\shape_2d.cpp'
    $shape3d = Join-Path $upstream 'src\cpp\shape_3d.cpp'
    $shape2dText = Get-Content $shape2d -Raw
    if ($shape2dText -notmatch '#include <numeric>') {
        $shape2dText = $shape2dText.Replace('#include <cstring>', "#include <cstring>`r`n#include <numeric>")
        [System.IO.File]::WriteAllText($shape2d, $shape2dText)
    }
    $shape3dText = Get-Content $shape3d -Raw
    $shape3dText = $shape3dText.Replace(
        'for (size_t i = 0; i < points.size(); ++i)',
        'for (int i = 0; i < static_cast<int>(points.size()); ++i)')
    [System.IO.File]::WriteAllText($shape3d, $shape3dText)

    if (-not (Test-Path $python)) { py -3.11 -m venv (Join-Path $local 'venv') }
    $installStamp = Join-Path $local 'installed-revision.txt'
    $expectedStamp = "$revision-msvc-compat-v1"
    $actualStamp = if (Test-Path $installStamp) { (Get-Content $installStamp -Raw).Trim() } else { '' }
    if ($actualStamp -ne $expectedStamp) {
        & $python -m pip install --upgrade pip
        & $python -m pip install $upstream
        if ($LASTEXITCODE -ne 0) { throw 'PAT package build/install failed' }
        Set-Content -LiteralPath $installStamp -Value $expectedStamp
    }
    & $python -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt')
}

function Export-Fixtures {
    $meshDir = Join-Path $local 'aetheris-meshes'
    New-Item -ItemType Directory -Force $meshDir | Out-Null
    $fixtures = [ordered]@{
        sphere = 'fixtures/Canonical/Primitives/sphere.firmament'
        cylinder = 'fixtures/Canonical/Basics/cylinder.firmament'
        torus = 'fixtures/Canonical/Primitives/torus.firmament'
        frustum = 'fixtures/Canonical/Primitives/frustum.firmament'
        slab = 'fixtures/Experiments/PATDogfoodX0/slab.firmament'
        # The isolated finite-edge fillet fixture currently fails SurfaceMeshIR
        # with a zero-area cell; the canonical all-edge RoundedBox is the usable
        # exact filleted-box witness for this benchmark.
        filleted_box = 'fixtures/Canonical/Primitives/rounded-box.firmament'
        rounded_rectangle_prism = 'fixtures/Experiments/PATDogfoodX0/rounded-rectangle-prism.firmament'
        sharp_box = 'fixtures/Canonical/Basics/box.firmament'
        chamfered_box = 'fixtures/Canonical/Features/EdgeFinish/semantic-selection-chamfer.firmament'
        through_hole = 'fixtures/Canonical/Features/Holes/through-hole.firmament'
        counterbore = 'fixtures/Canonical/Features/Holes/counterbore.firmament'
        thin_plate = 'fixtures/Experiments/PATDogfoodX0/thin-plate.firmament'
        phone_like_chassis = 'fixtures/Experiments/PATDogfoodX0/phone-like-chassis.firmament'
    }
    foreach ($entry in $fixtures.GetEnumerator()) {
        $out = Join-Path $meshDir ($entry.Key + '.obj')
        $source = Join-Path $repo $entry.Value
        $implicitStep = [System.IO.Path]::ChangeExtension($source, '.step')
        $implicitStepExisted = Test-Path $implicitStep
        dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- mesh $source --format obj --output $out --json
        if ($LASTEXITCODE -ne 0) { throw "Aetheris mesh export failed for $($entry.Key)" }
        # `aetheris mesh` currently materializes an intermediate STEP beside a
        # Firmament source. Keep generated benchmark output under artifacts/local.
        if (-not $implicitStepExisted -and (Test-Path $implicitStep)) {
            Remove-Item -LiteralPath $implicitStep
        }
    }

    # Exercise the real Sheet Metal compiler and a template-specialized
    # ProfileDelta program. The ordinary mesh command cannot consume authored
    # SheetMetal syntax directly, so materialize its authoritative formed STEP
    # first and then tessellate that exact BRep.
    $sheetDir = Join-Path $local 'sheetmetal'
    New-Item -ItemType Directory -Force $sheetDir | Out-Null
    $sheetSource = Join-Path $repo 'fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament'
    $formedStep = Join-Path $sheetDir 'profile-delta-tab-formed.step'
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- build $sheetSource --output $formedStep --json
    if ($LASTEXITCODE -ne 0) { throw 'Authored Sheet Metal formed build failed' }
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- sheetmetal inspect $sheetSource --json
    if ($LASTEXITCODE -ne 0) { throw 'Authored Sheet Metal inspection failed' }
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- mesh $formedStep --format obj --output (Join-Path $meshDir 'bent_sheet.obj') --json
    if ($LASTEXITCODE -ne 0) { throw 'Authored Sheet Metal formed mesh failed' }
}

Set-Location $repo
if ($Phase -in @('all', 'prepare')) {
    Prepare-Upstream
    Export-Fixtures
}
if ($Phase -in @('all', 'demo')) {
    & $python (Join-Path $PSScriptRoot 'pat_x0.py') demo --local-root $local
}
if ($Phase -in @('all', 'benchmark')) {
    & $python (Join-Path $PSScriptRoot 'pat_x0.py') benchmark --local-root $local
}
