# Imported feature recovery assistance — 2026-10-21

## Contract

Agents can inspect an imported STEP, select a proved round through-hole from
measured facts, give it a semantic name and generate an executable Firmament
revision. This is bounded feature recovery, not reconstruction of the original
feature history. The existing production revision owner remains authoritative.

The [public workflow](../../../public/firmament/imported-feature-recognition.md) documents
CLI and SDK use. The [recovered CTC-01 fixture](../../../../fixtures/Regression/InlineStep/production/ctc01-recovered-hole-move.firmament)
is source evidence. Generated output and detailed logs remain ignored under
`artifacts/local/imported-feature-assistance/`.

## Decisions and ownership

- Reuse named `Region` as the semantic alias. CLI `--as MountHole` chooses that
  Region and replacement feature name; no additional Firmament keyword is needed.
- A Region's `candidate` selects an exact inspected handle, mutually exclusive
  with raw/sequential face selectors. Handles use sorted source wall entities,
  scoped to the explicit source pin. Stale IDs reject; there is no closest match.
- Discovery shares the production connected-wall traversal and recognition proof.
  Measured groups that fail recognition remain visible with reasons. Inspection
  is explicitly `InspectionOnly`; `CanPlan` requires family and import admission,
  but does not claim destination clearance.
- SDK `FirmamentImportedFeatures.Inspect` retains an immutable source snapshot.
  `PlanMove` derives local bounds, executes Core, emits ordinary Firmament and
  compiles it through normal build/export admission. File-backed planning rechecks
  the source pin through the existing loader.
- Destination intent is one named Concept Struct Point3. Generated destination
  bounds reference its components. Changing that point rechecks stock and collision
  admission, including failures, without duplicating absolute coordinates.
- `within` accepts either the existing single envelope or exactly two local bounds
  objects for suppression and reinsertion. No geometry is swept between them.
  Every unrelated boundary is excluded from both regions with import uncertainty.
  A separate destination must also be inside both trimmed support material domains
  (excluding only the hole being suppressed). The
  existing planar-domain owner handles line/circle outlines with an explicit
  chord-sag/endpoint-error bound; unresolved outlines and ambiguous probes refuse.
  It never uses display-oriented whole-body bounding boxes as containment proof.

## Motivating witness and safety

The CTC-01 upper-left diameter-25 hole moves from (-160,45,0) to (130,45,0) mm,
across the central boss. A single enclosing prism rejects on the intervening
boundaries; two local prisms admit the edit without modifying the boss.
The tests assert retained surfaces, 115 surviving faces and 312 surviving edges.
Existing production tests retain full residual bindings, topology, pcurves,
qualified STEP roundtrip and new diameter/depth semantic PMI associations.

An occupied destination and an entirely exterior destination both reject.
The latter exposed why boundary exclusion alone is insufficient for disconnected
regions; the new support-domain proof refuses before materialization rather than
relying on a later roundtrip failure. A separate synthetic STEP regression proves
that the full interior of an existing prismatic opening is excluded even when its
walls are outside a local probe. Tests also exercise exact candidate identity,
stale selectors, conflicting selection fields, malformed region lists and edited
named targets. Validate and build use the same compiler admission path.

Original STEP text is untouched. `textSha256` changes line endings only for the
explicit pin calculation; import consumes original text. CTC-01 still needs an
explicit .01 mm numerical import budget; strict .001 mm is not silently relaxed.
`geometryOnly: true` remains required and reported. New hole PMI is generated;
original vendor PMI is excluded, not claimed as preserved.

## Qualification

Final qualification passed:

| Check | Result |
| --- | --- |
| Full Release solution build | Passed; 0 errors, 34 warnings in existing code |
| Focused kernel revision cases | 10 passed |
| Existing production-owner harness | 8 passed: 3 accepted moves, 5 refusals |
| Fast kernel lane, excluding SlowCorpus | 1,088 passed |
| Complete solution lane, including corpus and editable V8 | 5,844 passed in 20 non-empty test assemblies; 0 failures/skips |
| Real CLI inspect → fact-based selection → plan → validate → build → analyze | Passed |
| Strict import inspection | Measured groups reported; 0 candidates admitted for planning at .001 mm |
| Exterior/occupied destination plans | Rejected; no new source file written |
| Golden source versus emitter | Identical except relative import path |
| Documentation local links / diff whitespace | Passed |

