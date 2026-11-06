# Thread tip run-out

## Problem

The HexBolt thread stopped 1.3 mm before the tip chamfer behind a planar end cap, leaving a full-diameter band at
the tip. A nut cannot start on that: a real die-cut thread runs out through the chamfer.

## Construction

`BrepHelicalRib.CreateWithTipRunOut` continues the groove until its last section clears the material, then clips
every groove and land face by the chamfer. The material is one half-plane in each face's own parameters, because
radius and axial position are linear in both the helix angle and the profile coordinate:

| Face | Clip line | 3D clip edge |
| --- | --- | --- |
| Land (major cylinder) | axial = chamfer start | circle arc on the rim |
| Groove bottom | axial = where the cone reaches the minor radius | circle arc |
| Flanks | linear in (angle, profile) | conical spiral, certified Hermite B-spline (bound r + 4·dr/dθ) |

Edges are clipped once and shared, so each clip vertex joins exactly two clip edges. Walked the other way, the clip
edges close one loop that winds once forward around the axis (checked). The chamfer cone is that loop plus a
generator seam from the groove bottom's leading corner to the end rim, and the full end rim. All cone pcurves are
lines. The flat end face stays whole when the tip radius lies inside the minor radius, which is required.

`HexBoltThreadComposition` replaces the shank, tip chamfer and end face, and shares only the head-side rims.
A clipped straight edge restarts its line at the clip point: STEP re-parameterizes a line from its start, and a kept
interval starting mid-line otherwise disagreed with its pcurve on reimport.

## Evidence

- Showcase bolt (threaded 47.5 mm from the tip): OCCT reads one valid solid, 290 faces, volume 2938.2 mm³. Volume
  moves against the previous build as the groove budget predicts (≈9.2 mm³ per mm of groove).
- Kernel display tessellation shades every face (`CanonicalThreadDisplayAudit`).
- `GrooveRunsOutThroughTipChamfer` sweeps six phases of the last turn: manifold edges, pcurves within 1e-6, no
  vertex outside the chamfered stock, STEP round trip, enclosed and consistently oriented. A 25-phase sweep passed
  during development.

## Follow-ups

- `aetheris mesh` on the threaded bolt STEP runs out of memory; the pre-change bolt does too, so it is not caused by
  the run-out.
- No runout under the head is modeled.
