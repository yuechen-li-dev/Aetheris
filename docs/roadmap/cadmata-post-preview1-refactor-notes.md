# Cadmata post-Preview-1 refactor notes

This document records concrete seams observed while hardening the Preview 1
`aetheris view` path. P2-CADMATA-M1 consumed the application-shell, document
machine, TSPack, theme, renderer-boundary, and grid items. Remaining work is
marked below; the implementation report is
[`docs/preview2/cadmata-refactor-m1.md`](../preview2/cadmata-refactor-m1.md).

## Preview 2 M1 disposition

| Roadmap seam | M1 disposition |
| --- | --- |
| First-class startup/browser document intent | Resolved with the Machina-authored document machine; cancellation/multi-document replacement remains deferred |
| TSPack dependency/build/policy ownership | Resolved; Vite retained below TSPack |
| Shell composition | Resolved for stable command/workspace/viewport/inspector geography; experimental modeling route extraction deferred |
| Renderer boundary and fit helpers | Preserved and strengthened with theme input; no renderer rewrite |
| Shell and viewport presentation | Resolved with independent token/config models and Atelier/Monument proof modes |
| Logarithmic grid GPU/React cost | Resolved with bounded line segments and recorded structural/live evidence |
| Native desktop/webview replacement | Deferred; browser host and WinExe compatibility remain intentionally intact |
| Explicit frontend artifact in MSBuild package graph | Deferred to packaging; TSPack now produces the deterministic artifact |

## Current architecture snapshot

- `aetheris.client` is a React 19 + TypeScript 5.9 single-page application
  built by Vite 7. `src/main.tsx` mounts the single `App` component.
- `Aetheris.Server` is the ASP.NET Core host. `Program.cs` configures Kestrel,
  the in-memory `KernelDocumentStore`, and `/api/v1` kernel/document routes.
  Before Preview 1 hardening it did not serve the Vite production bundle.
- `App.tsx` owns application shell, document creation, STEP import/export,
  active occurrence, display preparation, picking, demo modeling state,
  diagnostics, and most presentation state in one component.
- STEP import currently flows through `StepImportDropzone` -> browser `File`
  -> `File.text()` -> `importStep` -> document summary/display preparation ->
  `buildDisplaySceneData` -> `AetherisViewport`.
- The renderer boundary is reasonably explicit: API DTOs are converted to a
  framework-independent-ish `DisplayScene`, then `AetherisViewport` maps its
  renderables to React Three Fiber nodes.
- Camera fitting already exists in `FitCameraToScene`, backed by pure helpers
  in `viewer/displaySceneBounds.ts`, and runs when `displayScene` changes.
- Development assumes Vite HTTPS on port 5173 proxying `/api` to the ASP.NET
  HTTPS development endpoint. Those assumptions belong only to `npm run dev`.

## Pain points observed during Preview 1 hardening

- The public CLI called an arbitrary external program labelled both “Cadmata”
  and “CAD Assistant”; the repository's actual Cadmata React/server stack had
  no executable file-open entry point.
- `App.tsx` couples document lifecycle, import lifecycle, viewer selection,
  modeling-demo state, status copy, and layout. Startup-open therefore has to
  join a large component even though it is conceptually one document intent.
- Browser-selected files and process-supplied files enter through different
  transport mechanisms but need the same import/display state transition.
  Keep the transition shared; do not let process arguments leak into renderer
  code.
- The Vite configuration contains certificate creation and fixed-port proxy
  behavior alongside production bundle configuration. This is useful in
  development but makes the production boundary harder to see.
- Display preparation classified an all-analytic through-hole body as needing
  no bounded fallback, even though multi-loop planar analytic DTOs intentionally
  omit a simple outer polygon. The renderer then dropped the two holed faces.
  Preview 1 now requests bounded patches for that narrow case; a future display
  contract should declare per-face render requirements instead of making the
  client infer them from loop count.

## MachinaLayout.js migration candidates

`machinalayout` is already a runtime dependency, but the current application
shell remains hand-composed in `App.tsx`/`App.css`. Candidates below are based
on inspected code, not a proposed wholesale port.

| Current component/region | Current responsibility | Likely future composition | State dependency | Migration risk |
| --- | --- | --- | --- | --- |
| `App` shell | Header, tab switcher, workspace columns, status/footer | Application shell + vertical stack | Nearly all app state | High; split state first |
| Viewer workspace | Viewport plus import/inspection controls | Resizable main viewport and sidebar | Document/import/display state | Medium |
| STEP import panel | Dropzone, import action, import status | Panel/form composition | File and import lifecycle | Low |
| Inspector/status panels | Body metadata, diagnostics, display status | Inspector/property panels | Active occurrence and display DTOs | Medium |
| Modeling demo | Box/transform/Boolean controls | Separate routed/tool panel | Demo-only state | Medium; avoid contaminating viewer shell |
| Viewport overlays | Grid/axis and future transient controls | Overlay slots | Viewer-local state | Low if renderer stays isolated |

## TSPack migration/consolidation opportunities

- Cadmata currently uses `package.json`/npm scripts directly and has no TSPack
  manifest or workspace seam in this repository.
- A future migration could make dependency synchronization, locked production
  builds, policy/security checks, and the .NET publish prerequisite one
  reproducible workflow. Preview 1 should not invent that migration.
- The important integration point is artifact production: generate `dist`
  once, then copy it into the Cadmata host publish output. Keep Vite-specific
  commands out of CLI runtime discovery.
- Preserve the existing `build`, `test`, and `lint` behavioral boundaries so a
  future TSPack task can wrap or replace tooling without changing app logic.
- The public manual's `docs-sync` currently emits plain `JSON.stringify`
  output that needs one `tspack format` pass before `tspack check --format`.
  A future consolidated build should make generated artifacts formatter-stable
  so sync followed by check is idempotent.

