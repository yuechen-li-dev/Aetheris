# @aetheris/cad

Browser-embeddable Aetheris CAD. The package includes the .NET WebAssembly runtime and uses the same Firmament, B-rep, tessellation, assembly, and AP242 authorities as native Aetheris.

```js
import { Aetheris } from '@aetheris/cad';
const cad = await Aetheris.create();
const { model, diagnostics } = await cad.compile(source, { sourceName: 'bracket.firmament' });
```

Vite projects add `aetherisCad()` from `@aetheris/cad/vite` to `vite.config.js`. `npm run build` builds the fast non-AOT development package. `npm run build:production` also publishes the Worker runtime with .NET browser WebAssembly AOT (`RunAOTCompilation=true`, `WasmStripILAfterAOT=false`) using the installed Microsoft `wasm-tools` workload. The production package contains both `runtime/` for page-thread language assistance and `runtime-aot/` for geometry in the Worker. A production Vite build requires that AOT package and copies both runtimes; Vite dev and a development-mode build use only the non-AOT runtime. The caller's `wasmUrl` option remains an explicit override.

## Selection correspondence

`model.describeSelection(definitionId, triangleIndex, occurrenceId)` returns the picked face's runtime ID, semantic topology ID when known, source addressability, canonical selector when admitted, source range, and build revision. `model.selectionForSemanticId(id)` and `model.selectionForSourceSymbol(symbol)` find display ranges in the current model. Use these ranges only with the current model revision.

Geometry provenance is assigned during Firmament parsing and BRep construction, then copied to display patches. The browser does not identify source features from face shape, tessellation indices, or STEP entity numbers. `model.describeEdgeSelection(...)` reads the same construction metadata for true BRep edges; `model.geometrySourceMap()` dumps the current face and edge records for inspection. A source range can be available even when Firmament has no qualified selector for that topology.

For a single, unmodified Firmament `Box`, the Web runtime tessellates the canonical BRep directly. Construction-owned face IDs map its six faces to `face(±X)`, `face(±Y)`, and `face(±Z)`. A single simple through-hole also retains its construction-owned wall and rim identity through direct display, but the wall and rim have no qualified Firmament selector yet. Display definitions include BRep edge polylines for a truthful edge overlay; these are not source selectors. The STEP export remains available. Other routes that reimport STEP currently return `RuntimeOnly` display faces with no source selector. A display `faceId` or triangle index is never itself a Firmament selector.

`Aetheris.create({ worker: true })` hosts the same runtime in a dedicated module Worker. Its mesh arrays and STEP bytes cross the boundary as transferable buffers. `cad.workerTiming` and `model.workerTiming` expose the last Worker operation's execution time, message delay, and transferred buffer bytes. Terminate a wedged Worker with `cad.terminate()` and create a fresh instance; termination rejects outstanding and future requests on the old instance. The .NET 10 runtime needs a message event listener rather than an assigned `onmessage` handler during Worker initialization.

The preview package is AGPL-3.0-only. Contact the Aetheris project for commercial licensing; this statement is package metadata, not legal advice.

## Project snapshots and material projection

Assembly and Scene source compile through their shared project owners. Supply all referenced
dependency documents explicitly, using unique project-relative paths:

```ts
const { model, diagnostics } = await cad.compile(sceneSource, {
  sourceName: 'house.firmament',
  projectDocuments: {
    'components.firmament': componentSource,
    'living.firmament': livingAssemblySource,
  },
});
```

Missing resources fail with a named diagnostic and never fall through to native
disk. `model.setSource` accepts the same `projectDocuments` option for resource
updates and otherwise retains the existing resource set. Definitions are shared
across occurrences, with stable geometry revisions. Occurrences carry world
transforms and resolved materials; Scene output also carries millimetre bounds,
environment boundaries and named cameras with poses/look-at/FOV. Scene is display
composition and does not provide a synthetic engineering STEP export.

`cad.language.analyze(source, { sourceName, sourceRevision, projectRoot,
projectDocuments })` uses the existing snapshot-aware Assembly analysis when
the root is an Assembly. This resolves included ports without touching disk.
Single-document analysis remains available; declaration modules without a
project context retain the explicit context-required informational diagnostic.

Production AOT preserves the semantic provenance record metadata used by the
reflection JSON bridge. Package generation clears its owned AOT publish output
before publishing, so obsolete fingerprints are not included in the tarball.

Optional definition `cir` metadata carries compiler qualification, structural
identity, typed field source and bounds. Runtime BRep and CIR ASTs do not cross
JSON. Display projection lazily resolves qualified definitions through the managed
Copeland VD-MIR/direct-WGSL backend. Optional `shader` contains the deterministic
artifact, compiler/source identities and binding state. The shared Telos adapter
automatically creates a field with an invisible engineering picking proxy and
visual mesh fallback until GPU validation succeeds. Generation, missing transport
and GPU failures remain separately inspectable; imported STEP and unsupported
geometry keep the mesh path.

The native and development WASM builds use one exact managed-backend package pin.
Before restoring a fresh checkout, run `scripts/prepare-managed-wgsl.ps1` with an
explicit `-CopelandRoot` matching `scripts/managed-wgsl-source-pin.txt`. This packs
the existing producer into the ignored local feed; runtime execution has no
sibling-checkout, DXC, Naga or subprocess requirement. No package publishing is
required. Development Vite runtime assets use `no-store` and explicit lengths;
production preview retains its existing fingerprint cache policy.

Shared Assembly/Scene face ranges carry BRep face IDs. A range without an owned
feature semantic ID uses `semanticEntityId: null`; selection resolves through the
picked occurrence. This does not grant a source selector to a runtime-only face.
