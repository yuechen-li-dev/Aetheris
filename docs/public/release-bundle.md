# Aetheris Windows bundle

This bundle is the `win-x64` product distribution. Run commands from the `Aetheris-win-x64` directory.

Double-click `Cadmata.exe` to open Cadmata in your existing default browser.
It starts a local service on a temporary loopback port. Close the server console
or press Ctrl+C there to stop it; closing the browser tab alone does not end the
session. `Cadmata.exe --no-browser --urls http://127.0.0.1:5090` is the explicit
headless entry point.

`Cadmata.Desktop.exe` is the optional dedicated Windows window. It uses the
shared Evergreen WebView2 Runtime and owns its server lifetime. Normal browser
mode does not need WebView2. The normal ZIP includes no browser engine, and
neither launcher installs one automatically. Both share the CAD and .NET
libraries in the bundle. See [Cadmata launch and runtime requirements](cadmata/portable-windows.md).

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
