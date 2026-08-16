# Sheet Metal M11 — finite bend/root termination

Status: **meaningful progression**. Aetheris now represents finite bend endpoints
as stable Sheet Metal semantics with explicit and bounded automatic treatment,
generic ProfileDelta ancestry, exact formed/flat dogfood, DFM, conflicts, and STEP
round-trip coverage. CTC-03's formed bidirectional residuals improve after the
four pictured side-root ends receive semantic treatments, but endpoint-local
parity is not proven and the part remains `NeedsReview`: unrelated global
base-region contour detail still has an 8.945 mm maximum residual.

## Architecture and diagnosis

The orange-circled geometry is owned by `LeftWallBend` and `RightWallBend`, not
by an ordinary outer-profile corner. Recovered flat evidence shows both side bend
strips are bounded rectangles whose roots reach to thickness-scale offsets from
the front/rear boundaries. The previous native construction let generic profile
corner compatibility override root extent, producing the formed wedges. M11 makes
the bend endpoint the final authority for that extent.

Real types are `SheetBendEnd`, `SheetBendTerminationTreatment`,
`SheetBendTerminationIr`, and the optional `SheetBendIr.StartTermination` /
`EndTermination`. Each non-natural termination retains a
`SemanticProfileDeltaIr` with stable level/member descendants. `Trimmed` changes
the finite root extent; `Rounded` uses the existing analytic semantic-corner
adapter at the adjacent planar profile; `Natural` emits no modification.

Current paths are:

- `LeftWallBend.StartTermination` / `.Finish`
- `LeftWallBend.EndTermination` / `.Finish`
- `RightWallBend.StartTermination` / `.Finish`
- `RightWallBend.EndTermination` / `.Finish`

The CTC source explicitly uses `Trimmed` with 0.9525 mm (half nominal thickness)
at all four ends. It retains seven bends, 17 openings, one exact connected blank,
17 inner loops, no relief loops, and source-independent native construction.

## Auto and judgment

`Auto` currently admits one bounded rounded family after checking finite setback,
depth, minimum half-thickness radius, and available root/profile extent. Unsafe
requests refuse before geometry. `JudgmentEngine` verdict: **Partial / not yet
used**. The problem shape is compatible with JudgmentEngine once multiple valid
treatment families exist, but scoring a single admissible construction would add
ceremony without a competing interpretation.

## CTC comparison

Against the corrected M10 baseline:

| Measure | M10 | M11 |
|---|---:|---:|
| formed source→native RMS | 3.546889 | 3.485097 |
| formed source→native max | 9.141310 | 8.991475 |
| formed native→source RMS | 2.667873 | 2.573147 |
| flat source→native RMS | 2.886221 | 2.872865 |
| flat source→native max | 9.094054 | 8.945201 |
| flat native→source RMS | 2.384335 | 2.413599 |
| bounds width / height residual | 0.002447 / 0.007820 | 0.002447 / 0.007820 |

The left-wall local flat residual improves from 1.90637 to 0.95387 mm. The
right-wall aggregate remains influenced by its service recess and regresses from
3.80101 to 4.75351 mm, so this report does not claim local contour completion.
The formed bidirectional RMS and former dominant maximum both improve, which is
meaningful progression but not sufficient evidence to declare all four pictured
ends visually exact.

CTC-03 is therefore not declared complete. The exact remaining manufactured
discrepancy is the main-deck/shared-boundary outer contour (`region-p-0065-0069`,
8.9452 mm maximum) plus right-wall aggregate service-boundary residual; neither
is silently assigned to bend termination.

## Validation scope

`SheetMetalM11Tests` covers explicit Rounded/Trimmed, Natural, bounded Auto,
Auto refusal, CornerProfile conflict, root ProfileDelta conflict, stable paths,
DFM, exact flat, formed preflight, formed and flat STEP export/reimport, CTC
inventory, and source isolation. The full repository build/test result and final
CLI timings/hashes are recorded in the task report that produced this bundle.
