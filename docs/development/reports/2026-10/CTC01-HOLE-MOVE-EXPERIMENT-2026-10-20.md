# CTC-01 local hole-move experiment

> Historical experiment evidence. The production implementation now lives in
> `BrepThroughHoleRevision` and the unified Firmament binder; the experiment tool
> delegates to that kernel owner. Commands/results below describe the original
> investigation, not the current release contract. See the
> [production closeout](IMPORTED-HOLE-REVISION-PRODUCTION-2026-10-20.md).

Result: **Success for the experimental study**. One real NIST CTC-01 through-hole
moves without reconstructing the whole part or invoking general Boolean surgery.
Production Recognize/Replace is unchanged and is not claimed qualified.

## Input and motivating edit

- Checkout investigated: `3aad55aa`.
- Source: `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp`.
- Source SHA256: `85a5752da05f53c456ca3a9e038c90358e1d5a3141d1f0d6e5f0970f2356e821`.
- Imported BRep: one shell/body, 117 faces, 318 edges, 206 vertices; enclosed
  manifold topology, 56 planes, 57 cylinders and four cones.
- Selected void: sequential faces 1/2, original STEP #764/#765. Two connected
  cylindrical half-walls form a Ø25 mm hole at (-160,45), Z=-50..0 mm.
- Edit: move +30 mm in X to (-130,45), keeping diameter, depth and supports.
- Local stock envelope: min (-175,30,-50), max (-115,60,0) mm. Conservative
  bounds exclude all 113 unrelated boundary faces. The two planar supports and
  two hole walls are the only permitted intersecting faces.

Unlike the earlier canonical-box suppression witness, this is the original
complex NIST part. No full-part reverse engineering, mesh replacement or guessed
HostSize is used.

## Compared approaches

| Approach | Executed result | Conclusion |
| --- | --- | --- |
| Literal subtract-a-box, then union replacement stock | Actual `BrepBoolean.Subtract` rejects the first cut: bounded family only supports recognized axis-aligned Box hosts. Union was not attempted. | Not a viable existing route for this import; do not broaden generic Boolean scope for this task. |
| Direct proved-boundary relocation | Moves the two wall supports, six edge curves, four vertices and planar opening pcurves; topology IDs retained; STEP roundtrip succeeds. | Small, effective optimization for unchanged hole shape within a proved stock prism. |
| Suppress then insert fresh topology | Intermediate has 115 faces / 312 edges / 202 vertices. Inserts a translated proved hole patch with fresh topology IDs, returning to 117 / 318 / 206; STEP roundtrip succeeds. | Preferred semantic production contract: retire the old void and insert a named replacement while preserving residual identities. |

The two successful strategies have matching boundary geometry signatures. This
is not a performance benchmark. The second strategy currently copies a proved
hole patch; it does not yet call a new semantic Hole materializer or carry PMI.

## Evidence and checks

The retained experimental tool is `tools/Aetheris.ImportedHoleExperiment`.
Its CTC-specific `qualify` command checks eight cases:

| Case | Outcome |
| --- | --- |
| +30 mm X, disjoint old/new holes | Both strategies accepted experimentally; old axis becomes material, new axis becomes void in the certified local analytic stock model |
| +5 mm X, overlapping old/new holes | Both accepted; avoids assuming old/new voids must be disjoint |
| Zero displacement control | Both accepted, unchanged boundary geometry |
| +320 mm X crossing other features | Rejected before editing: conservative boundary conflicts |
| -250 mm X breakout | Rejected before editing: outer boundary conflict |
| Exterior cylindrical face 4 | Rejected: outward material orientation |
| Blind candidate face 13 | Rejected: not two complete planar inner openings |
| Ø35 mm lobe hole, seed 72, +5 mm X | Conservatively rejected against faces 3/26/28; not evidence that the physical edit is impossible |

For accepted cases, the tool verifies:

- Complete connected cylindrical wall groups, full circular rim coverage and
  matching axial extents; exterior rounds are not mistaken for holes.
- No source-body mutation on acceptance or refusal.
- Closed, oriented two-use edge incidence and valid topology references.
- Exact equality of unchanged supports/bindings, edge geometry/topology, vertex
  coordinates and retained pcurves in memory. Other opening loops on both support
  faces remain unchanged. 115 faces and 312 edges are retained by identity.
- Recognition of the relocated hole again after STEP reimport.
- Matching parameterization-independent analytic support and trimmed-boundary
  signatures through roundtrip, including an unmodified whole-body control.
  Numeric signature values are rounded to 1e-8; this is **not** a Hausdorff bound
  or a general exact-equivalence proof.
