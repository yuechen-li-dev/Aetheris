# STEP import and inlineSTEP

Use [surface and trim inspection](surface-inspection.md) in Cadmata or Helios to compare filled geometry, BRep wire, translucent overlays and isolated source faces without changing the camera.

Direct STEP inspection is available through the native CLI/Cadmata workflows.
For mixed Part/Assembly products, prefer typed `include` or an
[Inventory Record](inventory.md). `Step("vendor/drive")` can supply an explicit
Assembly with its existing hierarchy and transforms. `InlineStep` is the local
annotation/surgery binding for one body; it can use `from: Components.housing`
or a direct path. An optional generated dependency lock supplies its source pin,
so authors do not need to write SHA-256 strings. See the Inventory guide for lock
commands, metadata and assembly imports' explicit source-PMI exclusion policy.

Helios **Import STEP** opens supported single solids as `InlineStep` parts and
admits assemblies with qualified solid components. A single-solid workspace
keeps the original as an immutable text asset, pins foreign STEP files, and
creates a Firmament annotation layer. An explicit assembly occurrence backed only by an open
surface can block assembly import. Use `aetheris analyze compound file.step --json`
to inspect the solid roots of such a file without reconstructing the hierarchy
or applying its occurrence placements.

In Helios, select an imported planar face to **Add datum**, or a cylindrical face
to **Add diameter**. Diameter authoring shows the measured value separately from
the requirement and requires explicit upper/lower allowances in millimetres.
Compilation checks the actual surface type and exports real AP242 geometric face
associations. Recognition labels are optional; direct `part.face("#191")` targets
use importer provenance, not STEP line order. Annotation changes do not edit the
imported geometry.

