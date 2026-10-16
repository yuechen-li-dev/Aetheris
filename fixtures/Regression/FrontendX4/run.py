#!/usr/bin/env python3
"""FrontendX4 corpus runner.

Every case is run through both `aetheris validate --json` and `aetheris build`.

- A case expected `valid` must validate, build, and match its geometry. A case with a
  `reference` must match the reference file built by the same CLI. Every valid case must
  also match the numbers recorded in expected.json.
- A case expected `invalid` must fail both commands. Its `code` must be among the build
  diagnostics, reported at `line`.
- The chimera must match chimera.baseline.firmament.

Usage:
  python fixtures/Regression/FrontendX4/run.py [--cli PATH] [--only a03,c04] [--record]

--record measures the references and guards with the current CLI and rewrites the
geometry recorded in expected.json. Use it only when a geometry change is intended and
explained.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
VOLUME_REL = 1e-9
ARTIFACTS = ROOT / "artifacts" / "local" / "frontend-x4"


def scratch(prefix: str) -> Path:
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    return Path(tempfile.mkdtemp(prefix=prefix, dir=ARTIFACTS))


def default_cli() -> Path:
    exe = "aetheris.exe" if os.name == "nt" else "aetheris"
    return ROOT / "Aetheris.CLI" / "bin" / "Release" / "net10.0" / exe


def run(cli: Path, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run([str(cli), *args], capture_output=True, text=True, cwd=HERE)


def validate_status(cli: Path, source: Path) -> str:
    p = run(cli, "validate", str(source), "--json")
    try:
        doc = json.loads(p.stdout)
    except json.JSONDecodeError:
        return "unparseable-json"
    return str(doc.get("firmamentV2Validation", doc).get("status"))


def build(cli: Path, source: Path, out: Path) -> tuple[bool, str]:
    p = run(cli, "build", str(source), "--output", str(out))
    return p.returncode == 0 and out.exists(), p.stdout + p.stderr


def geometry(cli: Path, step: Path) -> dict:
    s = json.loads(run(cli, "inspect", str(step), "--json").stdout)["summary"]
    evidence = scratch("verify-")
    v = json.loads(run(cli, "verify", str(step), "--json", "--evidence-dir", str(evidence)).stdout)
    bb = s["boundingBox"]
    return {
        "faces": s["faceCount"],
        "edges": s["edgeCount"],
        "vertices": s["vertexCount"],
        "bbox": [[bb["min"][k] for k in "xyz"], [bb["max"][k] for k in "xyz"]],
        "assessment": s["structuralAssessment"],
        "surfaces": {k: n for k, n in s["surfaceFamilies"].items() if n},
        "volume": v["brepMassProperties"]["absoluteVolume"],
    }


def same_geometry(a: dict, b: dict) -> list[str]:
    diffs = [f"{k}: {a[k]} != {b[k]}" for k in ("faces", "edges", "vertices", "assessment", "surfaces") if a[k] != b[k]]
    for i in range(2):
        for j in range(3):
            if abs(a["bbox"][i][j] - b["bbox"][i][j]) > 1e-9:
                diffs.append(f"bbox: {a['bbox']} != {b['bbox']}")
                break
    if abs(a["volume"] - b["volume"]) > VOLUME_REL * max(1.0, abs(b["volume"])):
        diffs.append(f"volume: {a['volume']!r} != {b['volume']!r}")
    return diffs


def diagnostic_lines(output: str, name: str, code: str) -> list[int]:
    pattern = re.compile(rf"{re.escape(name)}\((\d+),(\d+)\): fatal ([^:\s]+)")
    return [int(m.group(1)) for m in pattern.finditer(output) if m.group(3).startswith(code)]


def check_case(cli: Path, case: dict, tmp: Path, record: bool) -> tuple[bool, str]:
    source = HERE / case["file"]
    status = validate_status(cli, source)
    built, output = build(cli, source, tmp / (case["id"] + ".step"))
    if case["expect"] == "invalid":
        problems = []
        if status != "invalid":
            problems.append(f"validate={status}")
        if built:
            problems.append("build succeeded")
        lines = diagnostic_lines(output, source.name, case["code"])
        if not lines:
            problems.append(f"no {case['code']} diagnostic")
        elif case.get("line") and case["line"] not in lines:
            problems.append(f"{case['code']} at line(s) {lines}, expected {case['line']}")
        return not problems, "; ".join(problems) or "rejected as required"
    if record and case.get("reference"):
        rb, rout = build(cli, HERE / case["reference"], tmp / (case["id"] + ".ref.step"))
        if not rb:
            return False, "reference does not build: " + rout.strip().splitlines()[-1]
        case["geometry"] = geometry(cli, tmp / (case["id"] + ".ref.step"))
    elif record and case["kind"] == "guard" and built:
        case["geometry"] = geometry(cli, tmp / (case["id"] + ".step"))
    if status != "valid" or not built:
        first = next((l for l in output.splitlines() if "fatal" in l or "Error" in l), output.strip()[-200:])
        first = first.replace(str(source.parent) + os.sep, "")
        return False, f"validate={status}, build={'ok' if built else 'failed'}: {first.strip()[:220]}"
    got = geometry(cli, tmp / (case["id"] + ".step"))
    diffs = []
    if case.get("reference") and not record:
        rb, _ = build(cli, HERE / case["reference"], tmp / (case["id"] + ".ref.step"))
        if not rb:
            return False, "reference does not build"
        diffs += ["vs reference " + d for d in same_geometry(got, geometry(cli, tmp / (case["id"] + ".ref.step")))]
    if case.get("geometry"):
        diffs += ["vs recorded " + d for d in same_geometry(got, case["geometry"])]
    return not diffs, "; ".join(diffs) or "builds the expected geometry"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cli", type=Path, default=default_cli())
    ap.add_argument("--only", default="")
    ap.add_argument("--record", action="store_true")
    args = ap.parse_args()
    manifest_path = HERE / "expected.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    only = {x.strip() for x in args.only.split(",") if x.strip()}
    tmp = scratch("run-")
    failures = 0
    rows = []
    for case in manifest["cases"]:
        if only and case["id"] not in only:
            continue
        ok, why = check_case(args.cli, case, tmp, args.record)
        failures += not ok
        rows.append((case["id"], case["kind"], case["register"], "PASS" if ok else "FAIL", why))
    if not only or "chimera" in only:
        ch = manifest["chimera"]
        status = validate_status(args.cli, HERE / ch["file"])
        built, out = build(args.cli, HERE / ch["file"], tmp / "chimera.step")
        rb, rout = build(args.cli, HERE / ch["reference"], tmp / "chimera.ref.step")
        if not rb:
            ok, why = False, "chimera.baseline.firmament no longer builds: " + rout.strip().splitlines()[-1]
        else:
            ref = geometry(args.cli, tmp / "chimera.ref.step")
            if args.record:
                ch["geometry"] = ref
            base_diffs = same_geometry(ref, ch["geometry"]) if ch.get("geometry") else []
            if base_diffs:
                ok, why = False, "baseline drifted from recorded: " + "; ".join(base_diffs)
            elif status != "valid" or not built:
                first = next((l for l in out.splitlines() if "fatal" in l or "Error" in l), out.strip()[-200:])
                first = first.replace(str(HERE) + os.sep, "")
                ok, why = False, f"validate={status}: {first.strip()[:220]}"
            else:
                d = same_geometry(geometry(args.cli, tmp / "chimera.step"), ref)
                ok, why = not d, "; ".join(d) or "matches the baseline"
        failures += not ok
        rows.append(("chimera", "target", "all", "PASS" if ok else "FAIL", why))
    if args.record:
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    width = max(len(r[2]) for r in rows)
    for r in rows:
        print(f"{r[0]:8} {r[1]:10} {r[2]:{width}}  {r[3]}  {r[4]}")
    print(f"\n{len(rows) - failures}/{len(rows)} pass")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
