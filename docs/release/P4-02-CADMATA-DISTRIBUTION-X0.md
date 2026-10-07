# P4-02 — CADMATA-DISTRIBUTION-X0

Verdict: **Meaningful progression**, 2026-10-06. A self-contained Windows ZIP
now runs the actual Cadmata backend, production frontend and Three Telos
without development tools on the application's PATH. The extracted app renders
the mechanical sample, imports an external AP242 part, exports/reimports STEP,
selects geometry and closes its backend. Direct CIR also renders through the
packaged host. Public distribution remains held by incomplete dependency
notices and the required manual packaged-app session.

## Package architecture

`Cadmata.exe` is a Windows Forms WinExe host. It runs the existing
`CadmataApplication` ASP.NET application in the same process, rather than
launching a second server executable or duplicating the CAD API. Both the
existing server entrypoint and the desktop host use this startup owner. The
existing development/server launch behavior remains available.
The desktop project opts into `EnableWindowsTargeting` for the existing Ubuntu
solution-build lane, following [NETSDK1100 guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100).
Linux CI was not executed here; this permits Windows cross-target compilation,
not a Linux desktop distribution. The final Windows republish after that
property change produced the identical ZIP hash recorded below.

The desktop starts Kestrel in Production on an ephemeral `127.0.0.1` port.
An explicit endpoint takes precedence over developer URL/environment settings.
An unguessable per-launch bootstrap URL gives the embedded browser an HttpOnly,
SameSite=Strict cookie; requests without it receive 403. There is no LAN
listener, fixed production port, certificate or firewall configuration step.
The socket integration test covers endpoint selection and the cookie boundary.

WebView2 SDK 1.0.4258.31 loads the complete bundled **Fixed Version runtime
153.0.4234.48 x64**, with a new browser profile for each window. The host blocks
off-origin HTTP(S) resource requests, external navigation and new windows.
The normal app does not enable remote debugging; the qualification harness
enables it only for its own child process.

The package contains self-contained .NET 10, ASP.NET and WindowsDesktop,
production Vite JS/CSS, standalone Telos `mesh.wgsl`/`line.wgsl` and optional
temporal shaders. Managed CIR/WGSL emission remains the existing backend path;
no Node, DXC, Naga executable or Vite server is started at runtime. Ordinary
mesh and qualified CIR retain the same shared Telos ownership and SpatialOnly
AA. No packaging-specific renderer fallback was added.

No-argument launch opens `samples/mounting-block.firmament`. This is the existing
small machined mounting-block source, including authored PMI. A second small
`samples/cylinder.firmament` supplies the already-qualified direct CIR witness.
Paths resolve from the executable root. Existing startup arguments also accept
STEP/Firmament/Assembly documents; no new import formats were added.

Startup is bounded to 45 seconds and failures show a diagnostic with the log
path. About exposes Preview 4/version/source identity. Logs live under
`%LOCALAPPDATA%\Aetheris\Cadmata\logs`; twenty logs are retained. Disposable
WebView profiles live in the sibling `sessions` directory. Profile deletion
can be deferred by browser locks; stale profiles older than one day are retried
on later launches. Exports use the browser download mechanism. Model state is
session-local; reopening starts with the bundled sample.

Closing asynchronously disposes the browser controller, stops/disposes the
backend and closes the form. The first implementation's synchronous UI-thread
shutdown failed the packaged WM_CLOSE test. Awaiting shutdown fixed this:
the recorded final session stopped the backend in milliseconds and the listener
was gone before the harness completed. No background service, updater or
startup hook was introduced.

## Repeatable build

From a source checkout with .NET 10 SDK, Node/npm and TSPack available:

```powershell
./scripts/package-cadmata-preview4.ps1
```

The default output is ignored `artifacts/local/cadmata-distribution/release/`.
The script builds Telos from its tracked npm lock, synchronizes the locked
client workspace with TSPack, typechecks/builds the frontend, publishes the
desktop self-contained for win-x64, verifies/downloads the fixed runtime CAB,
collects licenses, inventories files, creates the ZIP and writes SHA256SUMS.
`-WebViewCab <absolute-path>` reuses a previously downloaded, hash-verified CAB.
These tools are build prerequisites; none are application prerequisites.

