# Forge interop X1 dogfood

These are intentionally tiny process clients for Forge Host Protocol v1. In the release ZIP, run from the extracted `Aetheris-win-x64` directory and pass the shipped host executable, the shared request, and a distinct output directory to a client:

```powershell
python samples/forge-interop-x1/python/client.py forge-host/Aetheris.Forge.Host.exe samples/forge-interop-x1/request.json out/python
go run samples/forge-interop-x1/go/main.go forge-host/Aetheris.Forge.Host.exe samples/forge-interop-x1/request.json out/go
rustc samples/forge-interop-x1/rust/client.rs -o out/rust-client.exe
out/rust-client.exe forge-host/Aetheris.Forge.Host.exe samples/forge-interop-x1/request.json out/rust
node samples/forge-interop-x1/typescript/client.ts forge-host/Aetheris.Forge.Host.exe samples/forge-interop-x1/request.json out/typescript
```

The clients require only their language runtime and the extracted release bundle. Each checks the structured response and emitted files; equivalent requests produce byte-identical artifacts.
