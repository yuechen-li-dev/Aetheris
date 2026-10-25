# Cadmata — browser-first Windows bundle

Download `Aetheris-2.0.0-win-x64.zip` from
[Aetheris releases](https://github.com/yuechen-li-dev/Aetheris/releases).
Extract the entire ZIP to a local folder. Open `Aetheris-win-x64` and run
**Cadmata.exe**. It starts a local CAD session and opens your default browser.
It uses your existing browser; the normal ZIP contains no private Chromium,
Edge or WebView2 runtime. No WebView2 installation is needed for this mode.

```powershell
.\Cadmata.exe
# Or open a part directly:
.\aetheris.exe view .\testdata\firmament\inline-step\canonical-through-hole.step
```

Cadmata opens a blank workspace. Use STEP 242 Viewer to open your own STEP file,
or generate a part in **Product Gallery**. Set **Export filename** before
generation; the part appears in the viewport and keeps that name for downloads.
**Download Canonical 242** keeps the imported or generated product's filename;
browser downloads use the filename portion of an export path.
Close the server console window, or press Ctrl+C there, to end the session.
Closing only the browser tab does not stop the local server. Reopen Cadmata.exe
to start another session. `Cadmata.exe --no-browser --urls http://127.0.0.1:5090`
starts an explicitly addressed server without opening a browser.

Left-drag to orbit freely through the top and bottom orientations, right-drag
or Shift-drag to pan, and scroll to zoom. **Standard view** selects exact top,
bottom, front, back, left, right or isometric views. **Refresh Display Data**
refreshes the currently loaded model and is disabled in an empty workspace.

The **Modeling Demo** builds one composed part from a rectangular base and a
centered raised block. Set their dimensions in millimetres and select **Build
composed part**. It uses the same Firmament Compose planning and STEP export as
the compiler; it does not run the retired two-body Boolean demo. The raised
block must fit within the base footprint. Expand **Firmament source** to inspect
the program used to construct the part.

Windows 11 x64 is the initial qualification target. A WebGPU-capable GPU/driver
is recommended. Three Telos uses automatic CIR where qualified and mesh
otherwise. A browser without WebGPU uses the labeled existing WebGL fallback;
initialization/device failures show a diagnostic. The ZIP includes the .NET
runtimes and production UI assets. No SDK, Node or internet connection is needed
for local CAD in an existing browser. Extract into a fresh local directory when
updating, rather than merging old browser/runtime files into the new package.

## Optional desktop window

Run **Cadmata.Desktop.exe** to use a dedicated Windows window instead of a browser
tab. It uses the shared Microsoft Evergreen WebView2 Runtime. If that runtime is
missing, it offers Microsoft's download page and a Retry button. Installation
is explicit; Cadmata never downloads or installs a browser automatically.
Normal Cadmata.exe does not need this prerequisite. Closing the optional desktop
window stops its local server.

An explicitly packaged offline desktop edition may carry a `webview2` directory.
Keep that directory intact and update that edition to receive browser fixes.
Fixed-runtime editions require a local ASCII extraction path; spaces work,
but a CJK path previously failed loading the runtime's `dxil.dll`. Network/UNC
locations are unsupported for that fixed runtime. These restrictions do not
describe the normal browser-first package.

The executable is unsigned. Windows may display an unknown-publisher warning
for a downloaded ZIP. The portable application has no installer or automatic updater.
Your browser and shared Evergreen runtime receive their own updates; download
a newer ZIP to update Cadmata itself.

In the optional desktop window, About Cadmata shows the version and source identity. Open diagnostic log shows
the current session log. Logs and disposable browser profiles live under
`%LOCALAPPDATA%\Aetheris\Cadmata`; the extracted application is read-only during
normal use. Twenty diagnostic logs are retained. Exports use browser downloads,
and model state is session-local. Reopening starts a blank workspace. An explicit
STEP or Firmament file argument still opens that file at startup.

STEP import keeps its qualification status visible. Warnings and informational
messages are summarized as counts; errors remain visible with their explanations.
Use **Download diagnostic log** beside the summary to save all messages, including
their severity, code and source. The same API diagnostics are recorded in the
desktop session log, accessible through **Open diagnostic log**. This presentation
does not change import tolerances, qualification or geometry.

Aetheris is AGPL-3.0-only; the managed Copeland compiler retains GPLv3.
See LICENSE and licenses/ for bundled dependency notices.
CORRESPONDING-SOURCE.md supplies exact source revisions and downloadable source
archives for both projects. Source: https://github.com/yuechen-li-dev/Aetheris.
BUILD-METADATA.json identifies the source revision, modifications, default browser
presentation and optional desktop runtime mode. Evergreen has no packaged browser pin.
A package marked `modifiedSource` is a local qualification candidate, not a
published source-identical release.
