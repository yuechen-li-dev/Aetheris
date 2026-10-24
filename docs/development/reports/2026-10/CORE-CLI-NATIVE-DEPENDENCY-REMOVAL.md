# Core CLI native dependency removal

2026-10-23. Base `b80e1838` plus the local changes. Source changes and local
qualification; no package publication.

## Boundary and implementation

The default compiler/CLI bundles no **third-party OS-native libraries**. .NET's
runtime and apphost remain platform dependencies; browser rendering uses the
user's browser. This is not a claim that the entire desktop application or every
optional interoperability package contains no native code.

- Removed PDFtoImage, SkiaSharp and PDFium from `Aetheris.Drawing`. PDF metadata
  inspection uses managed PdfPig 0.1.16. PDF.js 6.4.299 renders pages, thumbnails
  and source-linked PNG crops in the existing Drawing Notes UI. Its pinned
  minified browser assets and font/CMap/decoder licenses are embedded; there
  are no CDN requests, Node canvas dependencies or bundled browser executable.
  Portable browser WebAssembly decoders are included, not OS shared libraries.
  Only the pinned npm manifests and restore instructions are tracked. The full
  download and former vendor folder are ignored; source builds restore assets
  with `npm ci --ignore-scripts --omit=optional`. Explicit resource inputs exclude
  maps, viewers, legacy builds, package metadata, types and Node tooling.
- Vector drawing generation remains the existing managed PDF/SVG/PPTX path.
  Headless source-PDF rasterization is explicitly optional:
  `--renderer <path-to-pdftoppm>`. No installation, discovery or silent backend
  fallback occurs. An omitted backend reports `drawing-renderer-required`.
- The default material catalog uses the **existing** managed seed rows, shared
  with browser compilation. Validation and mapping remain in StandardLibrary.
  A regression compares every resolved identity, SI property and provenance
  field against the original checked-in SQLite database.
- `Aetheris.Materials.Sqlite` owns EF/SQLite, its compiled model, reproducible
  database generator and historical database. Custom callers migrate from
  `new MaterialCatalog(path)` to `SqliteMaterialCatalog.Open(path)`, retaining
  the `IMaterialResolver` injection seam. The default compiler does not reference
  this optional adapter. Its query adapter is qualified on .NET; NativeAOT
  SQLite query execution is not claimed.
- `Aetheris.ThreeDm.CLI` supplies `aetheris-3dm inspect` and `recover`, retaining
  the existing inspection/recovery implementation and Rhino3dm dependency.
  Old core CLI command names give an explicit installation/invocation message.
  No reflection-based plugin loader or parallel command implementation was added.
- FrictionLab retains its managed executable entry point (`dotnet run` works),
  but no longer generates an unnecessary apphost that was copied into the CLI.

## Coordinates and diagnostics

Notes remain referenced to the source SHA-256 and top-left physical PDF points
on the **rotated visible CropBox**, including UserUnit. The managed library's
page-size shortcut missed an inherited CropBox; inspection therefore resolves
page-tree box metadata through its existing tokenizer/object resolver, with
bounded cycle detection and explicit malformed-metadata failures. It does not
introduce another PDF parser.

The browser verifies the PDF hash, page count and viewport dimensions before
enabling annotations. A mismatch with an old notebook is an explicit error;
coordinates are not silently converted. Source inspector and renderer versions
are distinct metadata. Legacy notebook renderer provenance survives loading.
Crop sidecars retain page, original bounds, source hash, actual renderer version,
DPI and output filename; raster bounds round outward to whole pixels.

The external renderer uses argument lists, an execution timeout, process cleanup,
temporary output and PNG dimension verification. Browser rendering uses PDF.js
viewports and canvas encoding. Drawing Notes exceptions now retain their codes
and messages in structured CLI JSON, and code-bearing text output.

## Package qualification

The CLI build rejects resolved third-party native assets. The release script and
CI additionally run `scripts/Test-CoreCliPayload.ps1` over the actual package,
dependency receipts and PE/native payload files. The only runtime exemption is
the .NET runtime/apphost. Optional packages are deliberately outside that guard.

Evidence defaults to ignored `artifacts/local/native-dependency-removal/`:

- Core NuGet tool: **19,752,875 bytes (18.84 MiB)**; actual package, portable
  publish directory and ZIP all passed the guard. The Windows self-contained
  publish also passed, with only its .NET runtime/apphost native files exempted.
