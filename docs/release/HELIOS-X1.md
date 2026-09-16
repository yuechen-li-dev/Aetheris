# HELIOS-X1 release report

## Executive verdict

**Accepted.** Helios demonstrates a useful semantic CAD editor on the public `@aetheris/cad` package without private kernel knowledge or a parallel CAD implementation. The browser path compiled and rendered the canonical bracket and assembly, resolved selection in both directions, rebuilt a valid property override, and visibly retained the last valid revision and geometry for an invalid edit. A separately implemented public-SDK sample generated a 7,294-byte STEP payload in Chromium.

Helios is deliberately bounded. It demonstrates the integration surface and does not prevent deeper or more specialized third-party Aetheris frontends.

## Feature inventory

- Shell: command bar, searchable and semantic-kind-filtered browser, Three.js viewport, inspector, source/diagnostics region, status bar.
- SDK calls: `Aetheris.create`, `info`, `compile`; `entity`, `setProperty`, `setSource`, `rebuild`, `resolveSelection`, `selectionForEntity`, `exportSTEP`, and disposal.
- Authoring convenience: deterministic centered through-hole source block; New/Open/source download and source history.
- Display: shaded, real edge overlay, wireframe; perspective/orthographic; front/top/right/iso/fit-model/fit-selection; orbit/pan/zoom; grid and axes.
- Assembly inspection: occurrence, referenced definition, parent, and transformed translation are shown using public display-mesh data.
- Themes: Mars and Sirius through shared design tokens.
- Fixtures: `editable-bracket.firmament` and `shared-block-assembly.firmament`.
- Public dependencies: React, Three.js, Vite, and the packed `@aetheris/cad` package.

## Firmament authority proof

The Hole command changes readable Firmament source only. Rebuild passes that text to `ModelSession.setSource`; the resulting SDK snapshot replaces the tree, mesh, properties, revision, and diagnostics. No Helios code constructs CAD geometry.

For scalar edits, the inspector calls `setProperty` with `{ value, unit }`. A live Chromium qualification changed Width from 50 mm to 80 mm and advanced revision 1 to revision 2. Entering -8 mm was rejected, left revision 2 and the 80 mm geometry visible, retained the invalid draft for correction, and displayed a diagnostic.

## Selection authority proof

The live assembly qualification clicked a rendered triangle. `resolveSelection` returned `assembly-instance:WebBlockPair.Fixed` and `face:3`; Helios selected **Fixed** in the tree and inspector and highlighted its occurrence. Tree selection uses `selectionForEntity` and highlighted the matching viewport occurrence.

## SDK friction

| Finding | Classification | Result |
|---|---|---|
| WASM runs on the page thread | Deferred by SDK | Async loading states are explicit; large rebuild blocking is documented. |
| Sheet metal is unavailable in browser X1 | Deferred by SDK | Category is visibly disabled; no fake UI semantics. |
| Property overrides leave source unchanged | Public policy | Inspector labels effective overrides and source remains visible. |
| `ModelSession` updates its snapshot in place | Worked around generically | The Helios adapter snapshots the public session object after rebuild so reactive views receive a new identity. |
| Runtime package is large | Deferred by SDK | Vite emits a chunk-size warning; package/runtime trimming is outside Helios X1. |
| Programmatic download was not observable through the in-app browser download hook | Qualification friction | The Helios download path is component-tested. The independent public-SDK sample generated and attempted to download 7,294 STEP bytes in Chromium. |

No Helios-only SDK backdoor was added.

## Themes and accessibility

Both themes use the same named tokens: application, panel, elevation, borders, primary/secondary text, accent/hover, selection, error, warning, success, viewport, grid major/minor, and default/selected/hovered geometry. Mars uses charcoal and green-gray chrome with brass accents. Sirius uses warm off-white surfaces, graphite text, cool grids, and the same semantic accent roles. Controls have visible focus, text labels, and sufficient state redundancy beyond color.

## Browser and performance qualification

Chromium exercised fit, semantic selection, tree/inspector synchronization, source and property editing, transactional invalid-edit recovery, assembly transforms, and both themes. Warm local observations were 362 ms for initialization plus compile, 288 ms for source rebuild, and 307 ms for property rebuild. These are development-machine observations, not performance promises. Rendering is event-driven: the Three.js renderer invalidates for camera, scene, resize, selection, hover, and appearance changes; damping schedules frames only while motion continues. There is no application-level perpetual render loop.

Firefox is not qualified. STEP download event capture remains a browser-harness limitation noted above, not an SDK diagnostic.

## Fresh-agent qualification

Two independent fresh-agent tasks completed before release closure:

1. The reference-editor agent added a broad semantic-kind filter derived only from public `ModelTreeNode.kind`, composed it with text search and ancestor preservation, and added focused tests. It found no public-contract blocker.
2. The independent-frontend agent did not read Helios implementation code. It built `samples/web-sdk-pocket-view/` from public SDK material alone using framework-free TypeScript, a 2D canvas renderer, semantic tree, mesh counts, and STEP export. Chromium compiled revision 1 with 1 definition, 1 occurrence, 1,016 vertices, 1,808 triangles, and generated 7,294 STEP bytes.

The independent implementation exposed two concrete SDK frictions: the canonical fixture's public semantic tree was coarse (model plus `AirHoleSimpleShaftMaterializer`), and the local runtime package installed about 61.9 MB of untrimmed assets. Neither required a private API or a Helios-specific backdoor.

The editor's non-test TypeScript/TSX/CSS implementation is 666 lines, excluding package/configuration files. The independent sample is intentionally separate and is not counted toward that budget.

## Evidence and reproduction

Actual Chromium screenshots were captured during qualification for Mars, Sirius, selected feature, source/properties, invalid-edit recovery, assembly, and assembly selection. Per-run binary screenshots remain local/conversation evidence under the generated-artifact policy rather than source-controlled product assets.

Reproduce with:

```powershell
cd demos/Aetherion.Helios
npm run sdk:install
npm test
npm run build
npm run dev
```

CLI ground truth:

```powershell
dotnet run --project Aetheris.CLI -- inspect fixtures/Canonical/WebSdk/editable-bracket.firmament --json
dotnet run --project Aetheris.CLI -- asm inspect fixtures/Canonical/WebSdk/shared-block-assembly.firmament --json
```

Release validation passed with 26 Helios component tests, both Helios and pocket-view production builds, and the complete .NET solution test run using `--maxcpucount:1`. A first fully concurrent solution run exposed the existing five-second fallback-test sensitivity under machine load; that exact test, its complete 977-test project, and the serialized full solution all passed on rerun. `Aetheris.FrictionLab.Tests` continues to report no discoverable tests, as it did before this frontend work.
