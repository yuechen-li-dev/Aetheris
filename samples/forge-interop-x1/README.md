# Forge interop X1 dogfood

These are intentionally tiny process clients for Forge Host Protocol v1. Build `Aetheris.Forge.Host` in Release, then pass the resulting host executable, the shared request, and a distinct output directory to a client:

```powershell
python python/client.py <host-executable> request.json out/python
go run go/main.go <host-executable> request.json out/go
rustc rust/client.rs -o out/rust-client.exe
out/rust-client.exe <host-executable> request.json out/rust
node typescript/client.ts <host-executable> request.json out/typescript
```

[`scripts/test-forge-interop-x1.ps1`](../../scripts/test-forge-interop-x1.ps1) builds the host, runs all four, validates every STEP with Aetheris.CLI, and rejects any cross-language artifact hash difference.
