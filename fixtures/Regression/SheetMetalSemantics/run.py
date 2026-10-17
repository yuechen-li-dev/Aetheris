"""Real CLI admission and AP242 qualification; never records replacement goldens."""
import json
from pathlib import Path
import subprocess


def main():
    corpus = Path(__file__).resolve().parent
    repo = corpus.parents[2]
    output = repo / "artifacts/local/sheetmetal-semantics/cli"
    output.mkdir(parents=True, exist_ok=True)
    cli = repo / "Aetheris.CLI/bin/Release/net10.0/aetheris.dll"
    rejected = {
        "layout-unknown-field", "layout-unknown-feature-field", "layout-modern-xz",
        "layout-unknown-statement", "layout-false-require", "layout-contract-missing",
        "layout-contract-semicolon", "layout-extra-unused-pattern", "manufacturing-unknown-pmi",
        "manufacturing-unknown-field", "manufacturing-negative-tolerance",
        "manufacturing-zero-quantity", "manufacturing-unknown-kind", "manufacturing-second-pmi",
        "module-unknown-statement",
    }

    def invoke(*args):
        process = subprocess.run(["dotnet", str(cli), *map(str, args), "--json"], cwd=repo,
                                 capture_output=True, text=True, encoding="utf-8", timeout=120)
        try:
            return process.returncode, json.loads(process.stdout)
        except ValueError as error:
            raise RuntimeError(process.stdout + process.stderr) from error

    rows = []
    for source in sorted(corpus.glob("*.firmament")):
        name = source.stem
        expected = name not in rejected
        step = output / (name + ".step")
        validation_code, validation = invoke("validate", source)
        build_code, build = invoke("build", source, "--output", step)
        inspect_code, inspection = invoke("sheetmetal", "inspect", source)
        passed = (validation_code == 0) == (build_code == 0) == (inspect_code == 0) == expected
        evidence = dict(validation=validation, build=build, inspection=inspection)
        if not expected:
            diagnostics = validation.get("sheetMetalValidation", {}).get("diagnostics", [])
            passed &= bool(diagnostics) and all(isinstance(d, dict) and d.get("code") and
                                               d.get("message") and d.get("severity") for d in diagnostics)
            passed &= any(d.get("sourceFile") and d.get("sourceSpan") for d in diagnostics)
            passed &= build.get("success") is False and inspection.get("success") is False
        else:
            analysis_code, analysis = invoke("analyze", step)
            evidence["analysis"] = analysis
            passed &= analysis_code == 0 and build.get("success") is True
            items = analysis.get("semanticPmi", {}).get("items", [])
            if name.startswith("manufacturing-"):
                passed &= len(items) == 13
            if name == "modern-layout-release":
                passed &= len(items) == 5
                passed &= any(i.get("kind") == "Diameter" and i.get("value") == 6 and
                              i.get("toleranceMinus") == 0.05 and len(i.get("geometricFaceEntityIds", [])) == 2 for i in items)
            if name == "keyed-features":
                passed &= any(i.get("target") == "mounting.left" and i.get("value") == 6 for i in items)
            if name == "keyed-profile-cuts":
                passed &= any(i.get("target") == "vents.left" and i.get("text") for i in items)
            if name == "layout-suffix-capture":
                passed &= inspection["sheetMetal"]["semanticLayout"]["patterns"][0]["centerX"] == 50
        (output / (name + ".json")).write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
        rows.append(dict(name=name, expected=expected, passed=bool(passed)))
        print(("PASS " if passed else "FAIL ") + name, flush=True)
    (output / "results.json").write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    print(f"{sum(row['passed'] for row in rows)}/{len(rows)} cases")
    return 0 if all(row["passed"] for row in rows) else 1


if __name__ == "__main__":
    raise SystemExit(main())
