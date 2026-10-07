# Cadmata — Aetheris Preview 4

Release instructions draft. This local candidate is **not approved for public
distribution**: the redistribution notice audit and required manual session
remain open. No GitHub Release has been published.

When released, download `Cadmata-Preview4-win-x64.zip` from Aetheris GitHub Releases.
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

The executable is unsigned. Windows may display an unknown-publisher warning
for a downloaded ZIP. Preview 4 has no installer or automatic updater.
Download a newer ZIP to update the bundled browser/security runtime.

About Cadmata shows the version and source identity. Open diagnostic log shows
the current session log. Logs and disposable browser profiles live under
`%LOCALAPPDATA%\Aetheris\Cadmata`; the extracted application is read-only during
normal use. Twenty diagnostic logs are retained. Exports use browser downloads,
and model state is session-local. Reopening loads the included sample again.

Aetheris is AGPL-3.0-only; see LICENSE and licenses/ for bundled dependency
notices. Corresponding source: https://github.com/yuechen-li-dev/Aetheris.
BUILD-METADATA.json identifies the source revision, modifications and files.
A package marked `modifiedSource` is a local qualification candidate, not a
published source-identical release.
