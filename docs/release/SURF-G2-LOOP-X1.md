# SURF-G2-LOOP-X1 — Polynomial contacts and trimmed G2 plateaus

## Verdict: Accepted within the explicit X1 construction

Aetheris carries the qualified quintic section around a closed line/polynomial footprint through ordinary Firmament `Plateau` authoring, creates shared trimmed topology, and exports/reimports a manifold STEP solid. The generic witness passes before phone burn-in. The phone body and plateau are now one solid. No Boolean union, phone-specific kernel branch, or redesigned footprint/section law is involved.

The admitted construction is **homothetic**, explicitly selected by `Inset: Homothetic`. Width is the short-axis inset; the long-axis inset scales proportionally. This is exact polynomial geometry, not a constant-normal-distance offset or a recovery of Apple's hidden surface network. The old implicit `EdgeFinish Profile: CurvatureContinuous` diagnostic remains: that syntax does not supply the explicit parallel-support inputs of Plateau.

## Audit and minimum extension

1. **Existing local contacts:** `BrepBoundedFillet` locates the orthogonal concave corner in its occupied planar footprint, computes setback points, creates longitudinal contact edges, and intentionally replaces the selected shell region. The curvature-continuous mode changes the quarter-section and its extrusion surface, retaining the contact/topology path.
2. **Existing curve authority:** edge geometry already supports lines, circles, ellipses, hyperbolas, and non-rational `BSpline3Curve`. The local concave path uses straight longitudinal contacts; the old Profile contact shell constructs line/arc offsets and analytic components. Neither supplied contacts for a polynomial closed plateau.
3. **Existing face trims:** `PcurveGeometry` had Line, Circle, Ellipse, and Polyline. Planar polynomial edges fell through to sampled projection. AP242 pcurve export sampled even a polynomial represented indirectly.
4. **Exact blocker:** a smooth section alone cannot supply the polynomial support contacts, their UV trims, the base-face inner loop, and shared patch topology. The old contact-shell diagnostic correctly rejected that missing construction.
5. **Reused polynomial authority:** UV curves now reuse `BSpline3Curve` with Z=0, retaining degree, knots, control data, and the 3D edge parameter. There is no second spline evaluator.
6. **Architectural separation:** `PlanarPlateauContacts` owns 3D contacts and tensor surfaces; `BoundedPcurveBuilder` owns face-space bindings; `PlanarPlateauMaterializer` owns vertices, shared edges, coedges, loops, retained faces, and shell assembly. Display consumes these bindings rather than becoming trim authority.
7. **Minimum extension implemented:** affine polynomial contacts on two parallel planes, polynomial pcurve bindings/export, and a local planar-face graft. `CurvatureContinuousFilletSection.CreateQuarter` extracts the existing six-control law unchanged and is called by both the local fillet and Plateau. Two copies provide concave entry and convex exit.

No arbitrary SSI, rational construction, post-BRep repair, or sweep-network framework was added. Source `SmoothRoundedRect2` and the G2 section equations were not changed in this milestone.

## Construction, exactness, and identities

For source curve C(u), bounding-box center c, and s = 1 - Width/min(halfWidth,halfHeight), base contact is C(u), raised contact is c+s(C(u)-c). Height supplies the plane separation. The same affine maps act on the existing control points. The two quarter laws create two tensor patches per source span, with degree 1x5 on straight spans and 3x5 on corner spans.

`ExactAffinePolynomial` means no contact fitting error is introduced. Planar pcurves transform those controls into the support plane basis. Patch boundaries are exact isoparametric UV lines. All use forward source parameters [0,1]. Controls and provenance appear in CLI build/inspect JSON.

Representative generic span `BottomRightCornerA`, degree 3, knots [0,1], multiplicities [4,4]:

| Control | Base contact XYZ (mm) | Top contact XYZ (mm) |
|---|---|---|
| 0 | (22, -20, 5) | (19.8, -18, 8) |
| 1 | (24.7271713220, -20, 5) | (22.2544541898, -18, 8) |
| 2 | (27.4543426441, -20, 5) | (24.7089083797, -18, 8) |
| 3 | (28.7271713220, -18.7271713220, 5) | (25.8544541898, -16.8544541898, 8) |

Its named contacts are `Raised.BaseContact.BottomRightCornerA` and `Raised.TopContact.BottomRightCornerA`; support identities are `Body.Top` and `Raised.Top`. Band faces retain `Raised.Blend.<source span>.0/1`. The deterministic designated seam is the start of `Bottom`, an existing shared span boundary. There is no duplicated periodic seam edge. Other seam selections designate another source boundary without changing its geometry.

The materializer retains the existing shell/face/edge IDs, adds one inner loop to the base top face, and appends the band and raised cap. Every new ring edge is shared by two incident faces; each longitudinal boundary is allocated once. The source shell's winding convention is retained, with the hole opposing its outer loop. STEP import does not repair coedge orientation.

