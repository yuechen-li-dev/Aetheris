# Forge Host Protocol v1

Forge interop has three operations: list Templates, describe one Template, and invoke it. The host owns Firmament types, units, defaults, `Require` checks, specialization, geometry, and artifact production. Foreign clients only exchange explicit JSON and files.

X0 also publishes `Standard.Products.Office.Paperclip`. `describe` exposes its `PaperclipPolicy` Record fields and metric units; `invoke` accepts those fields, lowers them to a semantic WireForm Straight/Bend sequence, and emits `paperclip.step` through deterministic analytic AP242.

C# can use the direct Forge API. Python, Go, Rust, and TypeScript/Node use the process protocol:

```powershell
$host = ".\forge-host\Aetheris.Forge.Host.exe"
& $host list
& $host describe Standard.SheetMetal.ElectronicsEnclosure
python .\samples\forge-interop-x1\python\client.py $host .\samples\forge-interop-x1\request.json .\out\forge-python
go run .\samples\forge-interop-x1\go\main.go $host .\samples\forge-interop-x1\request.json .\out\forge-go
rustc .\samples\forge-interop-x1\rust\client.rs -o .\out\forge-rust.exe
.\out\forge-rust.exe $host .\samples\forge-interop-x1\request.json .\out\forge-rust
node .\samples\forge-interop-x1\typescript\client.ts $host .\samples\forge-interop-x1\request.json .\out\forge-typescript
```

These commands run from the extracted `Aetheris-win-x64` directory and require no source checkout. Repository developers can publish the same host from `Aetheris.Forge.Host`; the released NativeAOT executable above is the qualified product path.

The process command is `invoke <stable-template-id> --request - --out <directory>`. A minimal Python client can send the Protocol v1 JSON envelope directly:

```python
import json
import subprocess

host = r".\forge-host\Aetheris.Forge.Host.exe"
request = {
    "protocolVersion": 1,
    "arguments": {
        "width": "120 mm",
        "height": "40 mm",
        "depth": "80 mm",
        "thickness": "1.5 mm",
        "lidLipHeight": "8 mm",
        "insideRadius": "2 mm",
        "kFactor": 0.42,
        "reliefPolicy": "Rectangular",
    },
    "artifacts": ["StepAp242", "FlatStep", "Svg"],
}
completed = subprocess.run(
    [host, "invoke", "Standard.SheetMetal.ElectronicsEnclosure", "--request", "-", "--out", r".\out\forge-python"],
    input=json.dumps(request), text=True, capture_output=True, check=True,
)
response = json.loads(completed.stdout)
assert response["success"]
```

`describe` is the authority for argument names, categories, units, required/default state, enum cases, nested Record fields, named constraints, output kind, and artifacts. The stable ID is the invocation identity; the human-readable generic signature is descriptive metadata. Protocol callers never send Firmament source or compiler AST nodes.

The shared production request is [`request.json`](../../../samples/forge-interop-x1/request.json), and the tiny clients are under [`samples/forge-interop-x1`](../../../samples/forge-interop-x1). Protocol v1 is language-neutral. The shipped NativeAOT binary and release bundle are qualified only for `win-x64`; framework-dependent tests on other operating systems establish protocol logic, not release-binary qualification.
