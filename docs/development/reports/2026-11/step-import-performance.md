# STEP import performance

STEP import was slow for a reason other than parsing. The parser reads 10–15 MB/s. Nearly all of the time went into
work done by exhaustive search where the problem has structure. This pass replaces four such searches.

## Measured (native, Release, best of three warm imports)

| File | Before (ms) | After (ms) | Export hash |
|---|---|---|---|
| CTC-01 | 288 | 201 | same |
| CTC-02 | 8465 | 1481 | changed |
| CTC-03 | 118 | 75 | same |
| CTC-04 | 1441 | 514 | same |
| CTC-05 | 2253 | 317 | changed |
| FTC-06 | 301 | 148 | same |
| FTC-07 | 1193 | 666 | changed |
| FTC-08 (tessellated) | 73 | 45 | same |
| FTC-08 | 1009 | 217 | same |
| FTC-09 | 1422 | 238 | same |
| FTC-10 | 871 | 501 | changed |
| FTC-11 | 102 | 93 | same |
| STC-06 | 286 | 179 | same |
| STC-07 | 1046 | 526 | changed |
| STC-08 | 1000 | 180 | same |
| STC-09 | 1582 | 441 | same |
| STC-10 | 884 | 501 | changed |
| STEP Tools Boxy | 80 | 43 | same |
| **Total** | **22 414** | **6 366** | |

Small files vary by about ±15 % from run to run.

Every file keeps the same face count, edge count and qualification status. Where the export hash is the same, the
export bytes are identical.

## What changed

### 1. B-spline point projection converges instead of searching

`BrepPcurveRecovery.ProjectSplinePoint` inverts edge points onto B-spline faces. It runs at every sample of a
recovered pcurve.

**Before.** It ran Gauss-Newton and accepted only a distance of at most 1e-8 mm. A point off the surface, such as a
tolerant edge's gap, never reaches that distance. In those cases the projection fell through to a pattern search:
it started at 1/32 of the domain and halved the step down to 1e-13, which is about 350 surface evaluations per point.
On CTC-02, 89 % of the 132 312 projections ended in that search.

**Now.** The projection recognises the foot point it has already reached:
- **A converged step.** A full Gauss-Newton step that moves the surface point by less than 1e-12 mm is the foot
  point.
- **A step that stops descending.** A step under 1e-9 mm that no longer reduces the distance is also the foot point;
  1e-9 mm is the floor of the finite-difference tangents. The distance left over is second order in that step.
- **The domain boundary.** Trimming curves often run along the boundary, where the closest point is a constrained
  minimum. A parameter that the descent direction holds at its bound is fixed there, and the step is solved in the
  other parameter.

The pattern search remains as the fallback for these cases:
- a degenerate normal equation;
- descent that stalls far from a foot point;
- the iteration bound;
- a stationary point whose distance exceeds the tolerance. A seed carried over from the previous sample can settle
  in a local minimum, and the coarse pattern steps escape it.

**Evidence.** On every projection call in the NIST corpus, both versions ran on the same inputs. The comparison
covered 188 613 calls.
- The success decision agrees on every call.
- The new residual is never worse than the old by more than 1.2e-11 mm, except inside the radius both versions
  already accept as on-surface (1e-8 mm).
- Six export hashes change. The surface points of their pcurve vertices move by up to 3e-7 mm, along the flat valley
  of the distance function.

### 2. Loop containment stops at its first counterexample

The planar loop classifier counts, for every pair of a face's loops, whether one contains the other.

**Before.** Each test counted every edge crossing and measured every vertex's distance to the other loop, then used
only "no crossing, all vertices inside".

**Now.** The test checks the vertices first and stops at the first one outside, then looks for any crossing and stops
at the first. Most pairs of loops on a face are siblings, so the first vertex settles them.

**Evidence.** The predicate is the same and both halves are pure, so the result is identical: every export hash is
unchanged by this step. FTC-08 went from 1216 ms to 349 ms and STC-08 from 917 ms to 197 ms.

### 3. Orientation reads its sign from a coarse mesh first

`StepFaceOrientationResolver` uses only the sign of each closed component's signed volume.

**Before.** It took the sign from the full display tessellation.

**Now.** It first meshes coarsely: 45°, a 0.5 mm chord, and up to 256 segments. The coarse result decides the sign
only when every deciding component's volume is more than four times its chord times its area, which is more than the
mesh's chord error can move. Otherwise the resolver measures on the display mesh, exactly as before.

**Evidence.** The coarse mesh is about 6× faster on CTC-02 (109 ms against 674 ms). Every face orientation is
unchanged: all export hashes are the same as before this step.

The reported candidate volume is the coarse mesh's when that mesh decided. Both meshes approximate the volume;
nothing reads more than its sign.

### 4. Exact evaluation and export without repeated scans

- **`BSpline3Curve.Evaluate`.** It now runs on flat views of its arrays with stack scratch. It no longer allocates
  or makes interface calls per evaluation. The arithmetic and its order are unchanged, so results are bit-identical.
- **STEP export vertex resolution.** It used to scan every face and every edge for each vertex. It now builds the
  vertex-to-face and vertex-to-edge incidence once per export, in the same id order. Export bytes are identical:
  CTC-02 export dropped from 932 ms to 534 ms.

## Snapshots

The shared NIST snapshot is updated for the six changed canonical hashes. Each of the six was a plain hash update;
every other report field is unchanged. The Linux overrides are unchanged.

The Windows x64 and Apple Silicon overrides for CTC-02, FTC-07, FTC-10, STC-07 and STC-10 are now stale.
`NistCorpus_PerFile_AuditReport_MatchesSnapshot` fails on those platforms until those overrides are regenerated
there. CTC-05 has no override, so the shared snapshot covers it on every platform.

## Tests

`SplinePointProjectionTests` covers three cases:
- a target off the surface by 5 µm to 1 mm projects to its foot point, at its gap;
- a target beyond the parameter rectangle projects to the boundary foot point, no farther than a dense independent
  search finds;
- a seed from the far corner still reaches the target.

## Not done

- **Proof sample counts.** The dense lift-residual samples that qualify each recovered pcurve (8193 on B-spline
  faces) are qualification evidence and are unchanged. They are now the largest remaining cost of recovery.
- **The browser build.** The browser AOT build was not re-measured; it was about 10× slower than native before this
  change.

## Verdict

Meaningful progression. Import of the NIST corpus is 3.5× faster natively, and the slowest file is 5.7× faster.
Every outcome is unchanged: the same faces, edges, qualification and orientation. Two of the four changes are
bit-identical. The projection change moves recovered pcurves within the existing on-surface tolerance.
