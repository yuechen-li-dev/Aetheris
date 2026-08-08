# Preview 1 feature freeze

## Purpose

This is the authoritative Preview 1 support contract.  It freezes the
Firmament V2 author surface at commit `736a8e69b013afac15a046939a98fa1a26bd1bd0`
on branch `master` (2026-08-07).  The companion machine-readable contract is
[`artifacts/release/preview1-capabilities.json`](../../artifacts/release/preview1-capabilities.json).
No release package version is assigned (`aetheris.client` development metadata
is `0.0.0`).  The target is Aetheris canonical STEP/AP242;
the exporter and verification path operate on exact analytic B-rep entities,
not a tessellated interchange format.

The grammar identifier for this freeze is **Firmament V2 canonical grammar
X1**.  Canonical declarations use PascalCase, `Model Name { Units: mm }`,
typed points such as `Point2(1mm, 2mm)`, and dimensioned literals.  Existing
lowercase, phase-style, face-local, and `solid` forms are compatibility input,
not peer authoring dialects.

## Baseline

`dotnet restore Aetheris.slnx` and `dotnet build Aetheris.slnx -f net10.0
--no-restore /m:1` succeeded with **0 warnings / 0 errors** using .NET SDK
10.0.302.  The required test suites passed: Kernel Core 994, Kernel Firmament
1023, CLI 337, Server 41 (2,395 total).  The frontend passed 55 tests and its
production build with no warnings. `npm audit` reports 0 advisories.  `git
diff --check` was clean before this freeze work; final validation is recorded
in the manifest.

## Public Firmament V2 surface

| Surface | Preview 1 status | Canonical contract |
| --- | --- | --- |
| Model / Units | Supported | `Model` with `Units: mm`; `mm` is the only canonical unit. |
| Box / Cylinder / RoundedBox / Frustum | Supported | Exact primitive declarations and their documented fields. |
| Concept, `Concept Struct`, Struct | SupportedBounded | Typed semantic scaffolds and material bodies; no general runtime language. |
| Construction Plane | SupportedBounded | Immutable traced frame; source hole route is Box + signed-permutation frame + shaft ThroughAll. |
| Concept Path / Profile / Compose | SupportedBounded | Concept Path-derived Profiles are admitted by Extrude; Compose currently requires low-level `Profile ... Using ... { Loop ... Segment ... }` source. |
| Modify / Selection | SupportedBounded | Modify admitted bodies; Selection is source-grounded, never anonymous B-rep inference. |
| Hole<Shaft>, Hole<Counterbore>, Hole<Countersink> | SupportedBounded | Exact combinations below. |
| Slot<Capsule>, Slot<RoundedRectangle> | SupportedBounded | Through-all semantic removals in admitted Compose bodies. |
| EdgeFinish Chamfer | SupportedBounded | Source-bound profile and documented primitive routes. |
| EdgeFinish Fillet | SupportedBounded | Straight and two-line routes supported; whole-loop mixed shell is experimental. |
| Record / Static / arrays / Template / Pattern | SupportedBounded | Compile-time-only bounded expansion. |
| Match | Experimental | Template-expansion arm selection only; not general control flow. |
| Require | SupportedBounded | Static comparisons and named semantic diameter constraint for projection. |
| Assert Volume | SupportedBounded | Literal source assertion, evaluated after STEP reimport. |
| Pmi / Datum / projected PMI | SupportedBounded | Datum and HoleDiameter only, explicit projection only. |
| InlineStep / Recognize / Replace | SupportedBounded | Canonical Aetheris AP242 input and bounded shaft-hole workflow. |

The manifest maps each claim to a fixture and, where retained, a STEP artifact.
`SupportedBounded` is deliberate support under the listed admission rules; it
does not mean fallback to a generic Boolean or NURBS route.  `Experimental`
means executable evidence exists but it is not promoted to the frozen matrix.

### Public support table

| Area | Status |
| --- | --- |
| Exact primitives, canonical model root | ✅ Supported |
| Holes, slots, Profile/Compose, static authoring, PMI, semantic labeling | 🟡 Bounded / explicit policy |
| Mixed line/arc whole-loop Fillet and bounded Match | 🧪 Experimental |
| Mesh/NURBS export, general foreign STEP import, runtime scripting, automatic decompilation | ❌ Intentionally unsupported |

### Canonical declaration shapes