- A deliberate injection of a real native SQLite DLL into the portable payload
  was rejected as an unowned native PE. The clean payload passed again.
  A synthetic unexpected native dependency was independently rejected from the
  resolved-graph check.
- Core and optional 3DM tools were packed, installed using local-only NuGet feeds,
  and invoked through their installed commands. Core PDF inspection succeeded;
  3DM inspection preserved the local source's 27 BReps, 185 faces, 346 edges,
  219 vertices and metre-to-millimetre unit conversion.
- Real browser qualification checked all four rotations, an inherited offset
  CropBox and UserUnit 2 against **rendered red-pixel bounds**, then exercised
  annotation creation, save/reload and PNG/metadata downloads on both the
  coordinate witness and the engineering sheet.
- External pdftoppm 26.07.0 rendering and crops were checked against the same
  actual colored source pixels in all four orientations, not just output sizes.
- Published CLI compiled the countersunk L-bracket and two-component mounted
  bracket assembly to STEP, and generated the inspection plate's PDF/SVG/PPTX
  drawing. These paths do not invoke a PDF rasterizer.
  Framework-dependent and self-contained inspection drawings have identical
  PDF SHA-256 `5fd16e2871d428dd8a82401986317051d4f5b3998a11aed479a0af99fd9db4dd`.
  The existing complete code-native assembly rehearsal passed, including STEP
  round trips, stable definitions, moved occurrence placement and solid-Revolve
  geometry parity.

The browser regression is `scripts/test-drawing-notes-browser.mjs`; deterministic
source witnesses and regeneration instructions are in `fixtures/DrawingNotes/`.
Public usage and compatibility changes are documented under `docs/public/`.

Follow-up packaging hygiene: PDF.js is now a pinned npm build dependency rather
than checked-in vendor files. `.gitignore` excludes the downloaded distribution
and archives; the project embeds an explicit runtime subset using minified
modules. The revised tool package is **19,603,470 bytes**, with browser and
native-payload checks recorded under `artifacts/local/pdfjs-packaging/`. Optional
native Node canvas packages are omitted during restore.
The follow-up Release build, 1,088-test fast lane and complete 5,963-test suite
passed again with zero failures/skips. Browser rendering/crops and package
inspection passed; Git sees just the two manifests and README in the asset folder.

## Final regression gate

| Check | Result |
|---|---|
| Release solution build | Passed; final incremental build had zero errors/warnings |
| Fast Core lane | 1,088 passed |
| Full serial solution lane | **5,963 passed, zero failures/skips, 20 populated suites** |
| Material catalog parity and catalog behavior | 7 passed, including every original SQLite value and provenance field |
| Drawing Notes managed/external renderer tests | 16 passed, external renderer explicitly configured |
| Focused Drawing Notes/optional-tool CLI diagnostics | 5 passed |
| Real browser server/render/save/crop checks | Passed |
| Tool package, portable publish/ZIP and Windows self-contained publish | Native-asset guards passed |
| Published CLI STEP, assembly, drawing and local-tool install paths | Passed |
| Repository layout and whitespace checks | Passed |

FrictionLab has no discoverable tests and is not counted as coverage. After the
full run, the PDF.js fallback-module MIME correction was rebuilt and rechecked
with the 16 Drawing tests, five CLI tests and real browser qualification; no
engineering code changed. Standalone optional 3DM error handling was checked
against a missing file as well as successful inventory. Linux/other RID execution
was not run locally; CI now checks the actual portable tool package on Linux.

An initial run exposed a Drawing Notes CLI code-propagation failure (repaired), and an
unmodified assembly display test hit its five-second tessellation wall-clock
budget. The complete subsequent run passed, including that test. No engineering
algorithm or timeout threshold was changed for this pass.

Packaging is serial with MSBuild node reuse disabled. Fifteen reusable workers
left by an early packaging probe were identified by their completed parent
process and reaped; unrelated processes were not stopped.
Automatic approval review rejected deletion of redundant local downloads and
the initial package probe, supplying only “blocked by policy.” Those temporary
files remain; `artifacts/local/native-dependency-removal/cleanup-probes.ps1` is
provided for manual review (`-WhatIf` previews its bounded targets). It was not run.

## Verdict

**Success.** The default compiler/CLI admits the same engineering workflows while
its real published payload is free of third-party OS-native libraries. Drawing
Notes remains usable offline through the browser; explicitly external rasterizing
and native interoperability have visible, optional ownership. No engineering
emitter or geometry algorithm changed. This qualifies the core tool boundary,
not a general removal of native code from WebView2/.NET or optional Rhino/SQLite
integrations.