The CLI inspection reports 47 cylindrical groups, of which 8 pass current family
and import admission at .01 mm. Revised STEP analysis reports one enclosed
manifold body, 117 faces, 318 edges, 206 vertices and 636 pcurves. Build evidence
confirms replacement diameter PMI. The original STEP byte hash remains
`85a5752da05f53c456ca3a9e038c90358e1d5a3141d1f0d6e5f0970f2356e821`.
No source geometry was changed to obtain import qualification.

Reproduction lanes:

```shell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 -nr:false --filter "Category!=SlowCorpus" -- RunConfiguration.MaxCpuCount=1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
```

The first full test run was deliberately superseded after strengthening the
trimmed-material containment proof; the reported total comes solely from the
complete final run. Superseded task-owned runtime hosts were stopped before
rebuilding. Existing nullable/obsolete/platform/trimming/WASM interop warnings
remain outside this change's scope. FrictionLab discovers no tests, as before.

## Engineering authoring precision follow-up

Generated nominal lengths and coordinates now round to a 0.1 mm grid, using
nearest-tenth rounding with midpoint ties away from zero. Bounds/padding round
outward, eliminating imported floating-point noise from editable source.
Negative zero is normalized. The new diameter and semantic PMI are genuinely
25 mm, and a requested X=130.04 mm is authored and built at X=130 mm.

This is authoring policy, not a change to kernel epsilon or a claim that the
source carried a particular manufacturing tolerance. Numerical import accuracy
still emits .01 mm where requested. The rounded program re-enters normal
admission; unsupported resizing or off-plane placement rejects. Original source
and residual geometry remain untouched. The recovered fixture is regenerated
through the real CLI, and its STEP/PMI witnesses verify the rounded values.

Follow-up qualification passed: full Release solution build (0 errors, 25 warnings
in existing code), the focused rounding/STEP/PMI regression, the 1,088-test fast
lane and all 5,844 solution tests across 20 non-empty assemblies, with no failures
or skips. The real CLI regenerated the fixture, validated, built and analyzed the
rounded revision. It retained 115 faces and 312 edges and emitted nominal 25 mm
hole PMI. Logs and artifacts are under `artifacts/local/imported-feature-rounding/`.

Code review confirmed that rounding belongs to authoring/emission, while ordinary
compiler admission and numerical import qualification retain their existing
contracts. No kernel accuracy threshold or general formatter was relaxed.

## Code-quality review

Recognition traversal and legality remain shared with Core execution. Validate,
build and generated plans use the ordinary compiler owner. The change adds no
whole-document parser, rewrite pass, error-code fatality allowlist, alternate
geometry engine, global parser state or product-specific validation path.
The planar containment query explicitly refuses unavailable evidence rather than
using display bounds. CLI failures use the existing diagnostic presentation owner.
New output defaults to ignored local artifacts; only the durable source witness
and compact documentation are added for review.

## Verdict

**Success for the bounded recovery-assistance slice.** A candidate chosen from
measured geometry becomes a named, pinned, executable Firmament revision. Both
its acceptance and refusal go through the existing production owner. Geometry,
provenance and annotation boundaries remain explicit.

## Remaining limits

One isolated same-size Z-axis round through-hole between two planar supports in
one qualified body. No resizing, arbitrary frames, counterbores/countersinks,
blind/intersecting/blended/threaded families, multiple atomic edits, vendor PMI
migration or UI editing. Separate local regions currently require line/circle
support trim boundaries with a resolvable containment probe. Other producers need their
own qualification; this fixture is not universal imported-feature certification.

The next useful expansion is qualification against more independent STEP
producers and bounded families, retaining exact selection and refusal evidence.
