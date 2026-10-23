# Ordinary CAD fillet/chamfer robustness — 2026-10-18

**Success within the qualified ordinary CAD rim families.** The arc/chord
D-profile now builds a real fillet or chamfer through the source-owned planner,
exports/reimports STEP and produces a complete watertight mesh on either rim.
Judgment selects supported constructions before emission. Existing compatibility
gates pass; unsupported requests reject explicitly.

## Scope and reproduction

Investigated Claude's reported arc-and-line D-profile failure. The exact Claude
source was not supplied in this request, so the durable reproduction is a
minimal semicircle of radius 10 mm, closed by its diameter, extruded 8 mm and
finished with a 1 mm rim radius/distance. It uses ordinary Profile/Struct/Modify
authoring, not BodyState or a general surface blend.

The initial CLI run of the preferred-field specimen rejected with
`concept-path-arc-invalid`, then `EdgeFinishProfileComposeBoundaryUnsupported`
after using the old Arc field casing. The source-owned finish reader was still
testing PascalCase fields with structural regexes before geometry planning.
Its error described an unrelated primitive/Compose limitation. Inspection also
found that the old junction classifier used the polygon of segment endpoints:
two endpoints have zero polygon area even when their arc and chord enclose a
perfectly ordinary part.

Sources are preserved under
[`fixtures/Regression/EdgeFinishRobustness`](../../../../fixtures/Regression/EdgeFinishRobustness/README.md).
Raw run evidence is ignored under `artifacts/local/edge-finish-robustness/`.
No existing canonical source or geometry golden was rewritten to satisfy this
repair.

## Root causes and repairs

| Finding | Repair |
| --- | --- |
| Preferred Arc `radius`/`turn` and Line `turn`/`heading` missed old spelling projection | Extend the existing scoped spelling owner; preserve symbols and case-only compatibility. |
| Source-bound EdgeFinish/Selection recognition depended on PascalCase regex block discovery | Consume actual AST declarations and existing `FirmamentSyntaxFields`. Reuse source order, member-set selection and the historical ProfileSegments call form. Remaining regexes read bounded serialized field values only; they must consume the whole value. |
| Malformed optional values could disappear into defaults | Mark failed optional field conversions invalid. Numeric enum spellings reject. Closed-loop `endClearance` rejects rather than being ignored. |
| Multiple requested Profile finishes succeeded while emitting only the first | Reject the unsupported sequence before materialization with `ProfileBoundaryEdgeFinishSequenceUnsupported`. Reuse executable AST declaration ownership so uncalled Feature/Template recipes are not counted as operations. |
| Curved profile winding was inferred from endpoint chords; several finish helpers also doubled the arc area contribution | Reuse the existing exact boundary-area owner for classification, contact planning and materialization. Positive/negative sketch translations cannot change material polarity. |
| Every curved join was treated as a tangent rolling meridian | Prove tangency or choose the qualified circular-segment miter through Judgment. Reject other sharp curved junctions before topology allocation. |
| Whole-loop fillets assumed every convex polygon corner was 90 degrees | Compute the actual two-inset-line intersection and exact cylinder/cylinder ellipse for each convex angle. Retain the old support arithmetic at right angles; the separate incidence repair below deliberately changes vertex identity. |
| Collinear authored subdivisions were rejected as corners | Retain the subdivision and common support; do not invent a corner patch. |
| Whole-loop mixed bottom finishes were disabled | Use the selected cap's outward axis, transition depth and reversed loop traversal with the same support contracts. Check outward plane support sense for bottom/reversed-winding chamfers as well as trim containment. |
| Oversized polygon insets could reverse an edge | Reject reversed/collapsed inset edges before emission, using a length-scaled linear tolerance. Convex whole-loop fillets reuse that existing inset proof. |
| Every convex torus with source radius greater than finish radius was labelled Horn | Classify actual major/minor radii: Ring, Horn, or the existing trimmed outer-quarter Spindle support. Geometry is unchanged for existing admitted sectors. |
| Source-owned finish results did not retain their runtime body/correspondence or a feature receipt | Retain the actual emitted geometry, semantic descendants, stable EdgeFinish name and planning evidence. |
| Component-local IDs duplicated the same planned cap/side junction, including a sphere-limit apex | Allocate junction-owned contact vertices and one component-owned sphere-limit apex. This repairs actual loop incidence without coordinate welding. Shared face/edge semantics and boundary geometry remain retained. |
| SurfaceMeshIR rejected existing Ellipse3 curves and bounded torus/spline miter trims | Sample the actual ellipse with bounded chord/tangent error and reuse the existing analytic parameter-space trim owner for a four-sided outer-torus band. Keep shared source edge samples; do not mutate the BRep or synthesize renderer geometry. |

