# Forge Host Protocol v1

Forge interop has three operations: list Templates, describe one Template, and invoke it. The host owns Firmament types, units, defaults, `Require` checks, specialization, geometry, and artifact production. Foreign clients only exchange explicit JSON and files.

C# can use the direct Forge API. Python, Go, Rust, and TypeScript/Node use the process protocol:

```powershell
dotnet build Aetheris.Forge.Host -c Release
$host = "Aetheris.Forge.Host/bin/Release/net10.0/win-x64/Aetheris.Forge.Host.exe"
& $host list
& $host describe Standard.SheetMetal.ElectronicsEnclosure
python samples/forge-interop-x1/python/client.py $host samples/forge-interop-x1/request.json artifacts/forge-python
go run samples/forge-interop-x1/go/main.go $host samples/forge-interop-x1/request.json artifacts/forge-go
rustc samples/forge-interop-x1/rust/client.rs -o artifacts/forge-rust.exe
node samples/forge-interop-x1/typescript/client.ts $host samples/forge-interop-x1/request.json artifacts/forge-typescript
```

The shared production request is [`request.json`](../../../samples/forge-interop-x1/request.json), and the tiny clients are under [`samples/forge-interop-x1`](../../../samples/forge-interop-x1). Protocol v1 is language-neutral. The shipped NativeAOT binary and release bundle are qualified only for `win-x64`; framework-dependent tests on other operating systems establish protocol logic, not release-binary qualification.
