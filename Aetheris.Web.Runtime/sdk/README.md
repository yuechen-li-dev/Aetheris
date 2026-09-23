# @aetheris/cad

Browser-embeddable Aetheris CAD. The package includes the .NET WebAssembly runtime and uses the same Firmament, B-rep, tessellation, assembly, and AP242 authorities as native Aetheris.

```js
import { Aetheris } from '@aetheris/cad';
const cad = await Aetheris.create();
const { model, diagnostics } = await cad.compile(source, { sourceName: 'bracket.firmament' });
```

Vite projects add `aetherisCad()` from `@aetheris/cad/vite` to `vite.config.js`. The plugin serves the runtime during development and copies it into production builds.

## Selection correspondence

`model.describeSelection(definitionId, triangleIndex, occurrenceId)` returns the picked face's runtime ID, semantic topology ID when known, source addressability, canonical selector when admitted, source range, and build revision. `model.selectionForSemanticId(id)` and `model.selectionForSourceSymbol(symbol)` find display ranges in the current model. Use these ranges only with the current model revision.

Geometry provenance is assigned during Firmament parsing and BRep construction, then copied to display patches. The browser does not identify source features from face shape, tessellation indices, or STEP entity numbers. `model.describeEdgeSelection(...)` reads the same construction metadata for true BRep edges; `model.geometrySourceMap()` dumps the current face and edge records for inspection. A source range can be available even when Firmament has no qualified selector for that topology.

For a single, unmodified Firmament `Box`, the Web runtime tessellates the canonical BRep directly. Construction-owned face IDs map its six faces to `face(±X)`, `face(±Y)`, and `face(±Z)`. A single simple through-hole also retains its construction-owned wall and rim identity through direct display, but the wall and rim have no qualified Firmament selector yet. Display definitions include BRep edge polylines for a truthful edge overlay; these are not source selectors. The STEP export remains available. Other routes that reimport STEP currently return `RuntimeOnly` display faces with no source selector. A display `faceId` or triangle index is never itself a Firmament selector.

X1 runs on the page thread. `worker: true` fails with the typed `worker-unavailable` error; a qualified Worker transport is deferred rather than silently changing behavior.

The preview package is AGPL-3.0-only. Contact the Aetheris project for commercial licensing; this statement is package metadata, not legal advice.
