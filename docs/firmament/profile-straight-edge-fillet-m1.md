# Profile straight-edge fillet M1

M1 materializes one authored `Line2` segment on the outer loop of a prismatic
Profile extrusion. It is a source-bound operation: `Target` names
`Profile.Loop.Segment`; no B-rep edge lookup is performed.

```firmament
Modify Body {
    EdgeFinish SouthTopRound {
        Target: Bracket.Outer.South
        On: Top
        Kind: Fillet
        Radius: 2mm
        EndClearance: 3mm
    }
}
```

`On` is `Top` or `Bottom`. `EndClearance` is the distance retained at both
source-segment ends. If omitted it is exactly `Radius`; the emitted plan records
that `FilletSpanInset` policy. The span is `[EndClearance, Length-EndClearance]`
and must be non-empty.

For a directed outer-line segment `P0 -> P1`, M1 derives tangent `t` and the
inward profile normal `n` from loop winding. For Top, the cylindrical axis is
parallel to `t` and its centreline is `r*n-r*z` from the sharp edge. For Bottom
it is `r*n+r*z`. The cap contact is inset by `r*n`; the side contact is offset
by `r*z` into the body. The surface is an exact quarter cylinder; its end
boundaries are exact quarter-circle arcs in planes normal to `t`.

Each end is closed by a planar quarter-disc termination face. The source edge
therefore retains two sharp stubs, and adjacent Profile junctions remain wholly
untouched. Typed descendants include `FilletSurface`, both contact edges, both
endpoint arcs, both termination faces, and both retained sharp edges.

The implementation rejects non-positive radii/clearances, a radius at or above
host thickness, non-Line2 sources, inner loops, loop/chain targets, and spans
that cannot retain both endpoint clearances. For a Compose source, M1 first
tests an exact finite-span/axial corridor conservatively against Shaft and
Counterbore circles. Touching or overlap reports
`ProfileBoundaryFilletIntersectsShaft` or
`ProfileBoundaryFilletIntersectsCounterbore` before topology generation.
Disjoint composed hosts still report `ProfileBoundaryFilletComposeUnsupported`:
the M1 topology route is intentionally bare-Profile only.

`aetheris inspect-profile file.firmament --json` reports the selected target,
radius, clearances/span policy, cylinder axis and centreline, both contact
lines, corridor classification, typed descendants, and provenance chain.

For span `S` and radius `r`, the exact removed volume is
`S * r^2 * (1 - pi/4)`. The end termination faces merely partition the boundary
at the span ends; they add no further removed volume. The cylindrical area is
`S * pi * r / 2`.

The canonical top fixture now declares that analytic oracle directly with
`Assert Volume Body`; M1 compares it after normal materialization and never
uses the assertion to change the fillet route.

Current limitations: no adjacent-junction continuation, convex/reflex patch,
whole-loop or chain finish, inner loop, curve source, variable radius, imported
edge, cavity interaction, or composed Profile host is supported.
