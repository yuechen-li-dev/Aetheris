# Aetheris 2.0.0-preview.3 Windows bundle

This bundle is the qualified `win-x64` Preview 3 product. Run commands from the `Aetheris-win-x64` directory.

```powershell
.\aetheris.exe --help
.\aetheris.exe validate .\fixtures\Canonical\PMI\hole-diameter-and-datum.firmament --json
.\aetheris.exe build .\fixtures\Canonical\PMI\hole-diameter-and-datum.firmament --output .\out\first-part.step --json
.\aetheris.exe analyze .\out\first-part.step --json
.\aetheris.exe view .\out\first-part.step
.\forge-host\Aetheris.Forge.Host.exe info
.\forge-host\Aetheris.Forge.Host.exe list
```

The complete public guide is under `docs/public`, executable Firmament examples are under `fixtures`, foreign-language Forge clients are under `samples/forge-interop-x1`, and the deployed Standard Library material catalog is under `Materials`.

The ZIP is the complete end-user distribution. `packages/Aetheris.CLI.2.0.0-preview.3.nupkg`, the 16 public library packages, and `aetheris-firmament-0.3.0-preview.3.vsix` are published as separate release assets rather than duplicated inside the runtime tree. Verify downloaded binary assets against the release's `SHA256SUMS.txt`.

Cadmata and the NativeAOT Forge Host are included. The standalone CLI NuGet tool does not include Cadmata. Preview 3 is qualified only for Windows x64 and intentionally supports bounded geometry, Sheet Metal, PMI, FEA, and STEP-import classes; read `docs/public/reference/supported-features.md` and `docs/public/reference/known-issues.md` before relying on a boundary case.
