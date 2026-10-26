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

Standalone `InlineStep` parts also accept `.step`/`.stp` text in `projectDocuments`.
Resolve `path` relative to `sourceName` inside that snapshot. The existing
canonical-input checks apply; arbitrary vendor/assembly import and existing
imported semantic-PMI layers are not admitted by this slice. The immutable
admitted text, content hash and importer face provenance are retained. Imported
face selections expose `ImportedStable`, `part.face("#entity")`, `surfaceKind`
and `diameterMm` for cylinders. Source addressability is distinct from imported
topology addressability: there is no synthetic native feature history.

`model.mesh.importedAsset` exposes asset path, SHA-256 and import qualification.
`model.mesh.annotations` projects AP242 datum/diameter requirements with exact
values, allowances and separate font-outline/leader curves in Z-up millimetres.
Its additive `target` contains surface-marker geometry, a normal and stem length:
an interior plane patch for datums or the associated cylindrical wall and physical
rims for diameters. A disclosed `surfaceOffset` lifts only marker graphics to avoid
coincident-surface striping; the leader anchor stays on the exact support.
Parameter seams are not rim markers. Shared Telos WebGPU/WebGL
adapters render the gold witnesses without intercepting engineering picks; the
default body appearance is 78% opaque when PMI is present, preserving authored
appearances. Part GLB export retains
the body buffers and appends separate annotation nodes plus structured metadata
under `asset.extras.pmi` (`aetheris/pmi-display/1`, `pmiUnits: mm`). An unsupported
callout projection warns in the viewport and rejects annotated GLB export;
STEP remains the engineering authority. USDA/STL/OBJ do not project PMI.

`cad.language.analyze(source, { sourceName, sourceRevision, projectRoot,
projectDocuments })` uses the existing snapshot-aware Assembly analysis when
the root is an Assembly. This resolves included ports without touching disk.
Single-document analysis remains available; declaration modules without a
project context retain the explicit context-required informational diagnostic.

`cad.language.outline(source, { sourceName, sourceRevision, projectRoot,
projectDocuments })` returns the document's declaration graph for visual editors
(`firmament-outline/1`). Every declaration is a node whose `id` is its name path
(`MotorMount/Modify:Body/Pilot`); a name shared by siblings is qualified by kind,
and a repeat of both carries an ordinal. Ids depend on names, kinds and nesting,
never on offsets. Nodes carry their authored fields with source spans. Edges are
resolved name uses (`field:Profile`, `clause:Using`, `clause:Over`, a Pattern's
Feature call, `Modify X` naming its body) found by lexical scope. With
`projectDocuments`, names the document does not declare are resolved against the
other documents' top-level declarations and carry `toDocument`. Uses nothing
answers are listed as `externals`. The outline is source structure, not
admission: the build still decides whether a use is valid.

`cad.language.edit(source, operations, { sourceName, sourceRevision })` applies
structural edits addressed by outline ids and returns the edited `text`, the one
replacement (`edit.start`, `edit.length`, `edit.replacement`) that turns the
original into it, the outline id each operation left behind in `nodes`, and
`warnings`. Operations are `setField` (replace a value or add the field),
`removeField`, `insertDeclaration` (`parent`, a `declaration` spec, and `after`
or `first`), `removeDeclaration` (warns about uses that stop resolving),
`renameDeclaration` (renames every use in the document; uses in other project
files are not renamed) and `moveDeclaration` (among its siblings). Each edit is
a splice placed by the syntax tree, so comments and layout elsewhere are kept,
and an edit whose result adds a syntax error is refused with an
`edit-*` error code. `insertDeclaration` also takes a declaration as `source`
text, with `name` replacing its declared name; `insertFeature` (`node`, `source`,
`name`) puts a feature in the body's `Modify`, opening one after the body when
there is none. Whether the edited model builds is still the compiler's
decision.

Inside a body, a bare call such as `MountHole(center: Point2(10mm, 0mm))` is an
outline node with `call: true`: its `kind` is the callee and its `fields` are the
arguments, so `setField`/`removeField` edit arguments and `moveDeclaration`/
`removeDeclaration` place and remove it like a feature; `insertFeature` also takes
a call as `source`. Named applications also have `call: true`, their declared
`name`, and argument fields, with outline ids that survive reordering.
Value declarations carry `value` (the initializer, for a
parameter sheet) and `annotation`; Features carry `signature`.
`extractFeature` (`node`, `name`, `parameters`) turns a feature into a
document-level `Feature` whose chosen fields become typed parameters defaulting to
their current values, and leaves a call with those values in its place: the
model builds the same geometry. Extraction writes a named application using the
original declaration name, so
existing references, including a Pmi `Target`, continue to resolve.

`insertFeature` also accepts a bare recipe call as `source`, for example
`MountHole(center: Layout.leftMount)`. It writes `Feature name = Recipe(...)`
into the source. Pass a meaningful `name`, or omit it to allocate an unused
camelCase name in that Modify; existing application names are preserved.
`nameFeature` takes an anonymous call `node` (or its exact authored `span`) and an optional
`name`, and promotes it to the same named form. Use spans from the current source
revision. Duplicate names and spans that do not identify a call reject. Update
authored selectors that referenced its old ordinal-generated name after promotion.
The returned `nodes` contains the resulting named outline node.

