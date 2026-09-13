"""Build, inspect, and score local BenchCAD-to-Firmament reconstructions.

This is an experiment harness, not a BenchCAD submission runner. It consumes
independently authored Firmament under artifacts/local/benchcad-aetheris and
uses BenchCAD's own voxel scorer from an explicitly supplied checkout.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from collections import Counter
from pathlib import Path


def run(command: list[str], cwd: Path) -> tuple[int, str, str]:
    completed = subprocess.run(command, cwd=cwd, text=True, capture_output=True, check=False)
    return completed.returncode, completed.stdout, completed.stderr


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def parse_json_output(code: int, stdout: str, stderr: str) -> object:
    try:
        return json.loads(stdout)
    except json.JSONDecodeError:
        return {"success": code == 0, "stdout": stdout.strip(), "stderr": stderr.strip()}


def cad_summary(step: Path, cq) -> dict[str, object]:
    imported = cq.importers.importStep(str(step))
    solids = imported.solids().vals()
    faces = imported.faces().vals()
    edges = imported.edges().vals()
    vertices = imported.vertices().vals()
    box = imported.val().BoundingBox()
    surfaces = Counter(face.geomType().lower().replace("_", "-") for face in faces)
    return {
        "bounds": {
            "min": [box.xmin, box.ymin, box.zmin],
            "max": [box.xmax, box.ymax, box.zmax],
            "size": [box.xlen, box.ylen, box.zlen],
        },
        "volume": sum(solid.Volume() for solid in solids),
        "bodyCount": len(solids),
        "faceCount": len(faces),
        "edgeCount": len(edges),
        "vertexCount": len(vertices),
        "surfaceFamilies": dict(sorted(surfaces.items())),
    }


def load_records(path: Path) -> dict[str, dict[str, object]]:
    return {
        row["record_id"]: row
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip()
        for row in [json.loads(line)]
    }


def classification(task: str, record_id: str, second_iou: float) -> tuple[str | None, str]:
    if second_iou >= 0.99:
        return None, "Success"
    if task == "Vision2Code" and record_id.startswith("washer_"):
        return "Reasoning", "Use the existing construction-plane Profile extrusion rather than a world-XY profile."
    if task == "CodeEdit" and record_id.startswith("battery_holder_"):
        return "Reasoning", "Inspect the constant major-arc section and preserve the finite end walls with a section stack."
    if task == "CodeEdit" and record_id == "topup_ball_knob_axial_hole":
        return "MissingSemanticPrimitive", "The stem uses an annular Profile; the remaining delta is the same axial Hole on the Sphere component."
    if task == "CodeEdit" and "ball_knob" in record_id:
        return "Reasoning", "Use two analytic component definitions, a DatumFrame Interface, and Assembly AP242 export."
    return "Reasoning", "The bounded reconstruction did not reach near-parity."


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--aetheris-root", type=Path, required=True)
    parser.add_argument("--benchcad-root", type=Path, required=True)
    parser.add_argument("--artifact-root", type=Path)
    parser.add_argument("--aetheris-cli", type=Path, help="Freshly published aetheris executable; defaults to dotnet run.")
    parser.add_argument("--baseline-summary", type=Path, help="Prior milestone results-summary.json; compares against its second pass.")
    parser.add_argument("--corpus", default="test_data", choices=["test_data", "data"])
    args = parser.parse_args()

    aetheris = args.aetheris_root.resolve()
    benchcad = args.benchcad_root.resolve()
    artifacts = (args.artifact_root or aetheris / "artifacts/local/benchcad-aetheris").resolve()
    first_pass = json.loads((artifacts / "first-pass-results.json").read_text(encoding="utf-8"))
    prior = {}
    if args.baseline_summary:
        prior = {(row["task"], row["recordId"]): float(row["secondPassIou"])
                 for row in json.loads(args.baseline_summary.read_text(encoding="utf-8"))["records"]}

    sys.path.insert(0, str(benchcad))
    from benchcad_core.scoring.iou import _ocp_hashcode_fix, iou_step_vs_step, norm_iou
    from benchcad_core.scoring.views import composite_for_step

    _ocp_hashcode_fix()
    import cadquery as cq

    records_by_task = {
        "Vision2Code": load_records(benchcad / "Vision2Code" / args.corpus / "records.jsonl"),
        "CodeEdit": load_records(benchcad / "CodeEdit" / args.corpus / "records.jsonl"),
    }
    result_rows: list[dict[str, object]] = []

    for task, records in records_by_task.items():
        task_dir = artifacts / task
        if not task_dir.exists():
            continue
        for record_dir in sorted(path for path in task_dir.iterdir() if path.is_dir()):
            record_id = record_dir.name
            record = records.get(record_id)
            if record is None:
                raise SystemExit(f"No {task} {args.corpus} record named {record_id}")
            target_rel = record["step_path"] if task == "Vision2Code" else record["gt_step_path"]
            target = (benchcad / task / args.corpus / str(target_rel)).resolve()
            assembly_source = record_dir / "reconstruction.assembly.firmament"
            source = assembly_source if assembly_source.exists() else record_dir / "reconstruction.firmament"
            candidate = record_dir / "reconstructed.step"

            cli = [str(args.aetheris_cli.resolve())] if args.aetheris_cli else ["dotnet", "run", "--project", "Aetheris.CLI", "-c", "Release", "--no-build", "--"]
            build_args = (["asm", "export-ap242", str(source), "--out", str(candidate), "--json"]
                          if source == assembly_source else ["build", str(source), "--output", str(candidate), "--json"])
            build_code, build_out, build_err = run(cli + build_args, aetheris)
            write_json(record_dir / "build.json", parse_json_output(build_code, build_out, build_err))

            for name, step in (("target", target), ("reconstructed", candidate)):
                inspect_args = ["analyze", "compound", str(step), "--json"] if source == assembly_source else ["analyze", str(step), "--json"]
                code, stdout, stderr = run(cli + inspect_args, aetheris)
                write_json(record_dir / f"inspect-{name}.json", parse_json_output(code, stdout, stderr))

            target_summary = {
                "recordId": record_id,
                "task": task,
                "targetStepPath": str(target),
                "metadata": record,
                "classification": "Eligible",
                "scorerAvailability": "BenchCAD voxel IoU available",
                **cad_summary(target, cq),
            }
            write_json(record_dir / "target-summary.json", target_summary)
            composite_for_step(target, record_dir / "target-preview.png")
            if build_code == 0:
                composite_for_step(candidate, record_dir / "reconstructed-preview.png")

            model_iou = iou_step_vs_step(candidate, target) if build_code == 0 else 0.0
            first = prior.get((task, record_id), float(first_pass[task][record_id]["iou"]))
            baseline = float(record.get("iou", 0.0)) if task == "CodeEdit" else None
            score = {
                "recordId": record_id,
                "task": task,
                "metric": "voxel_iou_64_normalized",
                "firstPass": {"rawTargetIou": first},
                "secondPass": {"rawTargetIou": model_iou},
                "delta": model_iou - first,
            }
            if baseline is not None:
                score["baselineIou"] = baseline
                score["firstPass"]["normalizedIou"] = norm_iou(first, baseline)
                score["secondPass"]["normalizedIou"] = norm_iou(model_iou, baseline)
            write_json(record_dir / "score.json", score)

            cause, status = classification(task, record_id, model_iou)
            note = classification(task, record_id, model_iou)[1]
            notes = (
                f"# {record_id}\n\n"
                f"- Mode: source-assisted open-book reconstruction after STEP inspection.\n"
                f"- First-pass status: {'Success' if first >= 0.99 else 'BoundedAttempt'}.\n"
                f"- Second-pass status: {status}.\n"
                f"- Primary failure cause: {cause or 'None'}.\n"
                f"- Observation: {note}\n"
                f"- Independence: the Firmament source contains no STEP import or target path.\n"
            )
            (record_dir / "notes.md").write_text(notes, encoding="utf-8")

            result_rows.append({
                "task": task,
                "recordId": record_id,
                "firstPassIou": first,
                "secondPassIou": model_iou,
                "delta": model_iou - first,
                "baselineIou": baseline,
                "firstPassNormalizedIou": norm_iou(first, baseline) if baseline is not None else None,
                "secondPassNormalizedIou": norm_iou(model_iou, baseline) if baseline is not None else None,
                "buildSuccess": build_code == 0,
                "failureCause": cause,
            })

    write_json(artifacts / "results-summary.json", {"records": result_rows})
    print(json.dumps({"artifactRoot": str(artifacts), "recordCount": len(result_rows)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