**Save workspace** downloads a `.helios.zip` containing the original STEP text
and Firmament source; **Open file** reopens it. Local checkpoints retain both too.
Cloud single-document saves cannot store these workspaces and refuse rather than
omit the asset. An unchanged `InlineStep` can retain an existing semantic PMI
layer, normalize its length measures to millimetres, and add explicit controls.
Foreign graphical glyphs are excluded with a warning; advanced semantic forms
can be retained in STEP without promising their 3D label projection. See
[PMI transfer and explicit GD&T](pmi.md#imported-semantic-pmi-and-units) for
admission limits. Reopen a saved workspace to preserve its editable source layer.

The browser SDK accepts `.step`/`.stp` text assets in `projectDocuments`:

```js
const source = 'Model Imported { units: mm InlineStep part { path: "body.step" } }';
const result = await cad.compile(source, {
  sourceName: 'part.firmament',
  projectDocuments: { 'body.step': stepText },
});
```

Paths resolve relative to the source document inside the explicit snapshot;
missing assets never fall through to native disk. The admitted source text,
SHA-256 and topology map remain bound to that compilation. Imported face picks
report `ImportedStable`, the source selector, surface kind and measured cylinder
diameter; these do not imply a native editable feature history.

GLB part export projects supported datum/diameter PMI into separate font-outline
and leader line primitives, surface witnesses and structured `asset.extras.pmi` records
(`aetheris/pmi-display/1`, millimetres). Body vertex/index buffers are preserved.
Datum leaders terminate inside the selected plane on a small gold patch. Diameter
leaders terminate on the analytic cylindrical wall, whose trimmed surface and
physical rims are highlighted in gold; parameter seams are not highlighted as rims.
A short surface-normal stem and arrow identify each attachment. The default body
appearance is lightly translucent during PMI inspection/export; explicit authored
appearances are retained. All markers are presentation only and do not intercept
engineering picks. `target` metadata records the witness geometry, normal and a
tiny `surfaceOffset` used only to prevent coincident-surface rendering artifacts.
The leader anchor stays on the exact support; engineering geometry is not offset.
Callout text uses static XY-plane outlines; the viewport legend remains readable when
the geometry text is edge-on. These are presentation assets, not AP242 semantics
or manufacturing approval.
Unsupported callout kinds/associations fail annotated GLB export explicitly;
an unavailable viewport callout is reported as a warning while STEP remains
authoritative. USDA, STL and OBJ do not carry this annotation layer.

Use `aetheris analyze part.step --json` to inspect STEP topology, analytic surface families, bounds, manifold assessment, and semantic PMI. When a part is easiest to understand by its manufacturing section, inspect it first with `aetheris analyze section part.step --xy|--xz|--yz --offset value --json`; use `--origin x,y,z --normal x,y,z` for an arbitrary construction plane and `--out section.svg` for deterministic visual evidence. This section-first path preserves analytic line/arc trims and reports closed-loop nesting instead of asking a tessellated view to carry design intent. Use `aetheris sheetmetal recognize` for bounded Sheet Metal recognition rather than treating generic topology analysis as manufacturing intent.

`analyze --json` also reports `splineRecovery`: counts of analytic, exact spline, recovered spline, and unsupported edge/face geometry; the worst measured recovery deviation; and the requested engineering tolerance. The default `ImportPolicy.RecoveryToleranceMillimetres` is 0.1 mm after STEP unit normalization. Rational curves and surfaces are recognized as analytic geometry where qualified; otherwise the importer attempts bounded generic spline recovery and records provenance on recovered geometry. The maximum deviation is sampled evidence.

For bound geometry, `pcurveQualification` reports the face-edge use count, source and reconstructed pcurves, failed uses, worst sampled 3D lift deviation, and surface-family counts. `qualified` means every coedge is bound and the independent pcurve validator accepts edge/surface agreement, endpoint sense, and loop closure. `inspectable-unqualified` means the body can still be inspected, but its recovered pcurves do not form a complete qualified trim set; STEP export then omits that partial set while retaining valid source pcurves. `ImportPolicy.PcurveQualificationToleranceMillimetres` defaults to 0.001 mm. A recovered curve or support can use a larger local allowance based on its measured geometry-recovery deviations, capped by the separate 0.1 mm geometry recovery budget. The CLI reports the observed lift deviation separately from geometry recovery deviation.

`summary.importQualification` distinguishes `Qualified`, `Inspectable`, `Degraded`, and `Failed` from authoritative BRep evidence. Parser/API success means geometry was retained; it is not a qualification verdict. `status` and `geometryStatus` report engineering qualification independently of the viewport. `displayStatus` separately reports `NotAssessed`, `Complete`, `RefinementLimited`, or `Partial`, with display reasons and missing face identities. Missing trim bindings remain `Inspectable`; detected invalid trims or incomplete intended solid shells are `Degraded` even when a useful preview is complete. Exact faces can receive derived face-local pcurves without being classified as recovered geometry.

An unchanged `InlineStep` can be annotated and re-exported when strict production
output checks pass, even when optional UV recovery is incomplete. Source pins
prove the identity of the imported data; they do not select a stricter operation.
Export checks topology bindings, imported shell enclosure, canonical representation
and 3D boundary consistency. For retained boundaries, the default allowance is
0.001 mm or the STEP file's declared distance accuracy, whichever is larger,
capped at 0.1 mm. The importer converts the declaration to millimetres. A refusal
reports the measured trim deviation and allowed distance where available.
Set `importTolerance` on a pinned `InlineStep` to choose a different budget.
For example, `importTolerance: 0.002mm` sets the boundary and normalization
budget to 0.002 mm. The automatic allowance applies to retained boundaries.
Normalization keeps its default 0.001 mm budget until you set an explicit value.
Export carries forward the source distance accuracy. Invalid UV traces are omitted
with `step-export-recovered-pcurves-omitted`; STEP uses its 3D edge curves and
vertices to define those boundaries. The source qualification report is retained.
`Remove`/`Replace` operations that depend on face-local trims keep their own
qualified-trim requirements. Tessellated-only source cannot enter this exact
part path through an automatic approximation fallback.

`aetheris canon` applies the same strict geometry boundary in both metadata modes:
`deterministic` selects deterministic headers; `production` preserves source
metadata. Both retain supported semantic PMI and report exclusions or omitted UV
traces in JSON `diagnostics` and text warnings. Neither mode promotes an open
surface into a solid. Open inputs remain available to `analyze` and inspection.
The [closed ellipse extrusion](../../../fixtures/Canonical/Boundary2/closed-ellipse-extrusion.firmament)
is a complete source witness with analytic elliptical boundaries and planar caps.

Legacy AP203/AP214 import uses the same topology and geometry path. Viewport quality is chosen automatically from model scale and bounded adaptive sampling. Viewport triangles are disposable approximations: STEP export, topology selection, and engineering qualification consume BRep directly. A convincing preview cannot qualify invalid engineering geometry. The frozen 26-file McMaster corpus and the original closeout evidence are recorded in [STEP-LEGACY-CLOSEOUT-X2](../../development/reports/2026-10/STEP-LEGACY-CLOSEOUT-X2.md); the subsequent visual investigation and ball roundtrip repair are in [STEP-LEGACY-FINAL-X3](../../development/reports/2026-10/STEP-LEGACY-FINAL-X3.md). Vendor files and generated meshes stay local; the tracked manifest retains the X0 baseline metadata and outcomes.

In Cadmata, expand **Surface / trim inspection** in the viewport. Compare **Surfaces only**, **BRep wire only**, **Translucent + BRep wire**, and **Face colors + BRep wire** without changing the camera. Red lines carry kernel edge identities; the wire and translucent views show them through surfaces. Select a patch from the list, or click a surface in an inspection view, to isolate its occurrence and face with the source STEP entity, support family, and boundary edges. STEP imports currently display BRep meshes, with no SDF field. These diagnostic controls change presentation only.

Dense rectangular spline trims retain native parameter grid cells as the existing `QuadCell` IR before final triangle lowering. Physical span lengths balance the grid rather than assuming equal U/V units mean equal lengths. Nearly coincident grid lines are merged only within a small sampled fraction of the display budget. Each retained quad is split along a deterministic valid diagonal. Axis and triangle caps can limit cell proportions; this path is bounded to rectangular trims and does not turn arbitrary trim bounding boxes into filled surfaces.

Dense nonrectangular spline trims reuse that knot-aware grid to decompose the actual outer-minus-holes region into local polygons. Existing constrained ear clipping triangulates each region before bounded chord/normal refinement. Only convex intersections of already validated seed triangles may use an anchor fan; general polygons never use an averaged centre. Periodic charts are normalized upstream, and display subdivision retains the original face and occurrence identity for picking. Quality limits remain explicit in display diagnostics. See [STEP-TRIM-TRIANGULATION-X4](../../development/reports/2026-10/STEP-TRIM-TRIANGULATION-X4.md) for the corpus and product evidence.

Rational source supports may be evaluated and displayed. Production AP242 uses
analytic or non-rational geometry after bounded recovery and independent boundary
validation. Native ellipsoid octants are the explicit exception: their exact STEP
projection uses rational quadratic patches. Arbitrary rational NURBS fallback is
not admitted, including through auxiliary PMI geometry. Intentional polynomial
B-spline curves and surfaces remain supported. Ordinary imported parts can use an
explicit recovery allowance of at most 0.1 mm; tighter source and application
requirements still matter for small features. Recovery reports sampled deviation
rather than claiming an exact mathematical error bound. Unsupported normalization
fails explicitly. Display mesh quality never supplies export evidence.

Firmament `inlineSTEP` consumes Aetheris-canonical AP242 on the bounded production path. Imported faces are selected by source entity identity, for example `body.face(#170)`, and recognized regions may supply stable semantic aliases. [`inline-step-cantilever.firmament`](../../../fixtures/Canonical/FEA/inline-step-cantilever.firmament) demonstrates FEA on a checked-in canonical STEP resource.

The single-body importer does not promise arbitrary containment, multi-root assemblies, or every AP242 surface family as one native body. `aetheris analyze compound part.step --json` is the exact inspection path for disconnected rigid roots: it reports each root independently and never silently selects or merges one. Multiple geometric roots alone do **not** establish product structure, occurrence transforms, mates, or interfaces. Use assembly import when the STEP product graph carries that semantics, and use Firmament assembly authoring when the intended component relationships must be stated explicitly. Unsupported containment in FEA reports `firmament-analysis-inline-step-containment-unsupported`; missing files and unresolved face identities are fatal diagnostics.

## What the reader accepts and what it refuses

The reader follows the ISO 10303-21 exchange structure. A file must begin with
`ISO-10303-21;`, hold at least one `DATA` section closed by `ENDSEC;`, and end with
`END-ISO-10303-21;`. A file that breaks this is refused with the reason: text that
never shows the start keyword is "not a STEP file"; a missing end or an unclosed
`DATA` section is reported as incomplete, usually truncated; an IFC building model is
named as such. Syntax errors name the entity, line and column where reading stopped.
Lists nested more than 256 levels deep are refused rather than read.

Accepted without change to the geometry, with an informational note where useful:
CR, LF or CRLF line ends; a UTF-8 byte-order mark; comments anywhere; lower-case
keywords and entity names; a missing or unknown `FILE_SCHEMA` (read as AP242, with a
warning); edition 3 `ANCHOR`, `REFERENCE` and `SIGNATURE` sections (skipped) and
named `DATA` sections; `&SCOPE` blocks; user-defined `!` entities and binary literals
(kept, not interpreted). Strings decode `\X\`, `\X2\`, `\X4\`, `\S\` and `\\`
escapes to Unicode.

Equivalent ISO 10303-42 forms are read as the form they are equivalent to:

- `BEZIER_*`, `QUASI_UNIFORM_*` and `UNIFORM_*` curves and surfaces take the knots
  their type defines.
- `POLYLINE` edges are the degree-1 B-spline through their points; a
  `TRIMMED_CURVE` trimmed by points uses the edge's vertices; a single-segment
  `COMPOSITE_CURVE` is its segment.
- `SURFACE_CURVE` and `INTERSECTION_CURVE` use their 3D curve; an associated surface
  in place of a pcurve is skipped and the pcurve is recovered.
- `RECTANGULAR_TRIMMED_SURFACE` and `CURVE_BOUNDED_SURFACE` under a face are their
  basis surface; `OFFSET_SURFACE` and `OFFSET_CURVE_3D` at distance 0 are their basis.
- `SEAM_EDGE` is read as the oriented edge it is; a `SUBEDGE` of an `EDGE_CURVE`
  uses its parent's curve between its own vertices.
- `FACE_SURFACE` is read as `ADVANCED_FACE`; `ORIENTED_FACE` is its face, reversed
  when its orientation is `.F.`.
- A `SHELL_BASED_SURFACE_MODEL` whose only shell is closed is read as the solid that
  shell bounds, with a warning.

Length units in metres, centimetres, inches, feet or any `CONVERSION_BASED_UNIT` are
converted to millimetres. A file that declares no length unit is read as millimetres
with a warning. A planar face the solid's shell does not list but that closes it is
adopted and reported as the repair `step-import-unlisted-faces-adopted`.

Refused, with a message naming the entity: curve and surface types the importer
does not evaluate (for example nonzero offsets, clothoids), composite curves of more
than one segment as an edge curve, and invalid values (non-finite numbers, zero
directions, wrong argument types, dangling or duplicate references). A file without a
`MANIFOLD_SOLID_BREP` or `BREP_WITH_VOIDS` says what it holds instead: open surface
models, faceted B-reps, constructive (CSG or swept) solids, wireframe, mapped items or
tessellated geometry.