## State-management cleanup

- Extract a document/session controller from `App.tsx`: create/reset document,
  import STEP, refresh summary/display, and expose one typed state machine.
- Model import as explicit states (`idle`, `selected`, `importing`, `loaded`,
  `display-failed`, `failed`) rather than coordinating `status`,
  `importStatus`, booleans, and multiple message strings.
- Keep browser file selection as an adapter that yields `{name, stepText}`.
  Startup open should yield the same shape through a host adapter.
- Demo modeling controls should own their state outside the viewer/document
  state so the default Cadmata path does not initialize unrelated concerns.

## Renderer/application boundary

- Preserve `DisplayScene` and the pure mapping/bounds helpers. They provide a
  valuable seam between server DTOs and React Three Fiber.
- Make camera-fit an explicit renderer command or scene-load policy in a later
  design, while retaining the tested pure fit calculation.
- Keep the renderer ignorant of process arguments, filesystem paths, Kestrel,
  and document transport. It should receive a scene and interaction callbacks.
- `AetherisViewport` still combines scene materialization, grid/axis helpers,
  picking, overlays, lighting, camera, and controls. Split only when the future
  layout/application architecture defines stable ownership.

## File/document lifecycle

- Preview 1 startup STEP enters through the host, is exposed as a single
  startup-open intent, and is consumed by `App` after document creation. In a
  proper refactor, make document-open intent a first-class application state
  transition and keep the renderer adapter ignorant of process arguments.
- Define cancellation/replacement semantics before adding multi-document or
  singleton behavior. Preview 1 intentionally starts a new instance per view.
- Preserve readable distinction between import success and display
  materialization failure; current tests already defend that distinction.
- Browser `File` validation and host-side path/extension/read validation should
  eventually share policy at an application boundary rather than duplicate
  presentation checks.

## Routing/window/application-shell concerns

- There is currently one SPA route and no document URL model. The Preview 1
  host should use a fixed root URL and server-held startup intent, not encode
  local paths in browser history.
- A future desktop shell can replace browser launch without changing the
  startup-open application transition or renderer.
- Do not introduce singleton IPC until document/session ownership and window
  semantics are designed together.

## Packaging implications

- The intended Preview 1 bundle seam is `aetheris[.exe]` beside a `cadmata`
  directory containing `Cadmata.exe` and its normal .NET publish files,
  including `wwwroot` production assets.
- The CLI should discover that relative shape without current-directory or
  source-tree knowledge. Development-source fallback may remain explicit and
  diagnostic until P1-PACKAGE-M1 publishes the bundle.
- Production assets must be rooted at the host content root/executable output;
  never search upward for the repository or start Vite at runtime.
- The Preview 1 host validates `wwwroot/index.html` before accepting a startup
  file. This is a useful packaging assertion, but the future package pipeline
  should make the frontend artifact a declared build input rather than relying
  on `dist` existing when MSBuild evaluates the project.

## Testing seams worth preserving

- Pure display-scene mapping and camera bounds/fit tests.
- API integration tests through `WebApplicationFactory`.
- App behavioral tests that distinguish import, display success, and display
  failure without brittle visual snapshots.
- CLI process-launch abstraction tests: exact executable, absolute argument,
  build-before-launch, and no-build direct STEP behavior.

## Temporary Preview 1 compromises to remove

This register is updated whenever Preview 1 glue is added.

| File/path | Reason | Why acceptable for Preview 1 | Preferred replacement | Removal dependency |
| --- | --- | --- | --- | --- |
| `Aetheris.Server` host/browser startup path | Reuses the working API and Vite UI without a desktop rewrite | One file, one local instance, no daemon or manual server | Desktop/webview application shell with explicit document-open adapter | Post-P1 shell/window architecture |
| CLI compatibility flag/env (`--cad-assistant-path`, `AETHERIS_CAD_ASSISTANT_PATH`) | Avoids breaking existing scripts | Public text can call the target Cadmata while honoring old configuration | Canonical Cadmata option/env with deprecation window | Packaging and compatibility policy |
| Cadmata host builds as `WinExe` and opens a browser | A console-subsystem host kept captured shell handles open after CLI exit | It provides a detached Windows application without changing the React/server stack | Native desktop/webview shell with a structured readiness/error channel | Post-P1 host/shell replacement |
| `Aetheris.Server.csproj` conditionally copies an existing Vite `dist` | Keeps frontend tooling out of CLI runtime and supports a self-contained publish shape | Packaging can build the SPA first and publish the host second | One TSPack/MSBuild package graph with an explicit frontend artifact dependency | P1 packaging workflow or later TSPack consolidation |

## Things that are surprisingly good and should NOT be rewritten

- `displaySceneBounds.ts` is pure, focused, and already covers far-from-origin
  and fit behavior.
- `buildDisplaySceneData` isolates API-to-render-scene conversion.
- Server import, document, display-preparation, and diagnostics endpoints form
  a usable application boundary; a shell rewrite need not replace the kernel
  API.
- Behavioral frontend tests mock the renderer and exercise state transitions,
  which is the right level for startup-open regression coverage.

## Candidate post-P1 milestone ladder

1. Specify document-open/session/window state independent of React and host.
2. Extract the current document/import controller behind that contract while
   preserving the existing API and behavioral tests.
3. Establish the TSPack workspace/build/package contract and reproducible
   frontend artifact handoff.
4. Move the application shell and panels incrementally to MachinaLayout.js.
5. Introduce a desktop shell adapter if desired, then remove the temporary
   browser-host launch glue.
6. Revisit renderer component ownership and overlays only after application
   state and layout boundaries are stable.
