import json
import pathlib
import subprocess
import sys

host, request_path, output_dir = sys.argv[1:4]
request = pathlib.Path(request_path).read_text(encoding="utf-8")
process = subprocess.run(
    [host, "invoke", "Standard.SheetMetal.ElectronicsEnclosure", "--request", "-", "--out", output_dir],
    input=request,
    text=True,
    capture_output=True,
    check=False,
)
result = json.loads(process.stdout)
if process.returncode != 0 or not result.get("success"):
    raise SystemExit(process.stdout or process.stderr)
for artifact in result["artifacts"]:
    assert (pathlib.Path(output_dir) / artifact["path"]).is_file()
print(json.dumps(result))
