"""Build a self-contained Windows Blender extension. Output stays under artifacts/local."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile


def main():
    repo = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=repo / "artifacts/local/blender-addon/aetheris_cad-0.1.0-windows-x64.zip")
    parser.add_argument("--runtime-dir", type=Path, help="Reuse an already published self-contained CLI directory")
    args = parser.parse_args()
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    runtime = args.runtime_dir.resolve() if args.runtime_dir else output.parent / "runtime-win-x64"
    if not args.runtime_dir:
        subprocess.run(["dotnet", "publish", str(repo / "Aetheris.CLI"), "-c", "Release", "-r", "win-x64", "--self-contained", "true",
                        "-p:UseAppHost=true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
                        "-p:UseSharedCompilation=false", "-p:DebugType=None", "-p:DebugSymbols=false",
                        "-m:1", "-nr:false", "-o", str(runtime)], check=True)
    executable = runtime / "aetheris.exe"
    if not executable.is_file():
        raise SystemExit("The runtime directory must contain the published aetheris.exe.")
    version = subprocess.check_output([str(executable), "--version"], text=True).strip()
    source = repo / "tools/blender-aetheris/aetheris_cad"
    files = {p.relative_to(source).as_posix(): p for p in source.rglob("*") if p.is_file() and "__pycache__" not in p.parts and p.suffix != ".pyc"}
    files.update({"runtime/" + p.relative_to(runtime).as_posix(): p for p in runtime.rglob("*") if p.is_file() and p.suffix not in (".pdb", ".xml")})
    files["starter.firmament"] = repo / "fixtures/Canonical/Learn/01-plate.firmament"
    files["LICENSE"] = repo / "LICENSE"
    files["THIRD_PARTY_NOTICES.md"] = repo / "THIRD_PARTY_NOTICES.md"
    inventory = {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in sorted(files.items())}
    metadata = json.dumps({"runtime": version, "platform": "windows-x64", "files": inventory}, indent=2).encode()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name, path in sorted(files.items()):
            info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, path.read_bytes())
        archive.writestr("package-info.json", metadata)
    print(json.dumps({"zip": str(output), "bytes": output.stat().st_size, "sha256": hashlib.sha256(output.read_bytes()).hexdigest(), "runtime": version}, indent=2))


if __name__ == "__main__":
    main()
