# Aetheris 2.0.0-preview.4 — Cadmata portable Windows

Cadmata is Aetheris's engineering viewer for authored models and STEP AP242.
Preview 4 provides a Windows x64 portable ZIP containing the application,
self-contained .NET runtime, fixed WebView2 runtime, shaders and small samples.

Download **Cadmata-Preview4-win-x64.zip**, extract it completely to a local
ASCII path such as `C:\Cadmata`, and run **Cadmata.exe**. Spaces are supported.
The included mounting block opens automatically. No SDK, Node/npm, terminal,
repository checkout or separate backend startup is needed for local CAD.

The packaged workflow includes Three Telos rendering, qualified direct CIR,
mesh display, orbit/pan/zoom and selection, STEP import with PMI, canonical
STEP download and reopen. Redistribution notices include the pinned Copeland
GPLv3 compiler license. The ZIP supplies exact corresponding-source links,
an emitted frontend package inventory and a complete notice audit.

Qualification targets Windows 11 x64 on one NVIDIA WebGPU configuration.
The executable is unsigned; Windows may show an unknown-publisher warning.
CJK extraction paths failed the bundled WebView2 `dxil.dll` load on this
machine; use an ASCII path. Network/UNC extraction folders are unsupported.
Session state resets on reopen. Updates use a newer downloaded ZIP.
Experimental modeling remains experimental; this release does not qualify
every GPU, device-loss recovery, or all Assembly/Scene workloads.

Aetheris remains [AGPL-3.0-only](https://github.com/yuechen-li-dev/Aetheris).
The bundled managed Copeland compiler retains GPLv3; both projects' exact
source revisions and source archive links are in `CORRESPONDING-SOURCE.md`.
`SHA256SUMS`, `BUILD-METADATA.json`, the package inventory and qualification
record accompany the release. GitHub's source archives identify the release
revision. See the [portable Windows instructions](portable-windows.md).
