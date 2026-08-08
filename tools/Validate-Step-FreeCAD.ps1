param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$StepFile
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $StepFile)) {
    Write-Error "STEP file not found: $StepFile"
    exit 2
}

$freeCadCmd = Get-Command FreeCADCmd.exe, FreeCADCmd, freecadcmd -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $freeCadCmd) {
    $knownInstall = Get-ChildItem -Path (Join-Path $env:ProgramFiles 'FreeCAD*\\bin\\FreeCADCmd.exe') -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($knownInstall) {
        $freeCadCmd = [pscustomobject]@{ Source = $knownInstall.FullName }
    }
}
if (-not $freeCadCmd) {
    Write-Host "Skipped: FreeCADCmd was not found on PATH. Install FreeCAD or add FreeCADCmd to PATH to run external STEP import validation."
    exit 0
}

$freeCadPython = Join-Path (Split-Path -Parent $freeCadCmd.Source) 'python.exe'
if (-not (Test-Path -LiteralPath $freeCadPython)) {
    Write-Error "FreeCAD Python runtime was not found beside $($freeCadCmd.Source)."
    exit 3
}

$tempScript = [System.IO.Path]::GetTempFileName() + '.py'
$resolvedStepFile = (Resolve-Path -LiteralPath $StepFile).Path
@'
import sys
import FreeCAD
import Import

step_file = r'__STEP_FILE__'
doc = FreeCAD.newDocument('StepImportValidation')
try:
    Import.insert(step_file, doc.Name)
    objects = list(doc.Objects)
    if not objects:
        raise RuntimeError('STEP import produced zero document objects')

    valid = True
    invalid = []
    solid_count = 0
    shell_count = 0
    closed = True
    surface_types = {}
    for obj in objects:
        shape = getattr(obj, 'Shape', None)
        if shape is not None and hasattr(shape, 'isValid'):
            ok = bool(shape.isValid())
            valid = valid and ok
            if not ok:
                invalid.append(getattr(obj, 'Name', '<unnamed>'))
            solid_count += len(getattr(shape, 'Solids', []))
            shell_count += len(getattr(shape, 'Shells', []))
            closed = closed and bool(shape.isClosed())
            for face in getattr(shape, 'Faces', []):
                surface = getattr(face, 'Surface', None)
                name = getattr(surface, '__class__', type(surface)).__name__
                surface_types[name] = surface_types.get(name, 0) + 1
    print('FreeCAD STEP import succeeded')
    print('object_count={}'.format(len(objects)))
    print('solid_count={}'.format(solid_count))
    print('shell_count={}'.format(shell_count))
    print('closed={}'.format(str(closed).lower()))
    print('shape_valid={}'.format(str(valid).lower()))
    print('healing_invoked=false')
    print('surface_types={}'.format(','.join('{}:{}'.format(k, surface_types[k]) for k in sorted(surface_types))))
    if invalid:
        print('invalid_objects={}'.format(','.join(invalid)))
    sys.exit(0 if valid else 3)
except Exception as exc:
    print('FreeCAD STEP import failed: {}'.format(exc), file=sys.stderr)
    sys.exit(1)
finally:
    try:
        FreeCAD.closeDocument(doc.Name)
    except Exception:
        pass
'@.Replace('__STEP_FILE__', $resolvedStepFile) | Set-Content -LiteralPath $tempScript -Encoding UTF8

try {
    # FreeCADCmd opens a .py file as a document on FreeCAD 1.0 rather than executing it.
    # Its bundled Python is the supported headless API entry point for this smoke.
    & $freeCadPython $tempScript
    exit $LASTEXITCODE
}
finally {
    Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue
}