The previous package-local TSPack manifest omitted the real sibling Telos
dependency and depended on an untracked client npm lock. The workspace now
declares that local source dependency. `ts-lock.toml` moved to the workspace
root byte-for-byte; registry versions were not updated. RunTargets use the
client package working directory. Existing generic release/SpaProxy targets
now point at the root workspace.

Current TSPack synchronization does not materialize this local source link.
The packaging script creates the declared Telos source junction and makes the
client use the workspace module tree. A stale client installation is moved to
ignored output rather than reused. These links are build-only and are absent
from the ZIP. An attempted fresh resolver update also rejected Tailwind's
`>=3.0.0 || insiders` peer range; the existing locked graph passes sync/check,
typecheck, lint, tests and production build. No sibling tooling repo was edited.

Qualification used a clean Git archive of base revision
`a8891bf7ea3a92f6bae2b13a7872195683d6f070`, overlaid with this task's source
changes, without development binaries, node_modules or the untracked client
npm lock. The snapshot is under ignored `artifacts/local/cadmata-distribution/clean-source`.
Its package explicitly identifies `.modified` source; it is not a release
whose exact source is already available at the base GitHub revision. Publication
requires committing/providing the corresponding final source revision.

ZIP entries have sorted paths and normalized timestamps. Packaging is
repeatable; byte-for-byte reproducibility across different SDKs, dependency
caches and checkout paths has not been established.

## Runtime pin and packaged qualification

The complete official WebView2 154.0.4258.62 runtime launched the sample backend
but failed Three Telos `requestDevice` with:

```text
DynamicLib.Open: dxil.dll Windows Error: 87
EnsureDXCLibraries(...dawn...PlatformFunctionsD3D12.cpp:221)
```

The packaged DXIL/DXC libraries were present. Changing the working directory
did not fix it. Runtime 153 rendered in the same extraction path, including
spaces and `ü`, so the build pins that version and packages it completely.
The final successful run uses the bundled runtime with no browser-folder
override. No DLL replacement, GPU sandbox disabling or legacy renderer patch
was used. The 154 failure remains preserved in local diagnostic evidence.
Any future browser pin change needs this packaged qualification again.

Official fixed-runtime distribution reference:
[Microsoft WebView2 distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).
The initial qualification target is Windows 11 x64, local disk. Windows 10,
UNC paths, other GPUs/driver versions and ARM64 are not qualified here.

The final ZIP is copied outside the repo into a fresh Temp extraction directory.
The application child receives System32-only PATH, an invalid DOTNET_ROOT,
unrelated Windows working directory and deliberately conflicting development
ASPNETCORE settings. The driver connects to the **actual extracted Cadmata.exe
WebView2**, without launching an alternate browser/backend. Its own Node and
Playwright are test tooling, outside the application dependency graph.
The harness verifies the browser origin against this new process's PID-specific
readiness log before driving it. Debug ports are selected dynamically by default.

Automated replay, using an absolute path to a locally available Playwright module:

```powershell
node scripts/qualify-cadmata-portable.mts "<extracted-package>" artifacts/local/cadmata-distribution/replay "<playwright/index.mjs>" "<external-part.stp>"
```

An optional fifth argument is the extracted `samples/cylinder.firmament` to
assert a direct CIR field draw. This replay does not satisfy manual qualification.

Observed host: Windows 11 Pro x64 build 26200, NVIDIA RTX 3070, NVIDIA driver
32.0.15.9636. Each launch creates a fresh WebView profile. This is not a new
Windows user account or a machine with the SDK uninstalled.

