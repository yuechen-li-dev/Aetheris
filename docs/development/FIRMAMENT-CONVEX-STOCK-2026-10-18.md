# Firmament convex stock and exact ellipsoid qualification

Date: 2026-10-18. Outcome: **Success within the stated primitive and stock-fillet boundaries.**

## Implemented surface

- `Polyhedron<N>`: regular convex face counts 4, 6, 8, 12 and 20, with positive `edgeLength`. Numeric compile-time expressions and bound Static integer values use the existing value binder. Stock is centered on its origin.
- `Ellipsoid`: positive X/Y/Z semiaxes in `radii`. Stock is centered on its origin. An asymmetric egg is a separate construction.
- `EdgeFinish<Fillet>` with `target: AllEdges` and `radius`: one operation on untouched direct size-based Box or regular Polyhedron stock. No face selector. Box keeps its bottom-on-XY frame.

See the executable sources under `fixtures/Regression/ConvexStock/` and the updated public syntax guide.

## Geometry ownership

The existing mechanical AST binder, primitive lowering records and executor own admission/execution. There is no new source parser, whole-document dispatch route or source rewrite. Shared admission checks refuse arbitrary-body rounding and mixed edits before a stock-only lowerer can discard them.

`ConvexStockPrimitives` builds the five known regular vertex sets and their deterministic outward convex face lattices. For radius r, it offsets each stock plane inward by r and constructs the boundary of that inner polytope plus a radius-r ball: retained planar supports, cylindrical edge rolls and spherical vertex patches. All seams share topology and exact curve geometry. This is a deterministic construction, not a competition among blend strategies; it does not need a Judgment selector. Radius must be below the stock inradius so topology does not collapse. Original planar support planes remain; a rounded polyhedron's vertex extrema can recede. Box outer dimensions stay unchanged.

`EllipsoidSurface` is a native exact quadric octant. Eight patches share exact ellipse meridians/equator edges. STEP projects each patch to an exact rational quadratic net. The existing Judgment surface-recovery lane gains a bounded ellipsoid candidate: it verifies the canonical net, orthogonal axes and rational weights rather than fitting a nearby freeform surface. Native rational geometry policy is unchanged. An isotropic patch may recover to the simpler exact Sphere support.

SurfaceMeshIR retains shared edge sample identities and evaluates bounded convex spherical/ellipsoidal interiors on their native supports. Rounded Box cannot reuse the sharp Box CIR mirror; it projects the actual BRep. The legacy display path also handles the new native ellipsoid through the existing spherical trim owner under an affine pullback.

## STEP entity review requested during qualification