The repair adds no parser dispatch family, source macro system, approximate
polyline solid, general blend fallback, or relaxed export preflight. Existing
Box, hole/finish, bounded internal fillet and low-level sculpting ownership stays
in place. This pass does not resume the separate arbitrary-BodyState editing
adapter work.

## Geometric construction and numerical authority

A circular segment is the intersection of a circle of radius `R` with a
half-plane. In the chord's inward coordinate, the chord lies at `k`. At inset
distance `d`, the finish section is the circle of radius `R-d` clipped by the
line at `k+d`. Their intersections have

```text
x² = R² - k² - 2(R+k)d
```

The finish must stay strictly below `(R-k)/2`, the segment's inradius. Equality
collapses the cap; a larger finish must not emit inverted or unrelated geometry.
The miter construction also requires positive retained stock thickness.

For Chamfer, parameterize `x` linearly. Both inset `d` and axial rise are then
quadratic in that parameter, so one degree-two Bezier represents the shared
plane/cone seam exactly in real arithmetic.

For Fillet, use the rolling meridian `t` in `[0, pi/2]`:

```text
d = f(1-cos(t))
z - transition = f sin(t)
x = sqrt(A+B cos(t))
A = R²-k²-2(R+k)f > 0
B = 2(R+k)f
```

The plane-side rolling cylinder and circular-side torus share this intersection.
Both faces use one edge identity. The implementation constructs piecewise cubic
Hermite spans subordinate to this equation, with a global fourth-derivative
remainder bound plus a separate coordinate-scale floating-point allowance.
Refinement is bounded to 16,384 spans and must meet the default 1e-6 mm linear
tolerance; exhaustion fails explicitly. This is a finite edge representation
on analytic support surfaces, not an assertion that the generic spline is an
exact symbolic curve. STEP retains that finite curve. No sampled polygon is
used as the engineering boundary.

For a 1 mm finish on the 10 mm semicircle, the top chord endpoints move to
`(+/-sqrt(80), 1)` in sketch coordinates. Each cap remains one arc and one line.
The result has six faces, ten shared edges and six vertices. The unmodified
extrusion has four faces. Tests assert this difference, actual support
containment and shared incidence, rather than accepting a plausible unchanged
solid.

## Judgment contract and diagnostics

`ProfileEdgeFinish.Junction.v1` evaluates source-contact and circular-segment
miter candidates before topology emission. Hard rules establish supported
joins and noncollapsing miter geometry; finite source-fidelity utility selects
among admitted constructions with the existing JudgmentEngine adapter. There
is no reject-bypassing generic blend candidate.

`build --json` publishes `edgeFinishPlanning` for the circular-segment path:
candidate rules, admission/rejection evidence, policy version, selected strategy
and maximum curve-deviation bound. Internal construction tuples are deliberately
excluded from this JSON contract. Engineering feature reports retain
`edgefinish:Rim`. A bare-Profile planner failure is anchored at its EdgeFinish
declaration; a rejected sequence identifies the second requested operation.
Expanded provenance follows the existing diagnostic mapping.
The legacy materializer transport removes the enclosing Model, so its local
offsets must first be mapped back to the corresponding declaration in the
frontend's retained source. The initial location test caught this mismatch;
the repair uses retained AST identity, without a new diagnostic-code allowlist.

