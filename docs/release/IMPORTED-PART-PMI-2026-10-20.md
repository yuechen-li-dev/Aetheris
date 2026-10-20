# Imported Part + PMI — implementation and qualification

**Success for the bounded canonical single-part workflow.** Full qualification
passed; unsupported import/annotation families remain explicit stops below.

## Scope

Implements the bounded follow-up to the Helios STEP/PMI human-workflow audit.
The original observations remain in the companion Helios repository's
`docs/release/HUMAN-WORKFLOW-STEP-PMI-2026-10-20.md`.

This is canonical single-part `InlineStep` annotation, not general vendor STEP,
assembly import, geometry healing, arbitrary GD&T authoring or manufacturing
approval. Imported BRep and its original text asset remain separate from the
editable Firmament annotation layer.

## Repairs and ownership

- Imported PMI now carries source STEP entity -> imported BRep face -> exported
  STEP entity associations. It no longer stores a resolved target only in prose.
  The existing importer owns provenance; the existing exporter owns emitted IDs.
- Imported topology maps use `FaceGeometryBinding.SourceStepEntityId`, including
  when entity records occur in a different textual order. Face ordinals are not
  guessed from a regular expression over file lines.
- The existing parser/compiler accepts explicit project text resources.
  `.step`/`.stp` assets resolve relative to the source document, never falling
  through to disk when a snapshot was supplied. The admitted text and its UTF-8
  SHA-256 are retained in the `InlineStep` record; lowering does not reopen it.
  The retained text is excluded from diagnostic JSON serialization.
- Language analysis receives the same snapshot. Assembly dependency documents
  retain analysis through their project root, so a part cannot lose access to
  included Templates while an imported standalone part gains asset resolution.
- Imported face picks expose `ImportedStable`, source entity selector, surface
  family and measured cylinder diameter. Datum/diameter export checks the actual
  plane/cylinder support. A requirement is distinct from a measured nominal.
- Helios adds Import STEP, face-driven Add datum/Add diameter, explicit asymmetric
  allowance inputs, linked source nodes, read-only asset/hash/qualification
  inspection, and portable `.helios.zip` save/reopen. Existing checkpoints retain
  text assets in the same project snapshot; no second history store was added.
- Shared AP242 readback projects supported PMI into separate annotation curves.
  WebGPU and WebGL use shared Telos adapters; GLB appends separate line primitives
  and `asset.extras.pmi` metadata with `aetheris/pmi-display/1`, millimetres and
  target STEP entity references. The engineering body's buffers are unchanged.
- Callouts use the existing font reader's raw contours. Reusing engineering text's
  glyph-region Boolean normalization initially stalled a browser diameter edit;
  presentation no longer performs that unnecessary construction. CAD text keeps
  its existing material normalization path.
- Finite annotation lines participate in whole-model framing. Reference grids
  do not. Toolbar wrapping prevents narrow viewports from overlaying controls.
  The graph counts authoring steps rather than calling PMI a physical part.
- Worker failure is independent of pending work, preserving recovery controls.
  Input classification rejects unsupported root files before creating a project.
  Local archive paths and import sizes are bounded.

## Real human-facing witnesses

Used the real rebuilt browser SDK and current Helios, ordinary native file picker,
viewport face picking, compiler edit transactions, actual downloads and reopen.
The browser extension's upload API required file-URL permission; no permissions
were changed. Native picker use is harness fallback, not a product capability.

### Through-hole control

Input: `testdata/firmament/inline-step/canonical-through-hole.step`.
Picked planar face #102 for datum A and cylindrical face #191 for Bore.
Authored Ø2 mm with explicit +0.1/-0.05 mm allowances, as projection-test values.
These allowances are not inferred process capability or production approval.

Saved a portable workspace, reloaded the app and reopened that downloaded file.
Both requirements and the original asset survived. Final reopen built in about
1.12 s; STEP + GLB export took about 0.70 s in the observed development runtime.
These are smoke timings, not a controlled benchmark.

Independent CLI inspection of the actual downloaded STEP finds datum and diameter
face associations, nominal 2 mm and both exact allowances. All **154** baseline
entity records remain identical. Analyzer summary, face, edge and vertex records
are identical. The retained asset SHA-256 equals the original fixture's hash.

