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

## Qualification

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

## Boundaries and next work

- Existing semantic PMI in a newly imported STEP is explicitly rejected rather
  than stripped. Reopen its original workspace to continue. A later imported
  annotation-layer adapter must preserve existing requirements and associations.
- Arbitrary vendor/assembly import remains behind the existing canonical input
  boundary. This work does not certify kernel interoperability beyond that scope.
- The first purpose-facing authoring form covers planar datum and cylindrical
  diameter. Existing graph/source transactions edit created records; complete
  GD&T, distance, angular and coordinate-dimension authoring are future slices.
- GLB callouts are static XY-plane outline/leader geometry, not camera-facing
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
