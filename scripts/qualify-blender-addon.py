"""Install the packaged extension in an isolated profile and exercise the real Blender/CLI path."""
import argparse
import json
import math
import os
from pathlib import Path
import subprocess


def main():
    repo = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blender", type=Path, required=True)
    parser.add_argument("--package", type=Path, default=repo / "artifacts/local/blender-addon/aetheris_cad-0.1.0-windows-x64.zip")
    parser.add_argument("--output", type=Path, default=repo / "artifacts/local/blender-addon/package-qualification")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    profile = output / "profile"
    env = dict(os.environ, BLENDER_USER_RESOURCES=str(profile))
    blender = str(args.blender.resolve())

    def run(name, arguments):
        with (output / (name + ".log")).open("w", encoding="utf-8") as log:
            subprocess.run([blender, "--background", "--python-exit-code", "1", *map(str, arguments)], env=env,
                           cwd=output, stdout=log, stderr=subprocess.STDOUT, check=True)

    run("repo", ["--command", "extension", "repo-add", "aetheris_test", "--name", "Aetheris qualification",
                 "--directory", profile / "extensions/aetheris_test", "--clear-all"])
    run("manifest", ["--command", "extension", "validate", args.package.resolve()])
    run("install", ["--command", "extension", "install-file", args.package.resolve(), "--repo", "aetheris_test", "--enable"])
    script = repo / "test-support/blender-addon/qualify.py"
    run("create", ["--python", script, "--", "--repo", repo, "--output", output, "--phase", "create"])
    run("reopen", [output / "qualified.blend", "--python", script, "--", "--repo", repo, "--output", output, "--phase", "reopen"])
    create = json.loads((output / "create.json").read_text())
    reopened = json.loads((output / "reopen.json").read_text())
    assert create["success"] and reopened["success"]
    assert not create["compiler_override"] and not reopened["compiler_override"], "Qualification must use the bundled compiler"
    compiler = profile / "extensions/aetheris_test/aetheris_cad/runtime/aetheris.exe"
    volume = json.loads(subprocess.check_output([str(compiler), "analyze", "volume", str(output / "reopened.step"), "--json"], text=True))
    assert volume["success"] and volume["exact"]
    expected = 100 * 50 * 8 - math.pi * 4 * 4 * 8
    assert abs(volume["volume"] - expected) < 1e-8, volume
    assert volume["boundingBox"]["min"] == {"x": -50, "y": -25, "z": 0}
    assert volume["boundingBox"]["max"] == {"x": 50, "y": 25, "z": 8}
    (output / "exact-step-analysis.json").write_text(json.dumps(volume, indent=2))
    report = {"success": True, "package": str(args.package.resolve()), "create": create, "reopen": reopened,
              "exact_volume_mm3": volume["volume"], "expected_volume_mm3": expected}
    (output / "qualification.json").write_text(json.dumps(report, indent=2))
    print(json.dumps({"success": True, "report": str(output / "qualification.json"), "blend": str(output / "qualified.blend")}, indent=2))


if __name__ == "__main__":
    main()