- Analytic local fill volume and cut volume both 24543.69260616987 mm³; net local
  volume change zero. This uses the proved local stock prism, not a mesh-volume
  assertion. Equal volume is not used alone as edit correctness evidence.

## Qualification limits found, not hidden

1. **Inherited trim gaps.** The original import is globally degraded: 32 coedges
   lack qualified pcurves. These same diagnostics persist through both edits and
   their roundtrips; no new pcurve diagnostic is introduced. Worst measured lift
   among existing bindings remains about 9.55e-9 mm. Missing bindings do not count
   as zero error or as a successful whole-body qualification.
2. **Native versus imported tolerance.** Native 1e-6 mm preflight reports 22
   pre-existing off-surface trim errors outside the selected region. The initial
   enforced export failed on these. The experiment explicitly uses a 0.001 mm
   imported-geometry budget, stricter than this source's declared 0.01 mm accuracy.
   That preflight passes without disabling checks. Both results are recorded;
   this does not repair pcurves or silently change production tolerances.
3. **Generic occupancy queries.** The existing global point-containment API
   returns Unknown at both axes for source, successor and roundtrip. The positive
   material witness instead follows from inward wall orientation, complete local
   rims, boundary exclusion over the stock prism and the actual circular opening
   after reimport. That local analytic reasoning is explicitly distinct from an
   independent whole-body point query.
4. **Semantics.** Experimental exports preserve the BRep geometry under the above
   checks, but do not migrate semantic PMI, product metadata or external bindings.
   They are not manufacturing-release artifacts. Fresh wall bindings retire their
   original STEP entity IDs; semantic feature correspondence still needs integration.
5. **Scope.** Only unchanged-radius/depth, Z-axis through-holes between parallel
   planar supports are implemented. No arbitrary healing, diameter changes,
   counterbore/countersink moves, blind/intersecting/threaded/blended hole edits,
   or general kernel Boolean extension is claimed.

## Reproduction and artifacts

See the [tool README](../../../../tools/Aetheris.ImportedHoleExperiment/README.md) for
commands. Generated output defaults to ignored `artifacts/local/`.

```powershell
dotnet build tools/Aetheris.ImportedHoleExperiment -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet tools/Aetheris.ImportedHoleExperiment/bin/Release/net10.0/Aetheris.ImportedHoleExperiment.dll testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp qualify artifacts/local/ctc01-hole-move/qualification
```

Local evidence includes `qualification/cases.json`, `control.json`,
`literal-box-cut.json`, each case's recognition/report JSON and successful
`direct.step` / `replace.step` artifacts. `comparison.png` compares compiler
wireframes of the original and moved BRep with the same world-space detail crop.

## Production decision

Use **local stock-region proof plus an atomic semantic replacement**. Keep the
existing Recognize/Region/Replace vocabulary, remove HostSize reconstruction from
this path, and make the allowed envelope explicit. Direct relocation can be a
qualified optimization; it cannot bypass the same proof or semantic contract.

The [production syntax proposal](../../architecture/imported-hole-revisions.md)
defines typed fields, source pinning, world-space placement, same-size admission,
identity/provenance, diagnostics and validate/build agreement. It is explicitly
not parser-backed yet. InlineStep's current canonical-header restriction also
needs real import-capability admission, rather than a header workaround.

The concrete production prerequisites are now isolated: qualified residual import
handling (notably CTC's 32 missing trims), semantic Hole insertion, and association
migration. These belong to their existing owners; no new document parser or
general Boolean surgery is needed to continue this bounded operation.

## Repository qualification

The tool builds with zero warnings/errors and its eight-case qualification passes.
The solution Release build passes with 34 warnings and zero errors. Required lanes:

```powershell
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter 'Category!=SlowCorpus' -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
```

- Fast lane: 1087 passed, zero failed/skipped.
- Full solution: 5808 passed across 20 projects, zero failed/skipped, including
  SlowCorpus and the EditableV8 tests.
- A dedicated source-quality pass removed duplicate validation and an unused
  parameter, checked conservative oblique-axis bounds, enforced comparison results
  rather than merely reporting them, and checked untouched support-face loops.
- `git diff --check` passes. Tool-only whitespace formatting was applied.
- No dotnet/testhost/MSBuild/VBCSCompiler hosts remained after qualification.

Logs are retained under ignored `artifacts/local/ctc01-hole-move/`. Only the
isolated experimental tool and documentation change; production frontend/kernel
implementations remain unchanged. No baseline fixtures or snapshots were edited.