Opened the actual downloaded GLB in a separate viewer using Three's GLTFLoader,
without Firmament compilation. It decodes three meshes (one body, two callouts),
two structured PMI records and readable datum/diameter text with leader geometry.
The automated export regression additionally compares the body's binary payload
before/after annotation and requires a byte-identical prefix.

### Original L-bracket motivating input

Input: `fixtures/Compatibility/Firmasm/LegacyAssembly/examples/occt-l-bracket/_part_003_l_bracket.step`.
The real UI imported the bracket, picked planar #311 and cylindrical #569,
created datum A and MountingBore Ø10 +0.1/-0.05 mm, then saved and exported.
Observed import about 2.64 s; the final annotation build about 3.01 s and export
about 0.92 s. All **550** baseline STEP entity records remain identical in the
downloaded annotated STEP. Both annotations have real face associations.

Both historical inputs currently carry the importer's **Degraded** geometry
qualification, visibly retained in the inspector. Successful annotation and
unchanged geometry do not upgrade that assessment. This pass performs no healing.

## Initial MVP qualification

- Focused imported topology/PMI/resource/export, text-profile and cross-file
  composition analysis: **85 passed**.
- Core fast lane: **1,087 passed**.
- Browser SDK transport/selection tests: **18 passed**.
- Shared Telos tests: **16 passed**.
- Helios tests: **126 passed**; development bundle/typecheck passed.
- Full solution: **5,736 passed across 20 projects; zero failed or skipped**.
  Final serial run: `artifacts/local/imported-pmi-final-full-tests.log`.

Commands use Release, `-m:1 -nr:false -p:UseSharedCompilation=false` for builds,
and `RunConfiguration.MaxCpuCount=1` for the full solution tests. Earlier runs
found and repaired a stale one-mesh export assertion, an Assembly analysis
regression and a wall-clock-sensitive synthetic refinement-warning test.
The latter now starts from deterministic complete tessellation before injecting
its warning, instead of accidentally testing a five-second partial-display stop.

Raw ignored evidence: `artifacts/local/imported-pmi-implementation/`, including
actual workspace/export archives, source/assets, CLI analyses, parity summaries,
`independent-glb-view.jpg` and `bracket-workspace.jpg`. The primary bracket's
workspace and exported STEP have the same basename, so they are inspected in
separate `bracket/workspace/` and `bracket/export/` directories.

## Engineering presentation refinement

The first MVP preserved semantic data but placed leaders at the first tessellation
vertex. That often meant a datum on a three-plane corner and a hole callout that
looked unattached inside the solid. Data preservation alone did not make those
graphics unambiguous.

The shared AP242 display projector now supplies explicit surface witnesses:

- **Datum:** an interior point and a small square patch on its selected plane.
  The patch fits inside a trimmed display triangle's incircle, avoiding hole voids,
  concavities and the arbitrary first vertex. A normal stem and arrow terminate
  at that point.
- **Diameter:** an interior UV-triangle point evaluated on the analytic cylinder,
  not a tessellation chord or rim vertex. The associated trimmed wall and physical
  rim wires receive gold highlighting. Doubly used parameter seams are excluded
  from the rim highlight. The normal stem is bounded by the cylinder radius.
- **Presentation:** gold witnesses, restrained wide-line halos in WebGPU, and
  78% default body opacity for PMI inspection. Explicit authored appearances are
  retained. GLB contains its own gold/emissive marker materials and translucent
  default body material; these are not styling added by the qualification viewer.

The additive `target` field in `aetheris/pmi-display/1` carries marker kind, normal,
stem length and geometry. Shared Telos WebGPU and transitional WebGL adapters
project the same compiler geometry. Filled markers and their wires cannot intercept
engineering picks. WebGL marker meshes/materials have explicit disposal. Body
buffers and STEP associations are unchanged. Multi-face requirements without an
implemented multi-face presentation reject explicitly rather than marking only
the first association.

Independent GLB readback exposed intersecting approximations: the browser body
used 32 cylinder segments while the original PMI projector used 24. The body
producer now retains its sampling budget for the projector and GLB exporter;
24- and 32-segment regressions require marker vertices to correspond to actual
body display vertices. This bookkeeping is excluded from the serialized mesh
contract. The projector also lifts only marker graphics a tiny distance
along the oriented surface normal, bounded by text/marker size and cylinder radius.
The additive `surfaceOffset` records that distance in millimetres. Leaders retain
their exact support anchors. Regression checks remove this recorded offset before
proving the markers lie on the selected plane/cylinder and within planar material.
The same offset geometry is shared by both viewport adapters and GLB.

