"""Qualify shared Profile authoring through the repository CLI, without recording goldens."""
import hashlib
import json
from pathlib import Path
import re
import subprocess


def main():
    root = Path(__file__).resolve().parent
    repo = root.parents[2]
    output = repo / "artifacts/local/sheetmetal-syntax/cli"
    output.mkdir(parents=True, exist_ok=True)
    cli = repo / "Aetheris.CLI/bin/Release/net10.0/aetheris.dll"

    def invoke(*args):
        result = subprocess.run(["dotnet", str(cli), *map(str, args), "--json"], cwd=repo,
                                capture_output=True, text=True, timeout=120)
        try:
            payload = json.loads(result.stdout)
        except ValueError:
            raise RuntimeError(result.stdout + result.stderr)
        return result.returncode, payload

    cases = [(p, True) for p in sorted((repo / "fixtures/Canonical/SheetMetal").glob("*.firmament"))]
    cases += [(p, p.stem != "unsupported-xz") for p in sorted(root.glob("*.firmament"))]
    for file, needle, name in [("docs/public/firmament/syntax.md", "Profile outline = Rect2", "doc-mechanical"),
                               ("docs/public/firmament/sheet-metal.md", "Profile ventilation = Slot2", "doc-sheetmetal")]:
        blocks = re.findall(r"```firmament\s*\n(.*?)```", (repo / file).read_text(encoding="utf-8"), re.S)
        source = next(b for b in blocks if needle in b)
        path = output / (name + ".firmament")
        path.write_text(source, encoding="utf-8")
        cases.append((path, True))

    rows = []
    for index, (source, expected) in enumerate(cases):
        label = source.stem
        step = output / (str(index) + "-" + label + ".step")
        validation_code, validation = invoke("validate", source)
        build_code, build = invoke("build", source, "--output", step)
        validated = validation_code == 0
        built = build_code == 0 and build.get("success") is True
        row = dict(source=str(source.relative_to(repo)), name=label, expected=expected,
                   validated=validated, built=built, passed=validated == built == expected)
        if built:
            row["stepSha256"] = hashlib.sha256(step.read_bytes()).hexdigest()
        if built and (build.get("domain") == "SheetMetal"):
            flat_code, flat = invoke("sheetmetal", "flatten", source, "--step", output / (str(index) + "-flat.step"))
            row["passed"] &= flat_code == 0 and flat.get("success") is True
            flat_pattern = flat.get("flatPattern", {})
            if "materialArea" not in flat_pattern or "bounds" not in flat_pattern:
                raise RuntimeError("Flat qualification omitted its metrics: " + str(flat))
            row["flat"] = {key: flat_pattern[key] for key in ("materialArea", "bounds")}
            build["flatQualification"] = flat
        (output / (str(index) + "-" + label + ".json")).write_text(
            json.dumps(dict(validation=validation, build=build), indent=2) + "\n", encoding="utf-8")
        rows.append(row)
        print(("PASS " if row["passed"] else "FAIL ") + label, flush=True)
    by_name = {row["name"]: row for row in rows}
    modern, legacy = by_name["modern-bracket"], by_name["legacy-bracket"]
    parity = modern.get("stepSha256") == legacy.get("stepSha256") and modern.get("flat") == legacy.get("flat")
    result = dict(cases=rows, formedStepAndFlatParity=parity)
    (output / "results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"{sum(row['passed'] for row in rows)}/{len(rows)} cases; formed STEP / flat parity: {parity}")
    return 0 if parity and all(row["passed"] for row in rows) else 1


if __name__ == "__main__":
    raise SystemExit(main())
