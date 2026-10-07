# Cadmata Preview 4 — distribution note draft

**Held local candidate; not published.** Redistribution notices and the
mandatory manual packaged-app session remain open.

Cadmata is Aetheris's engineering viewer for authored models and STEP AP242.
The Windows x64 portable ZIP includes the application, .NET runtime, fixed
WebView2 browser runtime, shaders and small samples.

When released: download the ZIP, extract it completely to a local folder and
run **Cadmata.exe**. The included mounting block opens automatically. No SDK,
Node/npm, terminal, repository checkout or separate backend startup is needed.

Automated tests of the extracted application demonstrate Three Telos rendering,
qualified direct CIR, mesh fallback, orbit/selection, external STEP import with
PMI, STEP download/reimport and clean window shutdown. The same workflow passes
after relocation with the package directory non-writable.

Initial qualification covers Windows 11 x64 on one NVIDIA WebGPU configuration.
Native picker/Save As and the required manual session remain unverified. The
app is unsigned; Windows may show an unknown-publisher warning for downloaded
files. Session state resets on reopen. Updates use a newer downloaded ZIP.

Source and licensing: [Aetheris](https://github.com/yuechen-li-dev/Aetheris),
AGPL-3.0-only. The package identifies its source/base revision and modified-source
status. A public artifact requires a published corresponding source revision
and a complete bundled-dependency notice audit.
