"""Transport and display-contract admission only. All engineering belongs to Aetheris."""
import hashlib
import json
import math
from pathlib import Path, PurePosixPath
import subprocess
import tempfile
import time


class BuildError(Exception):
    pass


def relative_path(value):
    value = value.replace("\\", "/")
    if not value or value.startswith("/") or ":" in value or "\0" in value or any(p in ("", ".", "..") for p in value.split("/")):
        raise BuildError("Project files need distinct relative paths without '..', for example parts/profile.firmament.")
    return str(PurePosixPath(value))


def source_hash(files, main_file):
    relative_path(main_file)
    keys = set()
    for name in files:
        clean = relative_path(name)
        if clean.casefold() in keys:
            raise BuildError("Duplicate project path: " + clean)
        keys.add(clean.casefold())
    if main_file not in files:
        raise BuildError("The main Firmament document is missing.")
    if sum(len(data) for data in files.values()) > 64 * 1024 * 1024:
        raise BuildError("This project exceeds the add-on's 64 MiB snapshot limit.")
    manifest = [main_file, [(name, hashlib.sha256(data).hexdigest()) for name, data in sorted(files.items())]]
    return hashlib.sha256(json.dumps(manifest, ensure_ascii=True).encode()).hexdigest()


def validate_display(document):
    if document.get("schema") != "aetheris/display-mesh/1" or document.get("units") != "mm":
        raise BuildError("The compiler returned an unsupported display schema or unit.")
    definitions, occurrences = document.get("definitions", []), document.get("occurrences", [])
    if len(definitions) != 1 or len(occurrences) != 1:
        raise BuildError("This version accepts one standalone BRep part per CAD object.")
    mesh = definitions[0]
    positions, normals, indices = mesh.get("positions", []), mesh.get("normals", []), mesh.get("indices", [])
    if not positions or len(positions) % 3 or len(normals) != len(positions) or not indices or len(indices) % 3:
        raise BuildError("Incomplete display mesh returned by the compiler.")
    if any(not isinstance(v, (int, float)) or not math.isfinite(v) for v in positions + normals):
        raise BuildError("Non-finite display coordinates returned by the compiler.")
    if any(type(i) is not int or i < 0 or i >= len(positions) // 3 for i in indices):
        raise BuildError("Invalid display triangle index returned by the compiler.")
    if any(d.get("code") == "display-external-face-unmeshed" for d in mesh.get("diagnostics", [])):
        raise BuildError("This BRep has faces the display route cannot tessellate; no partial preview was installed.")
    if occurrences[0].get("definitionId") != mesh.get("id"):
        raise BuildError("The compiler returned an unresolved display occurrence.")
    if occurrences[0].get("transform") != [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]:
        raise BuildError("A standalone CAD part must use its source coordinate frame.")
    return mesh


class BuildJob:
    """One short-lived CLI process; files are private to this immutable build snapshot."""
    def __init__(self, executable, files, main_file, timeout=180):
        self.digest = source_hash(files, main_file)
        self.timeout = timeout
        self.started = time.monotonic()
        self.process = None
        self.temp = tempfile.TemporaryDirectory(prefix="aetheris-blender-")
        self.directory = Path(self.temp.name)
        self.log = None
        try:
            source = self.directory / "source"
            for name, data in files.items():
                path = source / relative_path(name)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            self.display = self.directory / "display.json"
            self.step = self.directory / "part.step"
            self.log = (self.directory / "compiler.log").open("wb")
            executable = Path(executable)
            if not executable.is_file():
                raise BuildError("Aetheris runtime is missing. Install the complete extension ZIP or choose a compiler in add-on preferences.")
            command = (["dotnet", str(executable)] if executable.suffix.lower() == ".dll" else [str(executable)])
            command += ["mesh", str(source / main_file), "--format", "display-json", "--output", str(self.display), "--step-output", str(self.step), "--json"]
            self.process = subprocess.Popen(command, stdout=self.log, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
                                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        except Exception:
            self.close()
            raise

    def poll(self):
        code = self.process.poll()
        if code is None and time.monotonic() - self.started > self.timeout:
            self.close()
            raise BuildError("The compiler exceeded the 180 second build limit. The last accepted CAD part is unchanged.")
        return code

    def result(self):
        if self.poll() is None:
            raise BuildError("The compiler is still running.")
        self.log.flush()
        log = (self.directory / "compiler.log").read_text(encoding="utf-8", errors="replace")
        if self.process.returncode:
            try:
                report = json.loads(log)
                diagnostics = report.get("diagnostics", [])
                log = "\n".join(d if isinstance(d, str) else d.get("message", str(d)) for d in diagnostics) or log
            except ValueError:
                pass
            raise BuildError(log[-16000:] or "The Aetheris compiler failed without a diagnostic.")
        if not self.display.is_file() or not self.step.is_file():
            raise BuildError("The compiler did not produce both the display mesh and exact STEP.")
        document = json.loads(self.display.read_text(encoding="utf-8-sig"))
        mesh = validate_display(document)
        step = self.step.read_text(encoding="utf-8-sig")
        if not step.startswith("ISO-10303-21;") or "END-ISO-10303-21;" not in step:
            raise BuildError("The compiler returned an incomplete STEP artifact.")
        return mesh, step

    def close(self):
        if self.process and self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait()
        if self.log:
            self.log.close()
            self.log = None
        self.temp.cleanup()
