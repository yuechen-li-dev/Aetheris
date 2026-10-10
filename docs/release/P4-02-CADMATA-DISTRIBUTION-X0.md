# P4-02 — CADMATA-DISTRIBUTION-X0

Release preflight, 2026-10-10: redistribution notices are complete and a native
extracted-app UI session has been recorded. The user authorized publishing
Preview 4. The final clean-source ZIP, checksum, metadata and artifact-specific
qualification record accompany [v2.0.0-preview.4](https://github.com/yuechen-li-dev/Aetheris/releases/tag/v2.0.0-preview.4).
The October 6 held candidate is superseded, not reused as the release binary.

## Architecture

`Cadmata.exe` is a Windows Forms WinExe hosting the existing ASP.NET
`CadmataApplication` in-process. Kestrel binds an ephemeral IPv4 loopback port.
A per-launch bootstrap token establishes an HttpOnly, SameSite=Strict cookie;
unauthenticated API requests receive 403. Production endpoints override dev
settings. WebView2 blocks off-origin resources, navigation and new windows.

The ZIP includes self-contained .NET 10, ASP.NET and WindowsDesktop runtimes,
the complete fixed WebView2 153.0.4234.48 x64 runtime, production frontend and
standalone Telos shaders. No SDK, Node/npm, installed browser, Vite server,
DXC/Naga executable or separate backend startup is needed at runtime. Three
Telos retains Auto display, qualified CIR and mesh display. The existing
labeled WebGL path remains for browsers without WebGPU; initialization failures
show diagnostics. No packaging-specific renderer or compiler is introduced.

Samples resolve from the application root. No-argument launch opens the
mounting block; a cylinder is included for qualified CIR. About/logs expose
build identity. Logs and disposable browser profiles live under
`%LOCALAPPDATA%\Aetheris\Cadmata`; twenty logs are retained. Browser downloads
own export destinations. Reopen starts a fresh sample session. Asynchronous
close disposes the browser and stops the backend. There is no background
service, installer, updater or startup hook.

## Repeatable build

Build prerequisites: .NET 10 SDK, Node/npm, Git and TSPack. None are runtime
prerequisites. From a clean checkout:

```powershell
./scripts/package-cadmata-preview4.ps1 -Release
```

Output defaults to ignored `artifacts/local/cadmata-distribution/release/`.
`-OutputDirectory` must stay inside the checkout. `-WebViewCab` reuses a
hash-verified Microsoft CAB. Release mode refuses modified source or a revision
that differs from HEAD. Candidate mode explicitly marks modified source.

The script restores the public pinned Copeland compiler under ignored
`artifacts/local/managed-wgsl/source`, verifies its source graph and packs the
local feed. It builds locked Telos sources, synchronizes the locked TSPack
workspace, materializes its declared local source junction, typechecks/builds
the frontend, then rebuilds/publishes the desktop with isolated artifact paths.
A stale client installation cannot shadow the workspace dependency graph.

The script stages complete runtimes, shaders, samples, notices and source
links; incomplete notices or missing shaders stop packaging. It inventories
each path, size, purpose and SHA-256 and creates a sorted ZIP with fixed entry
timestamps, plus `SHA256SUMS`. Generated outputs stay ignored.

## Redistribution audit

Managed compiler commit: `70b6cbe742ecfb2947c9c6a31cfa8e1d3a89067b`.
Reviewed graph: `16c0ce996358af80d0a0812f6c93dd97cd6e48cf6bef31c12ec3bd1fcd2ba10c`.
Package version: `0.1.0-preview.1-a3.16c0ce996358af80`.

The public pinned repository supplies GPLv3 for Copeland.Markdown, Profile,
SpanAllocation, TS, TS.Mir, TS.Workspace and TS.Backend.Wgsl. The original text
is preserved for each package and checked against a recorded SHA-256. No
license terms or attribution were invented; no sibling repo was modified.
Aetheris remains AGPL-3.0-only. [GPLv3 section 13](https://www.gnu.org/licenses/gpl-3.0.html)
permits GPLv3/AGPLv3 combinations while retaining each license.
`CORRESPONDING-SOURCE.md` supplies exact revisions and source archives for both
projects, including build scripts and compiler pins.

`wwwroot/bundled-packages.json` inventories packages with retained code in
production chunks, including the lazy WebGL fallback. The collector checks
that every emitted package has notices. MediaPipe Tasks Vision 0.10.17,
maath 0.10.8 and stats-gl 2.4.2 have no retained code in any chunk and appear
in the audit's excluded installed-package list. Supplied/pinned supplemental
notices cover the emitted packages and NuGet graph. Complete Microsoft fixed
runtime and .NET notices are retained. `licenses/AUDIT.json` must contain no
unresolved notices; the script now fails on an incomplete audit.

## Native qualification

The preflight candidate was extracted into a separate local temporary folder
with spaces. The agent operated its actual native window through Windows
Computer Use. This is agent-performed native UI qualification, not a claimed
human operator session or a substitute browser screenshot.

Observed: opening the executable through the native launcher, no console/dev
tools, sample rendering, orbit, zoom, geometry selection, native STEP picker,
external NIST CTC03 import with PMI, ordinary browser download UI, title-bar
close and reopen. Export created a 715,740-byte STEP file in Downloads.
Aetheris.CLI reimported it as one body, one shell and 120 faces. Close removed
the prior listener; reopen restored the sample. No firewall prompt appeared in
these local sessions. Default downloads were used; no Save As dialog is claimed.

The final ZIP is independently extracted and qualified before publication.
The harness uses the actual extracted WebView2, System32-only PATH, unrelated
CWD, unavailable DOTNET_ROOT, fresh browser profile and contradictory ASP.NET
dev settings. It verifies mesh/CIR drawing, navigation/selection, STEP export
and reimport, invalid-file preservation, offline origin restrictions and clean
shutdown. `CADMATA_QUALIFICATION_MANUAL_CLOSE=1` lets the native operator close
the owning window while the harness checks process/listener termination.
The release qualification record identifies the exact tested ZIP and revision.
Raw evidence lives under `artifacts/local/cadmata-preview4-release/`.

## Limitations

Qualification targets Windows 11 x64 and one NVIDIA WebGPU configuration.
A CJK extraction path failed WebView2's `dxil.dll` initialization with Windows
Error 87. Identical bytes rendered from an ASCII path. Use a local ASCII path;
spaces work. The earlier Latin-1 path experiment is not general Unicode
support. No DLL relocation workaround or alternate renderer is introduced.
Fixed runtime 154 remains unqualified after its earlier initialization failure.

The executable is unsigned. Downloaded artifacts may trigger SmartScreen or
unknown-publisher warnings; local extraction does not prove their absence.
Fresh-account testing, physical network disconnection, broad GPU/device-loss
coverage and every Assembly/Scene workload are not claimed. Network/UNC folders
are unsupported. Experimental CSG remains outside core Preview 4 acceptance.

## Regression and artifact records

Validation includes the solution build, fast kernel lane, full solution lane
and frontend tests/typecheck/build. The full lane is not replaced by the fast
lane. An initial concurrent-build run hit a CLI display time budget; the
isolated failing test passed. Original and subsequent complete-run logs are
retained, with outcomes recorded in the release qualification asset. No
production display budget is relaxed.

Two pre-existing frontend lint errors were corrected locally: inspection refs
now synchronize in the effect, and a changed model resets its patch selection
before commit rather than triggering a second render from an effect. The GPU
host and inspection behavior are retained. Frontend lint/typecheck and 89 tests
in 19 files pass after this correction.

```powershell
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Release assets supply the final ZIP hash/byte count. `BUILD-METADATA.json`
supplies exact source identity and content inventory. The old candidate hash
is not reused for the clean-source release. User instructions and release
notes live under `docs/public/cadmata/`.