Oversize, unsupported junction, malformed field and unsupported endpoint-policy
requests reject through validate and build, leaving no artifact. The shared
diagnostic/presentation pipeline remains the compiler authority.

## Qualification and compatibility

The focused matrix covers D-profile fillet/chamfer, both rim sides, placed
frames, materializer winding reversal, minor/major circular segments, acute and
obtuse polygon corners, collinear subdivisions, named selections, typed
EdgeFinish variants, preferred/compatibility fields, shape identity, deterministic
STEP, reimport, curve/support agreement and invalid-size admission. Kernel-level
clockwise tests do not change the language's existing outer-loop winding rules.
Dynamic STEP imports are of newly constructed before/after artifacts, so they
cannot use a static Step242Corpus item.

One pre-existing unit specimen was incorrectly treated as a valid curved loop:
`CurvedProfile()` in `ProfileBoundaryChamferTests` reverses its tangent by 180
degrees entering the arc and has another non-tangent curved exit. Its old
`IsEnclosed` check proved incidence, not that the trims lay on their surfaces.
That exact specimen remains in the test, now asserting explicit rejection with
no body. Legitimate mixed line/arc production cases remain separate regression
tests; no failing source was edited into a different shape.

The first real mesh qualification caught two further ownership gaps that BRep
support containment alone did not detect. D-profile fillets needed bounded
torus/spline trim consumption; polygon fillets exposed duplicate planned vertex
identities at cap corners. Both are now covered through actual CLI STEP-to-OBJ
qualification with all faces, watertight/connected/outward topology and no cracks.
Strict coedge endpoint identity is asserted in addition to geometric closure.

The retained before/after Profile Chimera witness
(`fixtures/Regression/CanonicalGeometry/profile-edgefinish-chimera-fillet.firmament`)
compares every face support, ordered
loop curve, trim interval, edge/face orientation and endpoint position after
STEP reimport, independently of entity/vertex IDs. Its incidence repair is an
intentional STEP topology correction, not a rebased geometric golden. The
comparison result and counts are included in the final evidence below.

| Chimera STEP-reimport witness | Before | After |
| --- | ---: | ---: |
| Faces / edges | 37 / 86 | 37 / 86 |
| Vertices | 84 | 51 |
| Coedge end/start vertex-identity breaks | 65 | 0 |
| Geometry-only SHA-256 | `8592fd4f6a169f16f81be5b03c431890fbfda3405fe49c3324678664e881f552` | same |

The geometry-only hash excludes entity IDs, but includes the actual support
parameters and ordered trim geometry/sense/positions. The local proof helper
and `incidence-geometry-proof.json` are retained with the ignored run artifacts.

For this repaired whole-loop family, raw STEP hashes and vertex-derived IDs
intentionally change. Source feature names and the face/edge semantic owners
remain retained. This is not a promise of byte compatibility for the formerly
disconnected topology, or of identical display tessellation after enabling the
previously unsupported trim consumers.

| Gate | Result |
| --- | --- |
| Release solution build | 0 errors; 30 existing dependency/WASM warnings |
| Focused Firmament finish matrix | 96 passed |
| Focused CLI admission, identity and mesh matrix | 25 passed |
| Fast Core lane (`Category!=SlowCorpus`) | 1,076 passed |
| Complete solution suite | 5,559 passed, 0 failed, 0 skipped across 20 nonempty test assemblies |
| Historical domain frontend comparison | 37/37 matched: 35 byte-identical STEP artifacts and 2 expected rejections |
| Canonical corpus | 369 records: 296 qualified owner operations, 69 consumed modules, 4 expected external-body boundaries |
| Claude FrontendX4 | 32/32, including Chimera geometry matching its baseline |
| SheetMetalSyntax | 26/26; formed STEP / flat parity true |
| SheetMetalSemantics | 29/29 |
| Actual CLI build → STEP → OBJ | All 6 top/bottom cases fully covered, watertight, connected and outward; 0 cracks, nonmanifold edges or zero-area triangles |

