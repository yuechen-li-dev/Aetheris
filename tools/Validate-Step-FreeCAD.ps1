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
    Write-Host "Skipped: FreeCADCmd was not found on PATH. Install FreeCAD or add FreeCADCmd to PATH to run external STEP import validation."
    exit 0
}

$tempScript = [System.IO.Path]::GetTempFileName() + '.py'
@'
import sys
import FreeCAD
import Import

step_file = sys.argv[1]
doc = FreeCAD.newDocument('StepImportValidation')
try:
    Import.open(step_file)
    doc = FreeCAD.ActiveDocument or doc
    objects = list(doc.Objects)
    valid = True
    invalid = []
    for obj in objects:
        shape = getattr(obj, 'Shape', None)
        if shape is not None and hasattr(shape, 'isValid'):
            ok = bool(shape.isValid())
            valid = valid and ok
            if not ok:
                invalid.append(getattr(obj, 'Name', '<unnamed>'))
    print('FreeCAD STEP import succeeded')
    print('object_count={}'.format(len(objects)))
    print('shape_valid={}'.format(str(valid).lower()))
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
'@ | Set-Content -LiteralPath $tempScript -Encoding UTF8

try {
    & $freeCadCmd.Source $tempScript (Resolve-Path -LiteralPath $StepFile).Path
    exit $LASTEXITCODE
}
finally {
    Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue
}
