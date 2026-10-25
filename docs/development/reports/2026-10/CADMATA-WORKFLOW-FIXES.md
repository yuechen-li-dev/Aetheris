# Cadmata workflow fixes

Verdict: **Success**. All five reported workflows passed in the actual updated
Windows desktop application, using its bundled WebView2 and GPU renderer.

## Findings and repairs

| Report | Cause and repair |
| --- | --- |
| Cannot orbit into top/bottom | The shared Telos navigation adapter discarded pitch when the eye approached its up axis (`0.995` guard). Camera orbit now rotates the viewing frame with the eye, without a pole rejection. Exact standard views use the camera owner; Fit preserves orientation. |
| Gallery generates but does not display | ASP.NET serialized Forge artifact kinds numerically, while the UI searched for `StepAp242`. The Forge enum contracts now declare their canonical string serialization and still accept legacy numeric input. Generation also checks the preview result before reporting success. |
| Canonical download uses UUID | Gallery accepts a download export path, returns it with the artifact and carries it through preview into Canonical download. One browser filename helper preserves the product leaf name. The path is download metadata, never a server filesystem destination. Imports preserve their source filename; failed imports retain the prior model/name. |
| Startup/Refresh interference | No-argument desktop startup now opens a blank document. Explicit startup files remain supported. Successful STEP imports clear stale assembly source paths, so Refresh targets the current model. Blank Refresh stays disabled. |
| Obsolete modeling demo | Removed the two-body Boolean/translation controls. The replacement builds a bounded base plus centered raised block through ordinary Firmament Compose and the current section-stack planner. It exposes the generated source, rejects invalid dimensions and retains the real materialization receipt. Developer diagnostics are collapsed. Existing low-level API endpoints remain for compatibility. |

The demo submits Firmament source to the existing compiler; its actual
materialization receipt is `PrismaticSectionStack/Add`. It does not call the
document Boolean endpoints or introduce another geometry lowerer/language route.

## Evidence

All generated evidence is under
`artifacts/local/release-2-readiness/cadmata-workflow-fixes/`.

- Full solution build succeeded; final incremental build: zero errors/warnings.
- Fast .NET lane: 1,089 passed. Full serialized lane: **6,011 passed**, zero
  failed, two optional external PDF-renderer tests skipped. FrictionLab has no
  discovered tests. The final server/API test binaries were rebuilt before their
  position in the full serialized lane; its 70 server tests passed.
- Frontend: **104 passed**, production build and ESLint passed.
- Shared Telos: **19 passed**, including a full orbit through both poles,
  finite camera matrices, exact pole views and fit preserving direction.
- Final desktop receipt: `desktop-ui-final/qualification.json`.
  It proves blank startup, gallery rendering, `mounting-plate.step` download,
  composed-block rendering/Refresh, exact Top/Bottom/Isometric actions,
  canonical STEP export/reimport, failed-import model retention, native close
  and server shutdown. No uncaught browser errors.
- `desktop-ui-final/packaged-blank-startup.png`,
  `packaged-gallery-preview.png` and `packaged-composed-block.png` document the
  real UI. These are automated application interactions, not a human manual run.
- The supplied McMaster screw still renders as Degraded with all 35 diagnostic
  messages preserved in both logs and summarized in the UI.
- The demo's generated program was independently compiled and inspected through
  the real CLI: one enclosed manifold, 11 planar faces, 24 edges, 16 vertices,
  bounds `[-40,-25,0]..[40,25,20]` mm. See `composed-block-build.json` and
  `composed-block-analysis.json`.
- The gallery's Canonical reexport is Qualified: 46 faces, 116 edges and
  76 vertices (`gallery-reexport-analysis.json`).
- Kernel binaries remain byte-identical to the existing qualified bundle. Only
  the desktop, server and Forge protocol managed libraries plus frontend assets
  were overlaid into the local test candidate. No geometry algorithm, existing
  Firmament fixture or dependency version changed.

The Telos producer was rebuilt and its local-package lock identity refreshed
through TSPack; the lock diff changes only the local Telos identity/references.
The existing fixed local-dependency build tool was used after the older PATH
tool failed on the already-known peer-range limitation. No dependency cache was
edited by hand and no other repository was changed.

Public portable-app documentation now describes blank startup, standard views,
gallery export naming and the composed-block demo. Code-quality review removed
obsolete Boolean UI state/styles and retained one camera and filename authority.
Dependency notices and `git diff --check` pass.

## Local candidate

`Aetheris-win-x64/Cadmata.exe` under the evidence directory contains the updated
frontend and compiled application libraries, reusing the qualified runtime.
Metadata explicitly marks it as modified local source. `local-candidate-receipt.json`
records changed library hashes and kernel parity. The original extracted bundle
is untouched. This is not a new published release or a claim of fresh hosted CI.
