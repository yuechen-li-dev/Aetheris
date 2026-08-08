# P1-CADMATA-M1 release hardening

## Architecture and Preview 1 contract

Cadmata is the `aetheris.client` React 19/TypeScript/Vite 7 single-page
application hosted by `Aetheris.Server`. The server owns document/import and
display-preparation APIs; the client converts DisplayIR to `DisplayScene` and
renders it with React Three Fiber/Three.js. `FitCameraToScene` already fits a
new scene using the pure bounds helpers in `displaySceneBounds.ts`.

Preview 1 uses one deliberately narrow launch contract:

```text
Cadmata.exe <absolute-file.step>
```

The host validates and reads one `.step`/`.stp`, serves the existing Vite
production bundle on an ephemeral loopback address, and exposes the startup
content once through `POST /api/v1/startup/step`. After Cadmata creates its
ordinary document and the renderer is ready, `App` claims that intent and runs
the same import/display transition used by browser file-open. A missing file,
invalid extension, unreadable/empty STEP, missing production bundle, browser
launch failure, import failure, or display failure produces a bounded readable
diagnostic. No daemon, fixed port, dev server, or singleton IPC is involved.

## CLI path and discovery

`aetheris view source.firmament` builds the adjacent STEP first and does not
launch after a failed build. `aetheris view artifact.step` or `.stp` skips the
compiler. In both cases the CLI normalizes the handoff to an absolute path,
starts Cadmata, checks for an immediate exit for 250 ms, reports the handoff,
and returns without supervising the viewer. Each command starts a new host.

Discovery order is:

1. `--cadmata-path` (legacy `--cad-assistant-path` remains accepted)
2. `AETHERIS_CADMATA_PATH`
3. compatibility `AETHERIS_CAD_ASSISTANT_PATH`
4. `Cadmata.exe` beside the CLI
5. `cadmata/Cadmata.exe`, then `tools/cadmata/Cadmata.exe`, relative to the CLI
6. `Cadmata` on `PATH`
7. a Release/Debug `Aetheris.Server` source-build fallback

## Production and smoke evidence

The host content root is its executable directory. Vite `dist` is copied to
`wwwroot` for build/publish, and the host serves default/static files plus the
SPA fallback. The release-like smoke used this isolated shape:

```text
bundle/
  aetheris.exe
  cadmata/
    Cadmata.exe
    Cadmata.dll and runtime files
    wwwroot/index.html and hashed assets
```

The bundle ran outside the repository from a temporary `Aetheris Smoke ...`
directory. Relative and absolute source/STEP inputs, nested directories,
spaces, Unicode, `.step`, and `.stp` were exercised. Source view produced its
adjacent STEP; direct STEP view did not build. Package-relative discovery
selected `bundle/cadmata/Cadmata.exe` without environment configuration.

The actual GUI smoke imported `box-hole-pmi.firmament` as `plate ü.step`.
It exposed and then verified a real display defect: the two multi-loop planar
faces had analytic metadata but no renderable bounded polygon, so the client
dropped them. Display preparation now supplies the existing tessellated
fallback for those trimmed planes. The corrected GUI showed one solid plate,
the round through-hole, and usable initial framing. Browser inspection found no
console error or failed resource request; Three.js emitted one upstream
`THREE.Clock` deprecation warning.

Measured on this development machine, the final isolated direct-STEP CLI
handoff returned in 402 ms; the Firmament build plus handoff returned in
1,157 ms. Document readiness took 51 ms and STEP import plus display
preparation took 234 ms in the running production host. The browser smoke was
visibly usable within about 2.5 seconds; that last number includes browser
navigation and observation overhead, not only renderer work.

## Packaging boundary and future work

P1-PACKAGE-M1 should build the Vite bundle before publishing the host and place
the complete host under `cadmata` beside `aetheris`. The current conditional
MSBuild copy is preparation, not the final NuGet build graph. The browser-host,
`WinExe` detachment, compatibility names, and one-shot startup endpoint are
Preview 1 compromises. The concrete post-release refactor record is
`docs/roadmap/cadmata-post-preview1-refactor-notes.md`; it covers the future
MachinaLayout.js application shell, TSPack consolidation, document state,
renderer boundary, packaging, and preserved test seams without implementing
that redesign here.