The STEP Tools AP242 EXPRESS definitions include an exact [`ellipsoid`](https://www.steptools.com/docs/stp_aim/html/t_ellipsoid.html), positioned by an axis frame and three positive semiaxes. It is included in [`csg_primitive`](https://www.steptools.com/docs/stp_aim/html/t_csg_primitive.html), so the user's observation is correct.

It is not a BRep face surface: [`advanced_face_geometry_select`](https://www.steptools.com/docs/stp_aim/html/t_advanced_face_geometry_select.html) admits B-spline, elementary and swept surfaces; [`elementary_surface`](https://www.steptools.com/docs/stp_aim/html/t_elementary_surface.html) does not include an ellipsoid. A `surface_replica` is also outside that advanced-face select. Therefore the CSG entity cannot simply replace the surface reference of the current `MANIFOLD_SOLID_BREP` export.

Decision: retain exact rational quadratic patch projection for the current BRep/PMI interchange path. This is an exact representation, not a fitted polynomial approximation. Direct `ELLIPSOID` output requires a deliberately qualified CSG representation and corresponding import/product/consumer support. This pass does not silently replace an editable BRep artifact with a CSG-only artifact or add an unrequested parallel export route.

## Admission and diagnostics

Unknown fields, non-length dimensions, unsupported regular face counts, nonpositive dimensions, excessive radii, malformed selectors and invalid variant values reject. The mechanical owner captures spans for primitive and Modify-field errors, separates explanatory text from stable codes and preserves severity. Validate and build share compilation admission; no alternate validator was added.

Whole-stock rounding on Cylinder, Ellipsoid, RoundedBox, imported/hollow bodies, profile construction, semantic Bounds/Pattern construction adapters and bodies with other edits is unsupported. Combined holes, repeated finishes, threading/replacement edits and all-edge chamfer reject. Existing local finish routes remain available under their existing limits.

Ellipsoid angular pcurves require a STEP-domain remapping before export. The primitive emits exact 3D edge curves without pcurves; an ellipsoid face carrying native angular pcurves rejects export rather than attaching them to incompatible rational patch coordinates. General ellipsoid face editing and pcurve succession are outside this primitive pass.

## Compatibility and code quality

Canonical constructs use PascalCase and fields use camelCase. Existing title-case field aliases remain admitted. Existing RoundedBox still means its original rounded silhouette and rim behavior. Existing enum values and the three-argument lowered Box record constructor/deconstruction are retained. Numeric generic substitution is restricted to the numeric Polyhedron owner; other generic variant/type names do not become scalar references.

No new regex, global diagnostic allowlist, mutable parser state, background service or product-side validator was introduced. New schemas use the existing generated metadata owner; completion projects those schemas.

## Qualification evidence

Generated commands, reports, STEP/OBJ files and logs are kept under ignored `artifacts/local/convex-stock/`.

| Gate | Result |
| --- | --- |
| Full Release solution build, serial, shared compilation disabled | Passed; 0 errors, 30 existing warnings |
| Focused CLI admission/export checks | 24 passed |
| Focused authoring, schema and original-token diagnostic spans | 31 passed |
| Fast Core lane (`Category!=SlowCorpus`) | 1,087 passed |
| Full solution lane, including slow interchange corpus and editable V8 | 5,629 passed, 0 failed, 0 skipped; 20 test assemblies |
| Previously qualified current domain STEP artifacts | 30/30 byte-identical SHA-256 |
| Historical domain frontend witnesses | 37/37 matched: 35 accepted STEP artifacts byte-identical and 2 expected rejections |
| Complete canonical corpus | 296 owner operations, 69 consumed modules and 4 explicit external-body boundaries; all 369 records qualified |
| FrontendX4 authoring/geometry corpus | 32/32 passed against the existing references and recorded geometry |
| SheetMetalSyntax | 26/26 passed; formed STEP / flat parity retained |
| SheetMetalSemantics | 29/29 passed |
| Prior exact-face / box-cage / sphere-cage compatibility | Validate, inspect and exact STEP or STL/GLB export witnesses passed |
| New stock fixtures through the real CLI | 12/12 validate, inspect, STEP build, STEP reimport analysis and OBJ export passed; all enclosed-manifold, connected, watertight and outward-oriented |
| Final repository/resource checks | `git diff --check` passed; no remaining .NET/testhost/MSBuild/VBCSCompiler/CLI hosts |

The new 43 regression cases exercise actual topology, exact curve/surface support, shared seams, manifold winding, preflight, STEP reimport, display mesh closure/orientation, volume controls, deterministic feature identity, static-value binding and validate/build rejection agreement. Exact geometry checks do not depend on matching diagnostic sentences. The generated semantic schema expected list is extended for the two new public constructs.

The asymmetric ellipsoid reimports with bounds `[-18,-12,-25]` to `[18,12,25]` mm, 8 faces, 12 ellipse edges and 6 vertices. The rounded 20mm Box retains `[-10,-10,0]` to `[10,10,20]` mm and has 6 planar faces, 12 cylindrical rolls and 8 spherical corners. `new-stock-summary.json` records the topology, bounds, integrity results and deterministic mesh hashes of all 12 specimens.

The exact commands were the serial Release solution build (`--no-restore -m:1 -nr:false -p:UseSharedCompilation=false`), the required fast and full test lanes, `scripts/qualify-firmament-domain-frontend.ps1`, `scripts/Test-CanonicalFixtures.ps1 -NoBuild`, and the existing FrontendX4/SheetMetalSyntax/SheetMetalSemantics runners without `--record`. The ignored `qualify-corpus.ps1` driver and log files preserve the CLI commands and final results. Existing fixtures, geometry references and recorded expectations were not rewritten.

## Verdict

The motivating die stock and oval stock now compile through the unified admission path to exact native BRep geometry. Whole-stock fillets construct all edge and corner patches together within the explicit Box/regular-Polyhedron boundary. Invalid or unsupported intent rejects through validate and build rather than being omitted. Existing full regressions, canonical authoring and sampled byte-for-byte geometry controls pass. This does not qualify general arbitrary-body blending, CSG interchange, general ellipsoid editing or interactive Helios behavior.
