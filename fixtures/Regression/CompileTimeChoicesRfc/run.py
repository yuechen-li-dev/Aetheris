"""Evaluate the choices RFC against the production CLI; known RFC gaps fail.

No fixture is rewritten and no geometry reference is recorded automatically.
Run from any directory after building Aetheris.CLI in Release.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def main():
    root = Path(__file__).resolve().parent
    repo = root.parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=repo / "artifacts/local/compile-time-choices")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    cli = repo / "Aetheris.CLI/bin/Release/net10.0/aetheris.dll"

    def invoke(*arguments):
        result = subprocess.run(["dotnet", str(cli), *map(str, arguments), "--json"],
                                cwd=repo, capture_output=True, text=True, timeout=90)
        try:
            payload = json.loads(result.stdout)
        except ValueError as error:
            raise RuntimeError(f"CLI did not return JSON: {result.stdout}\n{result.stderr}") from error
        return result.returncode, payload

    cases = json.loads((root / "cases.json").read_text(encoding="utf-8"))
    results = []
    for case in cases:
        name = case["name"]
        source = root / (name + ".firmament")
        step = output / (name + ".step")
        validation_code, validation = invoke("validate", source)
        build_code, build = invoke("build", source, "--output", step)
        admitted = build_code == 0 and build.get("success") is True
        validated = validation_code == 0 and validation.get("firmamentV2Validation", {}).get("status") == "valid"
        row = dict(name=name, shouldBuild=case["shouldBuild"], validated=validated,
                   built=admitted, agreement=validated == admitted,
                   meetsRfc=admitted == case["shouldBuild"], reference=case["reference"])
        expected_code = case.get("diagnostic")
        if expected_code:
            def contains_code(payload):
                return any(isinstance(d, dict) and
                           (d.get("code") == expected_code or
                            str(d.get("code", "")).startswith(expected_code + ":"))
                           for d in payload.get("diagnostics", []))
            row["diagnosticMatches"] = (contains_code(build) and
                contains_code(validation.get("firmamentV2Validation", {})))
            row["meetsRfc"] &= row["diagnosticMatches"]
            row["canonicalDiagnosticCodes"] = all(
                d["code"] == expected_code
                for payload in (build, validation.get("firmamentV2Validation", {}))
                for d in payload.get("diagnostics", [])
                if isinstance(d, dict) and str(d.get("code", "")).startswith(expected_code))
        if admitted:
            analysis_code, analysis = invoke("analyze", step)
            if analysis_code != 0:
                raise RuntimeError(f"STEP analysis failed for {name}: {analysis}")
            summary = analysis["summary"]
            row["geometry"] = {key: summary[key] for key in
                               ("bodyCount", "shellCount", "faceCount", "edgeCount", "vertexCount", "boundingBox", "surfaceFamilies")}
            row["stepSha256"] = hashlib.sha256(step.read_bytes()).hexdigest()
        (output / (name + ".json")).write_text(json.dumps(dict(validation=validation, build=build), indent=2) + "\n")
        results.append(row)

    by_name = {row["name"]: row for row in results}
    for row in results:
        if row["built"] and row["reference"]:
            reference = by_name[row["reference"]]
            row["geometryMatchesReference"] = row["geometry"] == reference.get("geometry")
            row["stepMatchesReference"] = row["stepSha256"] == reference.get("stepSha256")
            row["meetsRfc"] &= row["geometryMatchesReference"]
        label = "PASS" if row["meetsRfc"] and row["agreement"] else "GAP"
        print(f"{label:4} {row['name']:30} validate={row['validated']} build={row['built']}")
    (output / "results.json").write_text(json.dumps(results, indent=2) + "\n")
    passed = sum(row["meetsRfc"] and row["agreement"] for row in results)
    print(f"RFC requirements met: {passed}/{len(results)}; evidence: {output}")
    return 0 if passed == len(results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
