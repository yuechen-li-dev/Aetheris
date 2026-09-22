# STEP-ORIENTATION-X1C

## Executive verdict

X1C preserves the STEP boundary forms that previously fell through the X1/X1B qualification boundary: face-local pcurves and explicit pole/apex `VERTEX_LOOP` topology. The derived face-orientation model is unchanged, and source `ADVANCED_FACE.same_sense` remains diagnostic provenance only.

## Pcurve coverage

| STEP 2D form | Parsed | First-class binding | Deliberate export | Round-trip evidence |
| --- | --- | --- | --- | --- |
| `LINE` | yes | yes | exact `LINE` | cylinder seam fixture |
| `POLYLINE` | yes | yes | exact points | planar plateau corpus |
| `CIRCLE` | yes | yes, placement retained | exact conic | cylindrical solid cap fixture |
| `ELLIPSE` | yes | yes, coefficient frame retained | exact conic for STEP-admissible placement | shared conic path |
| `B_SPLINE_CURVE_WITH_KNOTS` | yes | yes | exact degree/controls/knots | planar plateau corpus |
| `RATIONAL_B_SPLINE_CURVE` | yes | yes | exact weights plus spline data | hostile seam fixture |
| parameter `TRIMMED_CURVE` | yes | yes, interval and sense retained | represented basis/domain | decoder coverage and corpus |
| `COMPOSITE_CURVE` | no | explicit unsupported diagnostic | no | not encountered in qualified fixtures |

The planar plateau witness imports and re-imports 144 pcurve uses. The seam witness retains two distinct UV uses of one 3D edge at `U=0` and `U=2*pi`; they remain periodic equivalents without being deduplicated. A one-period shift, reversed edge use, rational weights, and sub-tolerance drift are covered by focused tests.

Unsupported associated curve forms fail with `Importer.Pcurve.UnsupportedCurve`. Material pcurve/3D disagreement is rejected by `BrepPcurveValidator`; drift within the import tolerance remains admissible.

## Vertex-loop coverage

`LoopKind.Vertex` stores one actual vertex and zero coedges. `VertexLoopParameterBinding` retains its face, support, UV location when available, and source STEP IDs. The handcrafted spherical pole round-trips as one real `VERTEX_LOOP` with zero edges. STC-06 imports and re-imports its two cone-apex vertex loops without fake geometry; its shell remains explicitly ambiguous because the new topology does not supply sufficient global orientation evidence.

## Authority and policy

No orientation repair moved into pcurve ingestion. `same_sense` is still source evidence only and has zero downstream orientation authority. Period arithmetic reuses `SurfacePeriodicity`; seam assignment and vertex-loop admission are deterministic. JudgmentEngine remains appropriate only for the existing multiple-admissible-encoding export policy, where explicit boundary roles outrank authored-order fallback and partial or contradictory evidence is rejected.

## Inspection

Normal `aetheris analyze` output now adds a concise boundary summary: edge-loop count, vertex-loop count, pcurve count, seam-pair count, and pcurve source types. This makes preserved topology visible without expanding the default report into entity-level noise.

## Qualification

X1C is **Accepted** against its bounded scope. Validation on 2026-09-22 produced:

| Check | Result |
| --- | --- |
| solution build | passed, 0 errors (7 existing WebAssembly/trimming warnings) |
| focused X1C pcurve/vertex-loop tests | 6/6 passed |
| STEP round-trip corpus | 17/17 passed |
| deterministic NIST aggregate snapshot | passed after the intentional canonical snapshot update |
| NIST display corpus | 18/18 passed |
| display-orientation corpus | 16/16 passed |
| CLI suite | 445/445 passed in 54 seconds |

The CLI unsupported-curve detail test now imports STC-06 once to select a representative edge and invokes the public CLI once. It no longer performs 310 complete imports; the focused test completes in about six seconds while retaining the externally visible contract assertion.

A serialized solution-level invocation still lets xUnit parallelize expensive corpus cases within `Aetheris.Kernel.Core.Tests`. Under that contention, display/volume tests exceeded their own bounded execution budgets and the aggregate run was stopped after the failure mode was isolated. The same affected suites pass independently with the counts above; this report therefore does not claim a green all-at-once aggregate.