The retained two-rim specimen now rejects through both CLI `validate` and
`build` with `ProfileBoundaryEdgeFinishSequenceUnsupported` at line 13,
column 9 (the second requested finish), and emits no new STEP artifact. The
original first-operation-only success remains preserved as reproduction evidence;
it is not counted as qualification. Separate fillet/fillet, chamfer/chamfer and
fillet/chamfer regressions assert rejection and no output. An uncalled finish
Feature preserves byte-identical STEP and one engineering feature receipt.

The full run includes 1,209 Core tests and 2,905 Firmament tests. FrictionLab
advertises no tests and is not counted as a passed test assembly. Canonical
qualification uses each domain's actual owner operation; an assembly/scene
inspection is not a claim that every such project was independently meshed or
STEP-hash compared. The historical comparison and the explicit Chimera proof
provide the geometry-parity evidence.

An earlier unrestricted-processor full run encountered one existing timed
display failure in `ExternalStepDefinitionsStaySharedWhenPoseChanges`
(`AssemblyKinematicsX0Tests`): Plane face 3 exceeded its execution budget at
6,498 ms. The unchanged test passed in isolation in 372 ms. The complete final
run above passed with two advertised processors; no timeout was increased.
The initial failure and isolated rerun remain in
`full-before-support-sense.log` and `timeout-isolated.log`. This is local
qualification, not a remote CI or desktop UI certification. Frozen Zig suites
were not run because no legacy Zig source or build infrastructure changed.

Reproduce the engineering path from the repository root:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament --output artifacts/local/d-profile-fillet.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll mesh artifacts/local/d-profile-fillet.step --format obj --output artifacts/local/d-profile-fillet.obj --json
```

The regression gates use the installed .NET 10.0.401 SDK and the repository CLI,
with `MSBUILDDISABLENODEREUSE=1`, `-m:1`, `-nr:false` and
`-p:UseSharedCompilation=false` for the Release build. The complete suite uses
`dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1`.
The final full run advertises two processors with process-local
`DOTNET_PROCESSOR_COUNT=2`; this limits oversubscription without removing tests
or changing their deadlines.

## Code-quality review

The source reader now reuses executable AST ownership and the existing field
consumer; no independent field schema, fatal-code allowlist, parser dispatch or
global rewrite was added. Its remaining regular expressions consume one entire
serialized field value. Selection binding retains the existing member-set and
compatibility call forms. The new Judgment policy compares two bounded support
constructions before emission, and cannot admit an unsupported intersection.

Shared topology is allocated from planned source junctions rather than proximity
matching. Circular-segment numerical refinement has an explicit error budget
and finite exhaustion diagnostic. Mesh consumers reuse source boundary IDs and
existing parameter-space triangulation. Public reports omit the internal tuple
packet rather than serializing its coordinates as empty objects. No runtime
host reuse, background service, repository-wide formatting or golden rebasing
was introduced.

## Remaining boundaries and verdict

This is qualification of ordinary source-owned rim families, not a claim that
all CAD fillets now work. Other sharp curved junction graphs, arbitrary imported
edge selection, interacting multi-feature fillets and non-right-angle reflex
junctions need their own shared topology/materialization proof. Existing
unsupported selections remain explicit failures. A general low-level surface
blend is not prescribed as the authoring workaround for a D-shaped CAD part.
The existing Profile/Compose fillet-with-cavity interaction boundary is not
broadened here; Box/hole finishes retain their separate qualified owner.
Profile/Compose still admits one EdgeFinish operation per compiled construction;
top-plus-bottom and mixed-kind sequences now reject rather than losing intent.

Public syntax, construction limits, diagnostics and executable source links
are updated in place. No remaining language-design decision is required for
this bounded repair. The end state is **Success** for the D-profile and qualified
polygon/tangent rim robustness pass, with the explicit boundaries above; this
is not a declaration of universal fillet support or broader X2 closure.
