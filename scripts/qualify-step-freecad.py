"""Independent OCCT STEP qualification inside FreeCADCmd.

Set AETHERIS_STEP_INPUT and AETHERIS_STEP_REPORT to explicit paths. Uses the
normal native importer, without custom healing or geometry replacement.
Generated reports belong in artifacts/local.
"""
import json
import hashlib
import os
import time

import Part
import FreeCAD
import Import

started = time.perf_counter()
document = FreeCAD.newDocument("StepQualification")
errors = []
shape = Part.Shape()
try:
    Import.insert(os.environ["AETHERIS_STEP_INPUT"], document.Name)
    shapes = [obj.Shape for obj in document.Objects if hasattr(obj, "Shape") and not obj.Shape.isNull()]
    shape = Part.makeCompound(shapes) if shapes else Part.Shape()
except Exception as error:
    errors.append(str(error))
elapsed = time.perf_counter() - started
if shape.isNull() and not errors:
    errors.append("Native importer produced no non-null shape.")
try:
    shape.check()
except Exception as error:
    errors.append(str(error))
box = None if shape.isNull() else shape.BoundBox
report = {
    "input": os.environ["AETHERIS_STEP_INPUT"],
    "importSeconds": elapsed,
    "valid": not errors and not shape.isNull() and shape.isValid(),
    "closed": not shape.isNull() and shape.isClosed(),
    "null": shape.isNull(),
    "solids": 0 if shape.isNull() else len(shape.Solids),
    "shells": 0 if shape.isNull() else len(shape.Shells),
    "faces": 0 if shape.isNull() else len(shape.Faces),
    "edges": 0 if shape.isNull() else len(shape.Edges),
    "vertices": 0 if shape.isNull() else len(shape.Vertexes),
    "volume": 0 if shape.isNull() else shape.Volume,
    "bounds": None if box is None else [box.XMin, box.YMin, box.ZMin, box.XMax, box.YMax, box.ZMax],
    "errors": errors,
    "freecadVersion": FreeCAD.Version(),
}
with open(os.environ["AETHERIS_STEP_INPUT"], "rb") as stream:
    report["stepSha256"] = hashlib.sha256(stream.read()).hexdigest().upper()
with open(os.environ["AETHERIS_STEP_REPORT"], "w", encoding="utf-8") as stream:
    json.dump(report, stream, indent=2)
print(json.dumps(report))
