param(
    [ValidateSet('all', 'prepare', 'demo', 'benchmark', 'wedge', 'analyze')]
    [string]$Phase = 'all'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$local = Join-Path $repo 'artifacts\local\dcsd-x0'
$upstream = Join-Path $local 'upstream'
$python = Join-Path $local 'venv\Scripts\python.exe'
$revision = 'fa1962cd5825cfc9bb698569714f579e1d75a4e8'

function Replace-Once([string]$Path, [string]$Old, [string]$New) {
    $text = Get-Content -LiteralPath $Path -Raw
    if ($text.Contains($New)) { return }
    if (-not $text.Contains($Old)) { throw "Expected patch context missing in $Path" }
    [System.IO.File]::WriteAllText($Path, $text.Replace($Old, $New))
}

function Prepare-Upstream {
    New-Item -ItemType Directory -Force $local | Out-Null
    if (-not (Test-Path (Join-Path $upstream '.git'))) {
        git -c core.longpaths=true clone --recursive https://github.com/xianacarrera/dual-contouring-of-signed-distance-data.git $upstream
    }
    git -C $upstream -c core.longpaths=true checkout --detach $revision
    git -C $upstream -c core.longpaths=true submodule update --init --recursive
    if ((git -C $upstream rev-parse HEAD).Trim() -ne $revision) { throw 'Unexpected upstream revision' }

    # Narrow Windows/headless compatibility patch. These edits do not alter the
    # objective, weights, iteration counts, Hermite update, or connectivity.
    Replace-Once (Join-Path $upstream 'include\contouring.h') "#pragma once`n" "#pragma once`n#include <vector>`n"
    Replace-Once (Join-Path $upstream 'include\ui.h') "#pragma once`n" "#pragma once`n#include <vector>`n"
    $cmake = Join-Path $upstream 'CMakeLists.txt'
    Replace-Once $cmake "project(contouring)`n" "project(contouring)`n`nfind_package(OpenMP)`n"
    Replace-Once $cmake 'list(REMOVE_ITEM CONTOURING_SRC_FILES "${CMAKE_CURRENT_SOURCE_DIR}/src/cpp/bindings.cpp")' "list(REMOVE_ITEM CONTOURING_SRC_FILES `"`${CMAKE_CURRENT_SOURCE_DIR}/src/cpp/bindings.cpp`")`n# sample_fun.cpp contains an obsolete second contouring() definition which wins on MSVC.`nlist(REMOVE_ITEM CONTOURING_SRC_FILES `"`${CMAKE_CURRENT_SOURCE_DIR}/src/cpp/sample_fun.cpp`")"
    Replace-Once $cmake "  polyscope`n)`n" "  polyscope`n)`nif(OpenMP_CXX_FOUND)`n  target_link_libraries(contouring PUBLIC OpenMP::OpenMP_CXX)`nendif()`n"
    Replace-Once (Join-Path $upstream 'src\cpp\ui.cpp') "    for (Cell& c : cells) {" "    for (int cell_index = 0; cell_index < static_cast<int>(cells.size()); ++cell_index) {`n        Cell& c = cells[static_cast<size_t>(cell_index)];"
    Replace-Once (Join-Path $upstream 'src\cpp\hermite_update.cpp') "    for (size_t ei = 0; ei < edge_cells.size(); ++ei) {`n        Eigen::Vector4i cell_indices = edge_cells[ei];" "    for (int ei = 0; ei < static_cast<int>(edge_cells.size()); ++ei) {`n        Eigen::Vector4i cell_indices = edge_cells[static_cast<size_t>(ei)];"
    Replace-Once (Join-Path $upstream 'src\cpp\contouring.cpp') "            for (size_t i = 0; i < cells.size(); ++i) {`n                Cell& c = cells[i];" "            for (int i = 0; i < static_cast<int>(cells.size()); ++i) {`n                Cell& c = cells[static_cast<size_t>(i)];"

    if (-not (Test-Path $python)) { py -3.11 -m venv (Join-Path $local 'venv') }
    & $python -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt') pybind11==3.0.1 cmake==4.4.3 ninja
    if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed' }

    $build = Join-Path $upstream 'build-win'
    $pythonAbsolute = (Resolve-Path $python).Path
    $cmakeExe = Join-Path $local 'venv\Scripts\cmake.exe'
    & 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\Launch-VsDevShell.ps1' -Arch amd64 -HostArch amd64 -NoLogo
    # The upstream OpenMP annotations compile after the loop-index portability
    # fixes but make the 32^3 demo exceed 3 minutes (serial: about 37 s).
    # Keep the faithful deterministic serial path for X0.
    & $cmakeExe -S $upstream -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release -DPython3_EXECUTABLE=$pythonAbsolute -DCMAKE_DISABLE_FIND_PACKAGE_OpenMP=TRUE
    if ($LASTEXITCODE -ne 0) { throw 'DCSD CMake configuration failed' }
    & $cmakeExe --build $build -j 12
    if ($LASTEXITCODE -ne 0) { throw 'DCSD native build failed' }
}

function Export-Fixtures {
    $meshDir = Join-Path $local 'aetheris-meshes'
    New-Item -ItemType Directory -Force $meshDir | Out-Null
    $fixtures = [ordered]@{
        sphere = 'fixtures/Canonical/Primitives/sphere.firmament'
        cylinder = 'fixtures/Canonical/Basics/cylinder.firmament'
        torus = 'fixtures/Canonical/Primitives/torus.firmament'
        frustum = 'fixtures/Canonical/Primitives/frustum.firmament'
        filleted_box = 'fixtures/Canonical/Primitives/rounded-box.firmament'
        rounded_rectangle_prism = 'fixtures/Experiments/PATDogfoodX0/rounded-rectangle-prism.firmament'
        sharp_box = 'fixtures/Canonical/Basics/box.firmament'
        sharp_wedge_prism = 'fixtures/Experiments/DCSDDogfoodX0/sharp-wedge-prism.firmament'
        chamfered_box = 'fixtures/Canonical/Features/EdgeFinish/semantic-selection-chamfer.firmament'
        through_hole = 'fixtures/Canonical/Features/Holes/through-hole.firmament'
        counterbore = 'fixtures/Canonical/Features/Holes/counterbore.firmament'
        thin_plate = 'fixtures/Experiments/PATDogfoodX0/thin-plate.firmament'
        phone_like_chassis = 'fixtures/Experiments/PATDogfoodX0/phone-like-chassis.firmament'
    }
    foreach ($entry in $fixtures.GetEnumerator()) {
        $source = Join-Path $repo $entry.Value
        $out = Join-Path $meshDir ($entry.Key + '.obj')
        $implicitStep = [System.IO.Path]::ChangeExtension($source, '.step')
        $existed = Test-Path $implicitStep
        dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- mesh $source --format obj --output $out --json
        if ($LASTEXITCODE -ne 0) { throw "Aetheris export failed for $($entry.Key)" }
        if (-not $existed -and (Test-Path $implicitStep)) { Remove-Item -LiteralPath $implicitStep }
    }

    # Exercise the authored SheetMetal ProfileDelta route, not a hand-built bend.
    $sheetDir = Join-Path $local 'sheetmetal'
    New-Item -ItemType Directory -Force $sheetDir | Out-Null
    $source = Join-Path $repo 'fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament'
    $step = Join-Path $sheetDir 'profile-delta-tab-formed.step'
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- build $source --output $step --json
    if ($LASTEXITCODE -ne 0) { throw 'SheetMetal ProfileDelta build failed' }
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- sheetmetal inspect $source --json
    if ($LASTEXITCODE -ne 0) { throw 'SheetMetal ProfileDelta inspection failed' }
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- mesh $step --format obj --output (Join-Path $meshDir 'bent_sheet.obj') --json
    if ($LASTEXITCODE -ne 0) { throw 'SheetMetal formed mesh export failed' }
}

function Export-WedgeFixture {
    $meshDir = Join-Path $local 'aetheris-meshes'
    New-Item -ItemType Directory -Force $meshDir | Out-Null
    $source = Join-Path $repo 'fixtures/Experiments/DCSDDogfoodX0/sharp-wedge-prism.firmament'
    $out = Join-Path $meshDir 'sharp_wedge_prism.obj'
    $implicitStep = [System.IO.Path]::ChangeExtension($source, '.step')
    $existed = Test-Path $implicitStep
    dotnet run --project (Join-Path $repo 'Aetheris.CLI') -- mesh $source --format obj --output $out --json
    if ($LASTEXITCODE -ne 0) { throw 'Aetheris export failed for sharp_wedge_prism' }
    if (-not $existed -and (Test-Path $implicitStep)) { Remove-Item -LiteralPath $implicitStep }
}

Set-Location $repo
if ($Phase -in @('all', 'prepare')) { Prepare-Upstream; Export-Fixtures }
if ($Phase -eq 'wedge') { Prepare-Upstream; Export-WedgeFixture }
if ($Phase -in @('all', 'demo')) { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') demo --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD demo failed' } }
if ($Phase -in @('all', 'benchmark')) { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') benchmark --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD benchmark failed' } }
if ($Phase -eq 'wedge') { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') wedge --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD wedge failed' } }
if ($Phase -in @('all', 'benchmark')) { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') thin-sweep --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD thin sweep failed' } }
if ($Phase -in @('all', 'benchmark')) { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') open-surface --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD open-surface test failed' } }
if ($Phase -in @('all', 'benchmark')) { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') analyze --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD feature analysis failed' } }
if ($Phase -in @('all', 'benchmark')) { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') visual-evidence --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD visual evidence failed' } }
if ($Phase -eq 'analyze') { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') analyze --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD feature analysis failed' } }
if ($Phase -eq 'analyze') { & $python (Join-Path $PSScriptRoot 'dcsd_x0.py') visual-evidence --local-root $local; if ($LASTEXITCODE -ne 0) { throw 'DCSD visual evidence failed' } }