| Check | Recorded result |
| --- | --- |
| Default sample | Mounting block renders, mesh fallback explicitly `no-field-representation` |
| Telos / AA | Three Telos, SpatialOnly, local mesh/line shader requests succeed |
| Qualified CIR | Cylinder: one field draw, one engineering pick proxy, `shader-artifact-bound` |
| Camera / selection | Scripted orbit/pan/zoom and geometry selection succeed; orbit changes rendered pixels |
| External STEP | NIST CTC03 AP242 copied outside repo, actual packaged import and mesh display succeed |
| PMI | CTC03 published/evidence entities, position callouts and leaders visible in packaged screenshot |
| Export/reimport | Canonical download creates ISO-10303-21 file; reimport succeeds |
| Invalid STEP | Visible import failure, process remains alive, last valid model/export remains available |
| Security | Unauthenticated API request 403, ephemeral IPv4 loopback listener |
| Offline resource guard | All recorded application HTTP requests use its loopback origin; host blocks external web requests |
| Spaces/non-ASCII/CWD | Sample and complete scripted workflow pass outside repo in space/`ü` path, CWD Windows |
| Move / read-only | Same extracted package moved; write-deny ACL verified by an actual rejected write, workflow still passes |
| Close/reopen | Repeated packaged sessions close via native WM_CLOSE; process exits and listener no longer responds |
| Concurrent windows | Two simultaneous instances use distinct loopback ports and fresh profiles; both complete and close |
| Version / writes | Preview 4 identity in log/About implementation; application/log/profile paths follow documented ownership |

Fresh account, physical internet disconnection, filesystem denial of repo
access, downloaded-ZIP SmartScreen/Defender observation, native picker/Save As,
native title-bar interaction, window resize/device-loss recovery,
broad Assembly/Scene qualification and PMI filter interactions are
not claimed by this automated evidence. The active development server was not
stopped or moved to simulate isolation.
The final process inventory found no Cadmata/WebView2 process executing from
any of this task's extraction directories. PE subsystem 2 confirms the entrypoint
is a GUI application; its file metadata carries the Preview 4 product/version.

### Manual session — required, not completed

No human/manual packaged session is claimed. The successful evidence is an
automated actual-host test, with browser input-file injection/CDP downloads and
native WM_CLOSE. The available computer-use surface has native APIs disabled;
the Windows computer-use skill says "Run this once per fresh `node_repl`
JavaScript session," and that session tool is not an
available tool in this run. An automated screenshot cannot substitute for the
mission's mandatory manual session.

Before acceptance, use this ZIP in an interactive Windows session: extract,
double-click Cadmata.exe, observe the sample, orbit/select, use the real file
picker to import STEP, save STEP using the actual browser dialog, close the
title bar and reopen. Record native prompts, picker/export behavior and process
cleanup. Observe unsigned-app/SmartScreen and firewall behavior on a downloaded
artifact. The WinExe is unsigned; no signing result or absence of security
prompts is inferred from test-process launch.

## Contents, size and provenance

Final artifact: `release-final/Cadmata-Preview4-win-x64.zip`.
ZIP: **399,619,347 bytes (381.11 MiB)**. Extracted: **901,096,773 bytes
(859.35 MiB)**, 1,017 files including BUILD-METADATA.json.

```text
e0a74dbc1b3ec7a732e476f3760011e24c93385cd17567b20f76ca7a90f41d35
```

Fixed WebView2 CAB SHA-256:
`11e8240cb0bc56dcd3e4498907203c251346f65107fe35a3a13e152c7d51c79e`.

Final-ZIP automated sample-ready measurements: 2.756 s and 2.513 s for the
simultaneous sessions, 2.219 s after relocation/read-only guard, 1.663 s for
the qualified cylinder. These measure harness observation of model/host
readiness, not a manually observed first pixel. Logs separately record backend
readiness and page visibility.

Repository CLI inspection independently confirms the external and exported
STEP each have one body/shell, 120 faces, 354 edges and 236 vertices, with an
enclosed-manifold assessment. This does not assert complete PMI roundtrip
preservation.

`BUILD-METADATA.json` records Preview 4/base source identity, modified-source
status, fixed runtime URL/hash, notice audit status and each payload file's
path, byte size, purpose and SHA-256. Its inventory excludes the metadata file
itself; `package-summary.json` measures all extracted files including metadata.