| Witness | Source spans | Band faces | Total faces | Edges | Vertices | Pcurves |
|---|---:|---:|---:|---:|---:|---:|
| Generic | 4 lines + 8 cubics | 24 | 31 | 72 | 44 | 144 |
| Phone body | 4 lines + 8 cubics | 24 | 35 | 84 | 52 | 168 |

Each band face has one four-edge loop/four pcurves. The top has one 12-edge loop/12 pcurves. The modified generic base face has two loops/16 edges/pcurves; the phone base has two loops/20 edges/pcurves. CLI `faces` reports numeric IDs, semantic identity, loop count, and edge count; the validated all-coedge pcurve coverage supplies the matching per-face pcurve count. Both shells are closed, manifold, and orientation-consistent, with no open trim loops.

## Admission and tolerances

A Bernstein bound on cross(C-c,C') proves positive angular progression; endpoint sectors and exactly one winding prove a simple star-shaped loop. Positive scale gives nested contacts. Monotone section height prevents bands at different section heights from intersecting; source sectors prevent adjacent spans from overlapping. Regularity and distinct positive height/width rule out collapsed patches. Contact control hulls must lie strictly inside the host's line/convex-arc support boundary. Unproved inputs fail typed rather than relying on sewing.

X1 admits one unmodified Profile extrusion, one planar top outer loop, and line/single-span cubic footprint geometry. Holes in the host, reflex support arcs, arbitrary modified hosts, and other support surfaces are not admitted. Straight polygon corners intentionally retain G0 creases between band spans; they do not imply G2 there.

No global sewing tolerance changed. Local identity/closure thresholds are 1e-8 mm, host containment clearance is 1e-7 mm, and existing pcurve verification uses 1e-5 mm. The supported scale tests are 0.1x, 1x, and 10x; extreme-scale universality is not claimed.

## Measured continuity and trim evidence

Analytic B-spline first/second derivatives are evaluated at 33 positions along every join. G1 is normal angle in degrees; G2 is the Frobenius residual of the shape operator in a shared world tangent basis, in 1/mm. Sampling checks the construction; it is not a global numerical certificate.

| Witness / joins | G0 max mm | G1 max degrees | G2 max 1/mm |
|---|---:|---:|---:|
| Generic base support (12) | 0 | 0 | 0 |
| Generic raised support (12) | 0 | 0 | 0 |
| Generic quarter joins (12) | 0 | 0 | 0 |
| Generic neighboring spans (24) | 1.005e-14 | 9.441e-13 | 1.408e-14 |
| Phone base support (12) | 0 | 0 | 0 |
| Phone raised support (12) | 0 | 0 | 0 |
| Phone quarter joins (12) | 0 | 0 | 0 |
| Phone neighboring spans (24) | 1.281e-14 | 7.694e-13 | 2.676e-14 |

Maximum edge-versus-surface deviation: generic 1.005e-14 mm; phone 3.178e-14 mm. UV closure maxima are 1.005e-14 and 1.281e-14 respectively. Every coedge has a valid domain and consistent orientation. Tests additionally compare polynomial pcurve control reconstruction, degree, and knots against the 3D edge controls.

## Phone burn-in and fresh authoring

[Refined source](../../fixtures/Experiments/IPhone17ProMax/refined-plateau.firmament) was written by a fresh agent given only public docs, the generic example, and the reconstruction README. It used ordinary Template/Record/Assembly authoring without kernel edits or manual trims. The body keeps 77.98 x 163.43 x 8.75 mm dimensions. The outer plateau contact remains 72.76 x 43 mm, centered at (38.99,-23.99), with 11.5 mm smooth corner extent and 2.55 mm rise. Width is explicitly 2 mm; the associated X inset is approximately 3.384 mm.

Independent evaluation of exported top-contact curves found minimum camera-circle clearance 0.275579 mm, with a conservative sampling bound above 0.273322 mm. Feature positions, diameters, and the 1.88 mm camera rise remain unchanged. Body plus plateau is one coherent solid; the assembly has 16 definitions and 28 product occurrences (29 including the root).

The fresh test exposed and helped resolve two generic friction points:

- STEP loop classification used only two chords per circular quadrant and falsely rejected an inner contact by 0.03738 mm. Its classification-only angular sampling now uses pi/64; exact circles remain untouched. No winding or topology repair was introduced.
- Display attempted coarse 3D-to-UV reprojection despite available trim bindings. Assembly display now recovers/verifies pcurves through the existing builder, and the bounded B-spline renderer consumes them. All faces render; the refined assembly contains 11,652 display triangles.

Standalone `inspect` is for the Model witness; assemblies use `asm inspect`. Plateau dispatch no longer mistakes a reusable assembly template for a standalone Model.

The +2 mm length variation updates the 13 explicitly authored bottom-detail translations as documented in the reconstruction README. A +0.1 mm plateau-height variation also passes. Both regenerate 35-face manifold bodies and G2 joins, with maximum pcurve deviation 3.178e-14 mm.

## Identical-camera evidence

The preview script reads CLI-generated meshes; the refined section reads the actual surface control net at Y=-23.99 mm. It does not create CAD geometry. Before remains the untouched blockout. Cameras, crops, neutral color, and illumination are fixed per comparison. The Drawing Notes support the rear stack and feature layout, but do not supply an exact transition-width/profile law; no invented drawing curve is overlaid.

![Generic raised plateau](../../artifacts/local/surf-g2-loop-x1/generic-shaded.png)

![Rear three-quarter before and after](../../artifacts/local/surf-g2-loop-x1/comparison-rear-three-quarter.png)

![Camera close-up before and after](../../artifacts/local/surf-g2-loop-x1/comparison-camera-closeup.png)

![Side profile before and after](../../artifacts/local/surf-g2-loop-x1/comparison-side-profile.png)

![Plateau section before and after](../../artifacts/local/surf-g2-loop-x1/comparison-section.png)

Display triangulation is derived and visibly coarser than the analytic surface. The direct `mesh --format obj/stl` SurfaceMeshIR route still rejects B-spline supports; the qualified preview route is `assembly-json` through the bounded tessellator. This is not a claim of a new watertight export mesh capability.

## STEP, performance, and persistence limits

Standalone body AP242 contains exact 3D B-splines, tensor surfaces, and exact polynomial UV pcurves. The existing product-structure assembly exporter retains the exact surfaces/edges and manifold definitions but does not serialize pcurve associations; these are recovered after import. Full pcurve serialization evidence is therefore the standalone generic and phone-body STEP, not the assembly STEP. Likewise, semantic span/face identities live in the inspection report; AP242 topology IDs may be renumbered on reimport. Product/occurrence identities are retained by assembly export.

| Witness | Contact plan ms | Trim/BRep graft ms | Total including export/reimport ms | Standalone STEP bytes |
|---|---:|---:|---:|---:|
| Generic | 111.8 | 117.4 | 352.5 | 104,418 |
| Phone body | 109.1 | 145.2 | 415.0 | 807,663 |

These are single local cold measurements, not benchmarks. Contact timing includes differential verification; graft timing includes pcurve construction/preflight. Total also includes initial host construction and STEP checks. Retained conic pcurve export uses the existing sampled serialization, accounting for the phone body's larger file. New polynomial contacts do not use it.

Repeated generic builds and freshly installed packaged CLI builds have byte-identical STEP. Packaged phone assembly STEP matches the fresh authoring export. Control data, pcurves, source order, seam selection, and face identities are deterministic by construction and covered by repeated export checks.

## Validation and reproduction

Release solution build: zero warnings/errors. Full serial solution regression: **3,606 passed**, all 20 test projects exited zero (the disabled legacy FrictionLab project has no active tests). This includes existing local circular/G2 fillets, SmoothRoundedRect2, SectionChain G1, STEP classification, display, and prior geometry regressions. The 18 new Plateau cases cover mixed/straight loops, tight corners, three scales, dimensional variations, zero/negative/excessive width, invalid height, outside support, degenerate/self-crossing footprints, retained topology, deterministic export, exact pcurves, and the fresh-authored phone assembly/display.

The isolated packaged CLI 2.0.0-preview.3 build/export checks, repository layout guard, documentation links, generic-code fixture-name scan, and `git diff --check` pass. Full logs and generated evidence are ignored under `artifacts/local/surf-g2-loop-x1/`; images linked above are local run artifacts.

```powershell
dotnet build Aetheris.slnx -c Release --no-restore
$cli = Resolve-Path Aetheris.CLI/bin/Release/net10.0/aetheris.exe
& $cli build fixtures/Canonical/Surfacing/g2-planar-plateau.firmament --out artifacts/local/surf-g2-loop-x1/generic.step --json
& $cli inspect fixtures/Canonical/Surfacing/g2-planar-plateau.firmament --json
& $cli asm inspect fixtures/Experiments/IPhone17ProMax/refined-plateau.firmament --json
& $cli asm export-ap242 fixtures/Experiments/IPhone17ProMax/refined-plateau.firmament --out artifacts/local/surf-g2-loop-x1/refined.step --json
& $cli mesh fixtures/Experiments/IPhone17ProMax/refined-plateau.firmament --format assembly-json --output artifacts/local/surf-g2-loop-x1/after.mesh.json
& $cli mesh fixtures/Experiments/IPhone17ProMax/blockout.firmament --format assembly-json --output artifacts/local/surf-g2-loop-x1/before.mesh.json
```

`qualify-surf-g2-loop-x1.py` renders the generated comparison inputs and the standalone phone-body control report. The latter is built by wrapping the phone declaration catalog in an ordinary Model and applying `Struct Part = BodyPart<Spec: Standard>`, exactly as assembly specialization does. A one-occurrence template wrapper around the generic example supplies `generic.mesh.json`. The script requires numpy, Pillow, and matplotlib.

Deferred: exact Apple hidden splines, lens ring refinement, body side-edge refinement, forward sensor partitioning, exact material keepout union, manufacturing equivalence, arbitrary support surfaces, normal-distance offsets, and the implicit EdgeFinish form. None are required to use the admitted generic Plateau construction.