The independent viewer fits camera clipping to the exported model's metre-scale
bounds. Its initial 0.01 mm near / 1 km far clip range discarded close-up depth
precision and produced additional striping. Standard glTF depth occlusion still
applies: orbit to inspect a far-side attachment, and use the legend when static
text is edge-on. The qualification viewer changes camera framing/clipping only;
it adds no PMI, materials, transparency or target styling to the downloaded asset.

These witnesses identify referenced **surfaces**. This pass does not add an
edge-target PMI authoring family or imply that a cylinder parameter seam is a
manufactured edge. Text still occupies a static plane. Markers are display
approximations on already admitted geometry; they do not improve the importer's
engineering qualification or constitute a released manufacturing drawing.

### Refinement qualification

- Focused source/PMI/export and surface-attachment regressions: **11 passed**.
  Checks cover interior planar material, analytic cylinder radius, normal stems,
  physical rims without a parameter seam, sampling parity at 24/32 segments,
  exact semantic values and byte-identical body-buffer prefixes.
- Core fast lane: **1,087 passed**. Full solution: **5,739 passed across 20
  projects, zero failures or skips**, including **3,000 Firmament** tests.
  Log: `artifacts/local/pmi-target-qualified-full-tests.log`.
- SDK: **18 passed**. Shared Telos: **18 passed**, including marker pick exclusion,
  geometry/material disposal and preserving authored appearance. Helios:
  **126 passed**; final SDK packaging and Helios bundle/typecheck passed.
- Real saved plate and L-bracket workspaces reopened in Helios, displayed gold
  targets and exported STEP + GLB with no diagnostics. CLI admission is valid.
  All **154 / 154** plate and **550 / 550** bracket baseline STEP entity records
  remain identical. Plate topology remains **7/15/12** faces/edges/vertices;
  bracket topology remains **16/42/28**. Source asset hashes are unchanged.
- The downloaded plate GLB was decoded independently and inspected in isometric,
  label-plane and reverse views. The bracket GLB additionally verifies its
  nonhorizontal datum and differently oriented bore. No compiler or private
  product state is available to that viewer.

Current ignored evidence is under `artifacts/local/pmi-surface-markers/`:
actual STEP/GLB exports, CLI analyses/admission, `qualification-summary.json`,
`helios-final.png`, the three plate GLB views, and bracket UI/GLB screenshots.
Temporary viewers and the development server were stopped; no .NET test hosts
remain. **Verdict: Success** for this bounded surface-attachment refinement.

## Boundaries and next work

- Existing semantic PMI in a newly imported STEP is explicitly rejected rather
  than stripped. Reopen its original workspace to continue. A later imported
  annotation-layer adapter must preserve existing requirements and associations.
- Arbitrary vendor/assembly import remains behind the existing canonical input
  boundary. This work does not certify kernel interoperability beyond that scope.
- The first purpose-facing authoring form covers planar datum and cylindrical
  diameter. Existing graph/source transactions edit created records; complete
  GD&T, distance, angular and coordinate-dimension authoring are future slices.
- GLB callout text is static XY-plane outline geometry, not camera-facing
  billboards or a production drawing layout. The viewport legend remains readable
  when a static label is edge-on. AP242 remains semantic engineering authority.
  This metadata is not a standardized glTF semantic PMI extension.
- An unsupported preview projection warns without rejecting admitted geometry;
  annotated GLB export fails explicitly. USDA/STL/OBJ do not carry PMI.
- Local portable workspaces and checkpoints retain assets. Cloud's single-source
  save contract explicitly refuses multi-file workspaces.

## Code-quality review

Reused parser, binder, imported topology provenance, AP242 writer/inspector,
language edits, project snapshots, checkpoints and Telos. No parallel importer,
frontend validator, new parser route, rewrite pass, global asset state or second
field schema was introduced. The obsolete regex face-order inference was removed.
Geometry tests require source/association/geometry evidence, not message strings
alone. Public import/export documentation and SDK guidance describe the admitted
scope and remaining stops.
