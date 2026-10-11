"""Generate reproducible sewing-pattern witnesses using an external GarmentCode checkout.

This calls upstream templates, not Aetheris simulation. Install pattern-only dependencies
in a research venv; no Python dependency is introduced into the Aetheris runtime.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--upstream", type=Path, required=True)
parser.add_argument("--out-dir", type=Path, required=True)
args = parser.parse_args()
upstream = args.upstream.resolve()
sys.path.insert(0, str(upstream))

import yaml
import numpy as np
from assets.bodies.body_params import BodyParameters
from assets.garment_programs.bodice import Shirt

revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=upstream, text=True).strip()
expected = "d449629979028123a5c4dc9e732a2ec19b7fce31"
if revision != expected:
    raise RuntimeError(f"Expected GarmentCode {expected}; found {revision}")

body_path = upstream / "assets/bodies/mean_all.yaml"
design_path = upstream / "assets/design_params/t-shirt.yaml"
base_design = yaml.safe_load(design_path.read_text())["design"]
output = args.out_dir.resolve()
output.mkdir(parents=True, exist_ok=True)
records = []
for name, length, sleeveless in [("shirt", 1.2, False), ("dress", 2.2, True)]:
    body = BodyParameters(str(body_path))
    design = copy.deepcopy(base_design)
    design["shirt"]["length"]["v"] = length
    design["sleeve"]["sleeveless"]["v"] = sleeveless
    garment = Shirt(body, design)
    if garment.is_self_intersecting():
        raise RuntimeError(f"{name}: upstream reported a self-intersecting pattern")
    pattern = garment.assembly()
    pattern.name = name
    # Upstream discovers components through a Python set. Canonicalize presentation order
    # without changing panel geometry, seam endpoints or arrangement.
    pattern.pattern["panels"] = dict(sorted(pattern.pattern["panels"].items()))
    pattern.pattern["stitches"].sort(key=lambda pair: json.dumps(pair, sort_keys=True))
    pattern.pattern["panel_order"] = sorted(pattern.pattern["panels"])
    directory = output / name
    directory.mkdir(exist_ok=True)
    # Use upstream JSON serialization. Disable printable PDF (not needed for this witness).
    pattern.serialize(str(directory), to_subfolder=False, with_3d=False,
                      with_text=True, view_ids=True, with_printable=False)
    oracle = []
    for panel_name, panel in pattern.pattern["panels"].items():
        # Exercise upstream's own curve reader; its SVG path flips Y for display.
        svg_vertices = np.asarray(panel["vertices"]) * [1, -1]
        for index, edge in enumerate(panel["edges"]):
            curve = pattern._edge_as_curve(svg_vertices, edge)
            oracle.append({
                "panel": panel_name,
                "edge": index,
                "lengthMm": curve.length() * 10,
                "samplesMm": [[curve.point(t).real * 10, -curve.point(t).imag * 10]
                              for t in [0, .25, .5, .75, 1]],
            })
    (directory / "curve-oracle.json").write_text(json.dumps(oracle, indent=2) + "\n")
    (directory / "body-measurements.yaml").write_text(body_path.read_text())
    (directory / "design.yaml").write_text(yaml.safe_dump({"design": design}, sort_keys=False))
    records.append({
        "name": name,
        "panels": len(pattern.pattern["panels"]),
        "stitches": len(pattern.pattern["stitches"]),
        "lengthMultiplier": length,
        "sleeveless": sleeveless,
        "sourceSha256": hashlib.sha256((directory / f"{name}_specification.json").read_bytes()).hexdigest(),
    })

(output / "provenance.json").write_text(json.dumps({
    "upstream": "https://github.com/maria-korosteleva/GarmentCode",
    "revision": revision,
    "template": "assets/garment_programs/bodice.py:Shirt (fitted=False)",
    "bodyMeasurements": "assets/bodies/mean_all.yaml",
    "designPreset": "assets/design_params/t-shirt.yaml",
    "license": "MIT; copyright (c) 2024 Maria Korosteleva",
    "bodyMeshCopied": False,
    "simulated": False,
    "witnesses": records,
}, indent=2) + "\n")
print(json.dumps(records, indent=2))