Construct projections for Feature-generated holes expose
`featureApplicationIdentity`, navigate to the original call where provenance is
available, and keep expanded fields read-only. Their ordinary admitted output
selectors remain available. This does not confer persistent identity on arbitrary
faces after topology changes.

`cad.language.palette()` lists what a visual editor can add
(`firmament-palette/1`): bodies (`placement: 'model'`) and features on a body
(`placement: 'feature'`), each a declaration `source` with starting values.
Every item is proven to build by the kernel's tests.
Body starters have `requiresEmptyModel: true`: they start a Model without an
existing material body. Separate objects belong in Parts of an Assembly;
adding an independent second body to an occupied Model rejects at that
declaration. A referenced legacy `with` prototype is compile-time data and
does not count as a second output body. Hole centers remain explicit source
values; the palette does not invent non-overlapping placements.

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


Compiled Assembly snapshots include `motions`, with clip names, durations,
repeat modes and realized Mate targets. `await model.sampleMotion(name, seconds,
{ preserveEndpoint: false })` returns occurrence transforms and scalar joint
state at the current successful revision. Sampling uses the retained compiler
graph, reuses the existing mesh definitions, and does not rebuild geometry or
change the engineering STEP snapshot. Consumers own their playback clock and
apply the returned occurrence matrices. An unsuccessful source rebuild retains
the previous admitted graph together with the previous geometry.

Compiler completion and `model.describeConstruct(semanticId)` return fields
applicable to the active Hole variant. The raw `cad.language.schema()` is a
union schema: its optional field `variants` array restricts applicability;
an absent or empty array means common to all variants. Schema-driven controls
must honor this metadata or request completion. For example, `CounterboreDepth`
applies to `Counterbore`, never `Shaft`. Part build diagnostics use the shared
compiler presenter, retaining stable error codes and authored source locations.

### Profile workspace projection

`cad.language.sketch(source, profileNodeIdOrNull, { sourceName, sourceRevision })`
returns `firmament-sketch/1`: the authored Profile choices, resolved local analytic
curves, construction frame, source provenance, and dimension controls. A null id
chooses the first authored Profile. The validation level is
`ProfileResolvedNotBuilt`; this query does not construct or admit a solid.

`cad.language.sketchEdit(source, request, options)` prepares a normal
`LanguageEdit` for `dimensions`, `create`, or a qualified circular `opening`.
Dimension changes use the returned control ids and numeric values in their
displayed units. Read-only expressions/tolerances cannot be replaced through
this operation. A directly named literal Static is updated at its initializer.
The caller must compare source revisions before applying the edit and then use
the normal compile/rebuild transaction. A refused edit returns its stable SDK
error code and explanation, with no source mutation.

This first contract addresses authored Profiles in one document. It does not
rewrite included/generated declarations. See [the authoring guide](../../docs/public/firmament/sketcher.md).

The additive `construction` projection exposes named points, bound guides, source
elements, literal dimension controls and layout frames even before a Profile
exists. Its `ConstructionResolvedNotTraced` level is not solid admission.
`sketchEdit` also accepts `{ kind: 'construction', construction: operation }`.
Typed operations create a layout, point, line, circle, Path, Path step, trace or
extrusion; `movePoint` and `dimension` edit literal authoring inputs. They reuse
the structural writer and existing Profile resolver. Keep intermediate source
in a host-owned draft transaction and compile the complete candidate before
committing. Preserve the preceding source and built model on refusal.

`construction.shapes` retains typed closed shapes and reuses the existing driving
dimension controls even before a Profile exists. `construction.spans` provides
selectable rectangle-edge intervals between existing named landmarks. It does
not invent intersections or flatten the source into editable segments.
`rectangle` creates an independent Rect2 or a top-right inset driven by a reference
rectangle; `parameter` changes a literal shared Length input. The existing edit
transport also accepts `constructionDimensions` with the projected control ids.
Connected span entries use `connect: true`; `wholeLoop: true` selects a complete
rectangle outline. `targetProfile` explicitly replaces an authored Profile after
the replacement resolves. All these edits remain staged until normal compilation.

### Pending graph authoring

`language.edit` also prepares `replaceDeclaration`, `patternFeature`,
`extrudeProfile`, `pocketProfile`, and `setPlacement` operations. These operate on
the existing structural AST. Preparing an edit does not admit its geometry:
compile the candidate in a separate model handle before committing its exact
source splice, and dispose that preview when controls change or it is cancelled.
Keep revision checks and source undo history in the host.

`patternFeature` currently repeats a field-based Hole in Modify, keeping the seed
and keyed row/column copies. `pocketProfile` targets Compose or qualified plain
Box/Extrude stock. `extrudeProfile` changes the extrusion end while preserving
its authored start. Full compilation remains the capability authority.

Outline Placement nodes expose optional `placementControls`: literal
Origin-to-Parent translation and one-axis rotation, or a disclosed read-only
reason. General matrices, expressions and named-frame relationships must not be
flattened into editable numbers. Support picking similarly requires an actual
compiler-owned selector; display face identity alone is insufficient.


The `construction` edit action `shape` authors a retained `Rect2`, `Circle2`,
`Ellipse2`, `Slot2` or `RoundedRect2` in a Concept Struct. The normal closed-boundary
owner validates fields and dimensions. Construction inspection projects public
member names from that lowerer's own member map and retains authored source
provenance. A single `wholeLoop` trace entry can consume any projected closed
shape. Workspace visibility is a client presentation choice; it never rewrites
source or changes engineering admission.