The dominant component is the complete fixed browser runtime (about 700 MB
uncompressed). Self-contained .NET/ASP.NET/WindowsDesktop and dependencies are
about 183 MB; the CAD/backend assemblies about 15 MB; frontend/shaders about
1.7 MB. Source samples are a few kilobytes. Complete measured groups are in the
local content summary. No runtime/framework trimming was attempted.

The ZIP has one obvious Cadmata.exe. Extra executable payloads belonging to
the browser and .NET crash helper are internal runtime components. The unused
Forge Host apphost, PDBs and static-webasset development manifest are removed.
It contains no node_modules, source tree, tests, build intermediates or session
logs. Browser third-party notices and .NET runtime notices are retained.

### Redistribution audit — release hold

Aetheris LICENSE/THIRD_PARTY_NOTICES and collected dependency notices are
included. Supplied npm license/notice text and NuGet nuspec/license metadata
are preserved. Pinned upstream license copies cover Fiber, Draco and two
NuGet meta-package omissions; supplemental sources/hashes are checked in.
Other NuGet omissions are retrieved from their supplied repository commit.

The conservative runtime dependency/peer closure still has explicit unresolved
notice entries in `licenses/AUDIT.json`. The seven Copeland packages used for
managed WGSL have **no license text/expression or repository URL in their
published nuspec metadata**, so their terms cannot be inferred here. The
affected version is `0.1.0-preview.1-a3.7329cf8ff04d504a`:
Markdown, Profile, SpanAllocation, TS, TS.Backend.Wgsl, TS.Mir and TS.Workspace.
The remaining npm omissions are MediaPipe Tasks Vision 0.10.17, maath 0.10.8
and stats-gl 2.4.2. Their installed license identifiers do not supply the
missing text/attribution; pinned historical upstream trees checked for maath
and stats-gl also omit a license file. This is an intentionally conservative
hold, not an assertion that all transitive optional code is shipped by Vite.

Complete the owner-supplied Copeland notices and resolve these package
omissions (or prove unused code is excluded) before public redistribution.
No terms were invented and no sibling repository was modified. The package
README and metadata visibly identify it as a held local candidate.

## Regression evidence and local artifacts

Clean-snapshot .NET solution build passes. Fast kernel lane: **1,016 passed**.
Full serial solution lane: **4,333 passed, 0 failed, 7 skipped** in 20 suites.
The seven skips are existing local 3DM qualification tests whose external
assets are absent from the clean source archive. Client typecheck,
lint, production build and **86 tests in 18 files** pass. The packaging script
also rebuilds/typechecks the production frontend and self-contained desktop.

```powershell
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

An initial test run under relocated `--artifacts-path` failed existing tests
that calculate repo roots from assembly depth. Running the normal build/test
layout in the clean source snapshot resolves those failures. Initial failed
logs remain available and are not substituted for the final green lanes.

Raw logs/screenshots and the candidate remain ignored under
`artifacts/local/cadmata-distribution/`:

- `release-final/Cadmata-Preview4-win-x64.zip`, `SHA256SUMS`, package/content summaries.
- `final-package-build.log`, `final-solution-build.log`, `final-fast-tests.log`, `final-full-tests.log`.
- `ci-compatible-package-build.log`: repeated packaging, same final ZIP bytes/hash.
- `final-qualification/`: mounting-block image, orbit/selection image, external STEP/PMI image, export and qualification JSON.
- `cir-qualification/`: actual cylinder field draw and subsequent STEP workflow.
- `moved-read-only-qualification/`: relocated package workflow and `read-only-guard.txt`.
- `concurrent-qualification/`: second simultaneous instance and independent loopback port.
- `diagnostic/` and `runtime-153-diagnostic/`: original 154 error and isolated 153 rendering evidence.

No GitHub Release was published. The requested normal-user verdict is not yet
Accepted: the real packaged engineering workflow improves and is demonstrated,
but distribution notices and manual native-interaction evidence remain concrete
release gates.
