# Recognized imported Sheet Metal recovery M1

> Superseded for contour-stitch status by Recovery M2: CTC-03 now has one valid
> recovered outer contour after three explicit recovery-only point-tangent
> micro-closure repairs. The M1 metrics below remain the historical baseline.

## Verdict

**Meaningful progression.** Aetheris can now distinguish machine bend
candidates from accepted bend authority, validate an engineer/LLM recognition
plan against the imported BRep, and unfold only the accepted bends directly
from imported geometry. CTC-03 produces exact source-derived line/arc contours
for every planar region, 17 exact opening contours, seven bend lines, and 118
source-edge provenance links. It does not yet produce one validated global
`PlanarContour2`: the bounded arrangement rejects three point-tangent junctions
in the hostile blank, so the source reference correctly remains `Partial`.

This is a recovery reference, not manufacturing authority and not a claim of
complete CTC-03 reconstruction.

## Existing recovery audit

Before this change, `SheetMetalRecognizer` already recovered:

- dominant constant thickness from tolerance-bounded opposing plane/cylinder
  pairs through `JudgmentEngine`;
- planar mid-surface regions and paired cylindrical bend regions;
- bend axes, angles, inside radii, directions, two-region adjacency, source
  face/edge bindings, and evidence;
- CTC-03's 8 planar regions, 7 bend regions, 15 circular holes, and 2 slots;
- a deterministic graph traversal and K-factor manufacturing flat.

The old imported flat treated every recovered cylinder adjacency as authority,
used polygon vertices for source regions, and exposed a convex hull as the
imported boundary. It therefore could not answer what the source free edges
actually looked like.

## Architecture and implemented types

```text
STEP / imported BRep
  -> SheetMetalRecognizer (detected facts)
  -> RecognizedSheetMetalModel
       RecognizedBend: Candidate | Recognized | Rejected | Ambiguous
  -> SheetMetalRecognitionPlan (explicit root, reference, decisions, names)
  -> ValidatePlan (cylinder, radius, angle, adjacency, connectivity, cycle)
  -> RecoveredSourceFlattener
  -> RecoveredFlatReference
       exact per-region contours + exact cuts + bend lines + ancestry
  -> RecoveredFlatComparer
  -> native Firmament flat
```

The public records are in `RecognizedSheetMetalModel.cs`; unfolding is in
`RecoveredSourceFlattener.cs`; comparison is in `RecoveredFlatComparer.cs`.
`SheetMetalPartIr` remains authored/native IR. The recognized model is an
interpretation of immutable source geometry.

## Recognition authority and human/LLM assertion

Detection emits all seven CTC bends as `Candidate`. The automatic plan changes
only candidates with complete paired cylindrical support, a finite bounded
angle, nonnegative radius, and exactly two recovered planar neighbors to
`Recognized`. Source unfolding consumes only that recognized subset.

The test suite renames one accepted assertion to `FrontWallBend` with authority
`engineer/LLM assertion checked against imported geometry`; the compiler
accepts it while preserving the raw bend ID and face pair. A second test
asserts an invented bend and receives
`sheetmetal-recognition-assertion-invalid` because no detected cylindrical
support exists.

## CTC-03 bend evidence

All values below are measured source facts; tolerance is 0.01 mm linear and
0.001 rad angular.

| source bend | axis | angle | inside R | adjacent regions | status |
|---|---|---:|---:|---|---|
| `bend-c-0005-0024` | X at Y=440.9948, Z=8.25754 | 90° | 6.35 | 0061-0066 ↔ 0088-0089 | Recognized |
| `bend-c-0006-0009` | X at Y=43.47972, Z=8.25754 | 90° | 6.35 | 0062-0063 ↔ 0096-0097 | Recognized |
| `bend-c-0046-0050` | Y at X=14.60754, Z=113.56594 | 45° | 6.35 | 0109-0111 ↔ 0118-0119 | Recognized |
| `bend-c-0057-0070` | Y at X=0, Z=84.45754 | 90° | 6.35 | 0065-0069 ↔ 0118-0119 | Recognized |
| `bend-c-0058-0064` | X at Y=58.08726, Z=69.85 | 90° | 6.35 | 0062-0063 ↔ 0065-0069 | Recognized |
| `bend-c-0059-0068` | Y at X=-241.3, Z=84.45754 | 90° | 6.35 | 0065-0069 ↔ 0102-0104 | Recognized |
| `bend-c-0060-0067` | X at Y=426.38726, Z=69.85 | 90° | 6.35 | 0061-0066 ↔ 0065-0069 | Recognized |

Each has paired coaxial cylinder faces, exact source adjacency, and confidence
1.0 under this bounded recognizer.

## Source unfolding semantics

The explicit root is `region-p-0065-0069`, the dominant deck. Plane-to-flat
maps are propagated across the recognized region/bend graph. M1 uses the
**geometric mid-surface**: planar source loops are projected to paired-face
midplanes and cylindrical strips use `inside radius + 0.5 * thickness`. This
is geometric unrolling of the formed reference surface.

It is not a shop flat and makes no material/K-factor claim. A manufacturing
flat uses authored neutral-axis policy `inside radius + K * thickness`. They
coincide only when K=0.5; the artifact records `GeometricMidSurface` explicitly
so incompatible conventions cannot be compared silently.

## Recovered CTC flat and right-wall diagnosis

Generated working artifacts:

- `artifacts/sheetmetal-recovery-m1/ctc03/recognition-plan.json`
- `artifacts/sheetmetal-recovery-m1/ctc03/recovered-flat.json`
- `artifacts/sheetmetal-recovery-m1/ctc03/recovered-flat.svg`

Observed bounds are 404.754237721 × 625.305580354 mm. Inventory is 15
unfolded material regions (8 planar plus 7 bend strips), 17 exact inner loops,
7 bend lines, and 118 source ancestry entries. All planar free edges retain
native lines/arcs; circular holes remain analytic semicircle pairs and slots
retain line/arc capsules.

The actual right-wall source region is no longer ambiguous. In recovered-flat
coordinates it has:

- R12.7 outer transitions at both wall ends;
- a baseline outer carrier at X=352.445;
- two diagonal 15.24 × 15.24 transitions into recessed X=337.205 runs;
- two 63.5 mm recessed runs adjacent to the service attachment;
- a 127 mm service-bend span at X=349.804;
- the existing partial angled service flange and rounded service tab.

The earlier geometric observation of multiple levels was real, but a
*persistent multi-level edge-program abstraction* would still be
overcomplicated. The flat explains the part as an ordinary wall profile, two
local cutbacks, two rounded end corners, and a partial flange attachment.
Existing `EdgeProfile`, `CornerProfile`, and partial-flange concepts should be
extended/exposed for those ordinary operations before inventing a general edge
program. The current Sheet Metal semantic-layout adapter cannot express the
asymmetric one-chamfer cutbacks or a circular `CornerProfile`, so the canonical
native source is not patched with a misleading symmetric approximation.

## Flat comparison and reconstruction status

Against `docs/development/milestones/modules/sheetmetal/artifacts/m8/ctc03-final.firmament`, direct
comparison reports:

| metric | recovered source ↔ current native |
|---|---:|
| width residual | 0.002447 mm |
| height residual | 0.007820 mm |
| source→native RMS / p95 / max | 4.069438 / 7.836135 / 16.027648 mm |
| native→source RMS / p95 / max | 12.500820 / 17.316571 / 156.843630 mm |
| cuts | 17 ↔ 17 |
| bend lines | 7 ↔ 7 |
| status | NeedsReview |

Because the one-loop material stitch remains partial, these residuals sample
the retained region boundary set and are diagnostic, not final tooling-parity
numbers. Profile-M3's historical stitched-native contour was 2.191367 mm RMS,
4.7752 mm p95/max against the old recovered polygon reference. The two reports
measure different source authorities and must not be presented as a strict
before/after improvement.

No native refold claim is made in M1: flat parity has not been achieved, so the
workflow correctly stops before attributing remaining formed residuals to bend
direction or reference-surface convention. Existing formed Profile-M3 values
remain 8.500190/12.478269/52.816066 mm source→native and
3.607855/8.940881/12.735724 mm native→source.

## Generic fixture, performance, and determinism

`NonCtcUChannel_StepOnlyRoundTripRecognizesAndUnfoldsWithoutNativeConstructionAuthority`
exports the normal `simple-u-channel.firmament` formed body to AP242, discards
native construction authority, reimports the dumb STEP BRep, recognizes its
two bends, validates an explicit plan, and source-unfolds exact planar loops.
This proves the mechanism is not keyed to CTC IDs.

The generic imported U-channel produces a validated 12-segment outer contour,
two exact circular inner loops, two bend lines, `Valid` status, and hash
`f540958c2d8b4929aa4c88fc3c6500c759297a4dddebac2d5b11d3383250a2ee`.
Two CTC runs produce recovered reference hash
`9ca0a211b6ec2868fbf3bfdfccd1382dfc80d364554e062f214c46547d51bbde`.
One observed run measured graph validation 2.7 ms, unfold 23.5 ms, and contour
stitch 21.8 ms, excluding STEP import/recognition.

## CLI workflow

```text
aetheris sheetmetal recognize part.step --plan recognition-plan.json --json
aetheris sheetmetal recover-flat part.step --recognition-plan recognition-plan.json --out-dir recovered --json
aetheris sheetmetal compare-flat recovered/recovered-flat.json native.firmament --json
```

`recovered-flat.json` stores the absolute source path and recognition plan so
comparison reimports and revalidates source geometry rather than trusting a
stale forensic dump.

## LLM friction and architecture verdict

Source flattening materially improves reverse engineering even in its partial
state. Rounded wall ends, the two diagonal cutbacks, their exact spans, and the
service attachment's three X levels are immediately visible as 2D analytic
curves. Those relationships were difficult to distinguish from occlusion and
thickness caps in formed views.

Aetheris can now recognize a set of Sheet Metal bends on an imported dumb STEP
and flatten the imported geometry directly without first reconstructing the
part in native Firmament. It can use that reference for flat-first comparison,
reuse recognized bend values, and later refold native construction. CTC proves
that this target is substantially better for an engineer/LLM, while also
showing why status must remain `Partial` today.

The single largest blocker to a robust FeatureWorks-like workflow is exact,
topology-guided global blank stitching at legitimate point-tangent/corner
junctions. The current bounded arrangement correctly rejects three such CTC
junctions instead of hulling or silently choosing angular order. Resolving
those from the recognized region graph—while preserving line/arc ancestry—is
the next blocker; arbitrary vendor naming or a new Profile language is not.

Code quality is an acceptable localized prototype: model/authority separation
and diagnostics are clean; source-loop extraction and CLI artifact projection
are intentionally compact and need consolidation after the topology contract
converges.
