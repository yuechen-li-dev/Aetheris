# Imported through-hole suppression: feasibility experiment

> Historical experiment evidence. The production implementation now lives in
> `BrepThroughHoleRevision` and the unified Firmament binder; the experiment tool
> delegates to that kernel owner. Commands/results below describe the original
> investigation, not the current release contract. See the
> [production closeout](IMPORTED-HOLE-REVISION-PRODUCTION-2026-10-20.md).

Status: **Meaningful progression**. The basic topology suppression operation
works on two valid imported witnesses. This is an isolated experiment, not a
production Recognize/Replace implementation or a qualified hole-move feature.

Investigated against checkout `3aad55aa`. The local probe references the existing
Release Kernel.Core assembly. No production kernel, parser or UI code changed.

## What was executed

The probe imports STEP, takes an explicitly selected cylindrical wall face, finds
its two adjoining complete inner loops on planar support faces, and constructs a
successor topology that omits the wall and those openings. It retains surviving
entity IDs, surface/curve objects, bindings, pcurves and vertex points. It clears
cached construction decomposition rather than retaining stale Boolean authority.

This does not recreate the imported body from HostSize or reconstruct a Box.
The input is unmodified. Only two support-face loop lists and shell membership
change among surviving topology; unrelated face loops are checked unchanged.

The checks cover graph validation, export preflight, required pcurves, closed and
oriented boundary incidence, STEP export/reimport, surviving geometry bindings,
and unchanged input topology. These are necessary checks, not a complete
self-intersection, material-occupancy or feature-recognition proof.

| Imported witness | Result | Preservation evidence |
| --- | --- | --- |
| Fresh CLI build of FrontendX4 reference `c03-face-reference-spellings.firmament` | 10 faces to 9; four cylindrical holes to three | Other holes, surviving supports/edges and unrelated loops retained; valid source and successor preflight/pcurves; closed oriented STEP roundtrip |
| Fresh CLI build of Composite `combined-hole-edgefinish-x1.firmament` | 12 faces to 11; two cylindrical holes to one | Outer chamfer and remaining hole retained with the same bindings; valid source and successor preflight/pcurves; closed oriented STEP roundtrip |
| Original canonical-through-hole.step | Suppression produces a valid six-face body, but **excluded as a valid-input success** | Input fails preflight and pcurves, with disconnected coedges and an unclosed loop; input boundary is not closed/orientation-consistent |
| Fresh CLI build of FrontendX4 chimera.baseline.firmament, selected face 15 | Rejected before editing | Selected wall alone is not bounded by two complete planar opening loops; complete connected wall-region tracing is missing |
| Planar face selected in the four-hole witness | Rejected before editing | Cylindrical wall required |

The numerical trimmed-face volume estimate is not used as preservation evidence.
For the four-hole input and three-hole successor it even decreases on suppression
(7818.2004 to 7813.6503 mm³), with reported bounds about 1772 and 1725 mm³.
Those display-derived estimates cannot qualify the expected positive fill-volume
delta. A production gate needs independent analytic section/material evidence.

## Reproduction and retained local evidence

Probe project/source and JSON/STEP results are retained under ignored
`artifacts/local/hole-suppression-experiment/`. In PowerShell from the repo root:

```powershell
dotnet build artifacts/local/hole-suppression-experiment/Probe.csproj -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/FrontendX4/cases/reference/c03-face-reference-spellings.firmament --output artifacts/local/hole-suppression-experiment/four-holes.step --json
dotnet artifacts/local/hole-suppression-experiment/bin/Release/net10.0/Probe.dll artifacts/local/hole-suppression-experiment/four-holes.step 7 artifacts/local/hole-suppression-experiment/three-holes.step
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/Composite/combined-hole-edgefinish-x1.firmament --output artifacts/local/hole-suppression-experiment/chamfered-two-holes.step --json
dotnet artifacts/local/hole-suppression-experiment/bin/Release/net10.0/Probe.dll artifacts/local/hole-suppression-experiment/chamfered-two-holes.step 11 artifacts/local/hole-suppression-experiment/chamfered-one-hole.step
```

This local experimental source is not a distributed tool. Its failed admissions
throw exceptions; it has no public diagnostic contract. Production work must move
the admitted algorithm into the owning topology-edit seam with structured errors
and permanent regression tests. The probe built with zero warnings/errors. No
full-solution regression qualification is claimed for this experiment. No .NET,
testhost, MSBuild or VBCSCompiler process remained after the run.

## Proposed production boundary

Start with isolated round through-holes connecting two parallel planar supports
in one valid closed, oriented manifold body. The surrounding body need not be a
Box. Require actual circular rim evidence, coaxial cylindrical wall coverage,
inward material orientation, and a fully traced connected void region. A cylinder
is not by itself a hole: it could be an outside boss or a partial cut.

Initially reject intersecting holes, breakouts, threads, blended entries, blind
holes, ambiguous imports, and unsupported surfaces. Handle split cylindrical
walls only after grouping connected, geometrically equivalent wall patches and
proving their complete external rims. The Chimera refusal is a concrete grouping
gap, not evidence that the part is inherently uneditable.

Use the existing Recognize/Replace authoring direction, with an internal atomic
transaction:

1. Recognize and prove the void against a particular imported body revision.
2. Plan a local face/loop rewrite and its explicit correspondence/deletions.
3. Suppress the old hole while retaining the actual residual BRep.
4. Prove the entire destination cylinder lies in the admitted material column,
   clears other features and stays within the authorized edit envelope.
5. Insert new semantic hole topology into that retained body.
6. Validate and export/reimport the successor, or discard the whole transaction.

The next unimplemented editing seam is **inserting a new hole into the retained
imported topology**, including destination clearance, circular trims, wall seams,
orientation and pcurves. The existing Box-based replacement materializer does not
provide that preservation contract. First qualify suppression plus reinsertion
at the same location as a control, then a real move. Verify old-axis material,
new-axis void, analytic volume change and unaffected topology independently.
Equal volume alone cannot prove a correct move.

## Region's role

Region is the right ownership abstraction, but declaring one does not itself
prove locality. Bind a proved region to its wall faces, opening loops, support
faces, body revision and permitted geometric envelope. Compare the actual edit
against that write contract. Global reads for collision checks remain necessary.

Support faces may span the whole part. Removing one inner loop changes that
face's trim list while retaining its support surface, outer loop and every other
hole. Locality therefore applies to the edited boundary and material envelope,
not a rule that every touched face must fit inside a small box.

Keep unchanged identities and PMI associations. Mark deleted topology as retired,
create fresh semantic identity for the new hole, and retain replacement history.
Report affected associations explicitly; do not silently map them to a nearby face.

## Counterbore and countersink follow-ons

Both are plausible extensions of the same void-region operation. A counterbore
adds a larger coaxial cylindrical wall and annular shoulder; a countersink adds
a coaxial conical mouth. Suppression removes the complete proved void boundary
and its two external opening loops. Recognition must prove connectivity,
material side, radii, depth ordering and cone/shoulder relations.

The existing Boolean hole-family recognizer uses authored Box-root construction
history; it is not imported STEP recognition. Its family definitions may be
reused, but cannot replace topology evidence. Neither compound family, automatic
discovery, destination insertion, PMI remapping nor vendor STEP qualification was
implemented or demonstrated in this experiment.

## Recommendation

Proceed with bounded imported-hole defeaturing and semantic reinsertion. The
suppression primitive is experimentally viable without general surface healing.
The substantial work is trustworthy recognition, destination material proof and
transactional topology insertion. Keep those gates explicit instead of growing
the old HostSize-based reconstruction path into an apparent general editor.