| Family | Canonical shape / required names |
| --- | --- |
| Root and primitives | `Model N { Units: mm }`; `Box N { Size: [...] }`; `Cylinder N { Radius; Height }`; `RoundedBox N { Size; CornerRadius }`; `Frustum N { BottomRadius; TopRadius; Height }` |
| Semantic construction | `Concept`, `Concept Struct`, `Struct`; `Construction Plane N { Trace: ConceptPlane }`; use PascalCase field names. |
| Paths and bodies | `Concept Path N { Start; Heading; Line/Arc; Close }`; `Profile N From Path` + `Extrude`; `Compose` requires `Profile N Using Layout { Loop Outer { Segment ... } }`; `Modify Body { ... }`. |
| Holes and slots | `Hole<Variant> N { On|From; Center; Diameter; End }`; Counterbore adds `CounterboreDiameter`, `CounterboreDepth`; Countersink adds `CountersinkDiameter`, `CountersinkAngle`; slots use `Center`, `Direction`, `Length`, `Width`, `Extent` and RoundedRectangle adds `CornerRadius`. |
| Edge finish and selection | `EdgeFinish N { Target; On; Kind; Distance|Radius }`; `Selection N { Target; Source; Require }`. |
| Static | `Record T { Field: Type }`; `Static N: T[] = [...]`; `Template N(T x) { ... }`; `Pattern N Over Values { Template(Current) }`; `Require N => comparison`. |
| Semantics and PMI | `Pmi { Datum N { Target } HoleDiameter N { Target; Value; Tolerance; DatumRefs } }`; projected form is `HoleDiameter N { From; As: HoleDiameter; DatumRefs }`. |
| Verification and imported STEP | `Assert Volume Body { Expected; Tolerance; Note }`; `InlineStep`, `Recognize`, and `Replace Source.Region With Hole<Shaft> ...` use PascalCase. |

## Mechanical features

`Hole<Shaft>` supports `On` + `Center` + `Diameter` with `ThroughAll`, and
the documented face-local blind/drill-point forms.  A Construction Plane hole
uses `From` instead of `On` and is limited to a simple Box, a proper
signed-permutation local frame, and ThroughAll.  The Profile/Compose route
supports deterministic Pattern-generated shafts and a `+Z`, ThroughAll,
disjoint Counterbore.  Counterbore requires `CounterboreDiameter` and
`CounterboreDepth`; Countersink requires `CountersinkDiameter` and
`CountersinkAngle` in `deg`.  Cavity overlap/touching, non-prismatic hosts,
other Counterbore orientations/end conditions, and Construction Plane
Counterbore/Countersink are intentionally rejected with typed diagnostics.

Slots are only `Capsule` and `RoundedRectangle` (`CornerRadius` is required
for the latter), `Extent: ThroughAll`, and an admitted Compose host.  Static
Patterns generate shafts or both slot families from static record arrays only.
Generated IDs are deterministic (`PatternName_0`, `PatternName_1`, …); a
generated Profile requires an indexed direct template invocation because it
must have an explicit identity.

## Profile and composition

`Concept Path` is the preferred connected line/arc scaffold.  Low-level
`Segment`/`Trace` remains supported for authored source-bound boundaries, and
is equivalent as a resolved profile input, not a different geometric backend.
Profiles accept line and arc outer loops; currently admitted inner loops are
the explicit cutout fixtures.  Segments use named points/guide corners, not
coordinate endpoints; loops close and have the required winding.  Extrusion
is in the construction plane's local +Z, so `Top` and `Bottom` are local—not
world-Z—terms.  Compose materializes prismatic stock and bounded profile
composition.  Unsupported arrangements, open/wrong-winding loops, and
unadmitted cavity interactions have fixtures and diagnostics in the manifest.

## EdgeFinish

### Chamfer

Source-bound Profile Chamfer is frozen for Top and Bottom straight segments,
connected chains, and whole outer loops; convex and reflex line junctions are
covered.  The mixed line/arc whole-loop card is also supported using exact
Plane/Cone patches: sharp convex/reflex, rounded convex/reflex, cone-frustum,
and bounded cone-apex cases.  Compose has a narrower Top whole-outer-loop
route with disjoint Shaft/Counterbore cavities.  There is no NURBS fallback.

`ConvexSmall` (`source radius < finish size`) is intentionally invalid:
`ProfileBoundaryChamferConvexArcRadiusTooSmall` describes the collapsed
inward offset.  Inner loops, unsupported Compose boundary routes, and cavity
collisions remain explicit rejection, not degraded geometry.

### Fillet

The frozen supported routes are a single finite straight outer edge and two
adjacent straight source segments at convex or reflex 90-degree junctions,
on local Top or Bottom.  Reflex defaults to **ExactRolling** (horn-torus)
and accepts the opt-in **SphereSeamCompatibility** representation.  Source
chain binding is accepted for those bounded two-segment cases; unsupported
chains and whole-loop requests diagnose rather than silently selecting raw
B-rep edges.

The seven-station mixed line/arc whole-loop Fillet and its compatibility card
currently build/reimport, but their Assert Volume evidence has a certified
curved-trim error bound of about 41,239 mm³.  They are **Experimental**, not
frozen Supported; tightening that verification is a release blocker.  Rounded
source policy is exact Cylinder/Sphere/Torus only.  `ConvexSmall` is
intentionally invalid with `ProfileBoundaryFilletConvexArcSpindleUnsupported`:
the rolling locus is spindle/self-intersecting.  No spline rescue exists.

