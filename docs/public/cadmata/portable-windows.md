# Cadmata — Aetheris Preview 4

Download `Cadmata-Preview4-win-x64.zip` from
[Aetheris Preview 4](https://github.com/yuechen-li-dev/Aetheris/releases/tag/v2.0.0-preview.4).
Extract the entire ZIP to a local folder. Run **Cadmata.exe**.

The included mounting block opens automatically. Use STEP 242 Viewer to open
your own STEP file and **Download Canonical 242** to save a canonical STEP file.
Close the window to exit; reopen Cadmata.exe to start another session.

Windows 11 x64 is the initial qualification target. A WebGPU-capable GPU/driver
is recommended. Three Telos uses automatic CIR where qualified and mesh
otherwise. A browser without WebGPU uses the labeled existing WebGL fallback;
initialization/device failures show a diagnostic. The ZIP includes .NET,
ASP.NET, WindowsDesktop and fixed WebView2 153.0.4234.48. No SDK, Node, browser
runtime installation, terminal or internet connection is needed for local CAD.
Run from a local disk; network/UNC folders are unsupported by fixed WebView2.
Use an extraction path containing ASCII characters, such as `C:\Cadmata`;
spaces are supported. A CJK extraction path failed WebView2's `dxil.dll` load
on the qualification machine. If the viewport reports Windows Error 87,
close Cadmata and extract the entire ZIP to an ASCII path before reopening.

The executable is unsigned. Windows may display an unknown-publisher warning
for a downloaded ZIP. Preview 4 has no installer or automatic updater.
Download a newer ZIP to update the bundled browser/security runtime.

About Cadmata shows the version and source identity. Open diagnostic log shows
the current session log. Logs and disposable browser profiles live under
`%LOCALAPPDATA%\Aetheris\Cadmata`; the extracted application is read-only during
normal use. Twenty diagnostic logs are retained. Exports use browser downloads,
and model state is session-local. Reopening loads the included sample again.

Aetheris is AGPL-3.0-only; the managed Copeland compiler retains GPLv3.
See LICENSE and licenses/ for bundled dependency notices.
CORRESPONDING-SOURCE.md supplies exact source revisions and downloadable source
archives for both projects. Source: https://github.com/yuechen-li-dev/Aetheris.
BUILD-METADATA.json identifies the source revision, modifications and files.
A package marked `modifiedSource` is a local qualification candidate, not a
published source-identical release.
