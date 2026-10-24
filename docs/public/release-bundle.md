# Aetheris Windows bundle

This bundle is the `win-x64` product distribution. Run commands from the `Aetheris-win-x64` directory.

Double-click `Cadmata.exe` in that directory to open the desktop application.
It hosts its own local service on a temporary loopback port and closes that
service with its window. The CLI and desktop share one set of .NET and CAD
libraries. The `webview2` directory contains the desktop's pinned offline browser;
keep it beside the executable. `Cadmata.Server.exe` is the explicit headless
server entry point, not the desktop application.

```powershell
.\aetheris.exe --help
.\aetheris.exe validate .\fixtures\Canonical\PMI\hole-diameter-and-datum.firmament --json
.\aetheris.exe build .\fixtures\Canonical\PMI\hole-diameter-and-datum.firmament --output .\out\first-part.step --json
.\aetheris.exe analyze .\out\first-part.step --json
.\aetheris.exe view .\out\first-part.step
.\forge-host\Aetheris.Forge.Host.exe info
.\forge-host\Aetheris.Forge.Host.exe list
```

The complete public guide is under `docs/public`, executable Firmament examples are under `fixtures`, and foreign-language Forge clients are under `samples/forge-interop-x1`. The default material catalog is embedded in the managed libraries.

The ZIP is the complete end-user distribution. The `Aetheris.CLI` tool package, its dependency-closed public library set, and `aetheris-firmament-0.3.0-preview.3.vsix` are published as separate release assets rather than duplicated inside the runtime tree. Verify downloaded binary assets against the release's `SHA256SUMS.txt`.

Cadmata and the NativeAOT Forge Host are included. The standalone CLI NuGet tool does not include Cadmata. The desktop bundle targets Windows x64; the managed CLI also runs on Linux x64. Aetheris supports bounded geometry, Sheet Metal, PMI, FEA, and STEP-import classes; read `docs/public/reference/supported-features.md` and `docs/public/reference/known-issues.md` before relying on a boundary case.