ExactRolling and SphereSeamCompatibility are geometrically distinct topology
policies, not exporter preferences.  ExactRolling is the default analytic
reflex horn-torus construction; SphereSeamCompatibility is explicit only for
downstream consumers that cannot retain that seam/topology.  Both preserve
source provenance and use the normal AP242 exporter.

## Static semantics, Concept, Require, and PMI

Record, Static arrays, Template, Pattern, Match, and ordinary Require are
compile-time-only.  Their graph is bounded, expansion is deterministic,
declaration provenance and generated identities are retained, and duplicate
names/declaration-order violations are diagnostic.  They are not runtime
programming, embedded C#, or a general expression language.

Concept/Static declares intent; `Require` binds and validates selected model
semantics; `Pmi` explicitly projects validated selected intent.  The supported
path is:

`Static/Concept -> feature -> named Require -> SemanticConstraint -> explicit
Pmi From/As -> AP242 exporter`.

`From` names the validated Require, `As: HoleDiameter` is required, and
`DatumRefs` supplies currently admitted datum references.  Projection rejects
target/value/tolerance overrides and failed/unknown requirements.  Manual and
projected HoleDiameter normalize to the same AP242 path.  Supported PMI kinds
are Datum and HoleDiameter only; there is no automatic Concept-to-PMI,
automatic Require export, or ontology inference.

## Verification and semantic labeling

`Assert Volume` accepts literal finite `Expected` and absolute non-negative
`Tolerance` in `mm^3`, an optional `Note`, and one semantic material body
target.  Build materializes, reimports its own STEP, measures with the
authoritative mass-property route, and exposes JSON results; assertion text
does not alter STEP.  There is no calculator, expression engine, or embedded
C#.  Curved trimmed faces can currently carry a conservative certified error
bound; the mixed whole-loop Fillet card is the material limitation.

For imported models: analyze first, then `InlineStep`, `Recognize`, and
`Replace`.  `Faces` and `Source.Face(n)` are sequential analysis IDs mapped to
raw `ADVANCED_FACE` entities; `StepFaceEntities` is traceability-only.  The
recognized kinds are HoleShaft and DatumPlane.  Replacement is only verified
ThroughAll `Hole<Shaft>` rebuild, with `On`, `Center`, `Diameter`, and
`HostSize`.  This is semantic labeling, not automatic decompilation.

## STEP/AP242 contract

The external artifact contract is canonical Aetheris AP242 STEP with header
schema `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF`, deterministic for a
fixed source/route, reimported and manifold-checked on advertised build routes.
Emitted exact analytic surface families are Plane, Cylinder, Cone,
Sphere, Torus, and Hyperbola where required by the analytic construction;
curves use exact line/circle/arc families.  AP242 Datum and HoleDiameter PMI
are exported on the stated route.  Topological traversal orientation is
preserved independently of analytic curve parameter direction, including
`EDGE_CURVE.same_sense`.  General foreign STEP import, arbitrary STEP entities,
mesh/NURBS production output, and automatic feature recovery are not claimed.

## Fixtures, invalid policy, and compatibility

The complete fixture-to-feature and invalid-policy maps are in the manifest.
Principal executable cards are the canonical primitive, hole, slot, path,
Profile/Compose L-bracket, Pattern/Template, Chamfer, Fillet, PMI, Assert
Volume, and InlineStep cards.  The canonical invalid cards cover Hole shape,
ConvexSmall Chamfer/Fillet, PMI projection overrides, Concept Path validity,
unsupported Profile edge finishes, and Compose cavity collisions.

Compatibility input is explicit: lowercase root/PMI, `solid` bindings,
phase-style EdgeFinish, bracket points, face-local `On` holes, lower-case
InlineStep/recognize/replace, and legacy `Dimension` PMI are retained only as
listed in the manifest.  `Solid`, `Let`, `Fill`, `Manufacturing`, `Feature`,
and `Expose` are compatibility-only parser vocabulary, not canonical Preview
1 declarations.  No compatibility form is presented as a new-authoring
alternative.

## Remaining release blockers

1. **Curved-trim mass verification** (Kernel/verification): make the
   seven-station mixed Fillet's measurement tolerance materially tight, with
   deterministic reimport evidence.  Code changes are allowed as a
   release-blocker correction to the experimental route, not new geometry.
2. **Whole-loop Fillet promotion decision** (Firmament/geometry): promote only
   after blocker 1 and an external-kernel smoke; otherwise leave it experimental
   at Preview 1.  No feature broadening is authorized.
3. **Release UX** (CLI/Cadmata/VS Code): clean-machine install and public
   authoring documentation evidence remain required release work.

## Change control

After this freeze, every change must be labeled BUG, RELEASE-BLOCKER, DOCS,
UX, PACKAGING, INTEROP, TEST, SECURITY, DEPENDENCY, or
PERFORMANCE-REGRESSION.  If none applies, defer it to post-Preview-1.
Canonical grammar changes require release-blocker justification and explicit
release-hardening reporting.  New geometry capability is prohibited unless it
fixes an advertised matrix feature or a verified correctness/interoperability
bug in advertised behavior.  No new Preview 1 features.
