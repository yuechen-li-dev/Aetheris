# AIR-FILLET-DIRECT-JUNCTION-M4A

## Corrected conclusion

The M4 spherical investigation was valuable negative evidence but its architectural
conclusion was too broad.  A sphere is the three-edge corner candidate; it is not
required for two equal-radius fillets.  The two selected replacement cylinders
close directly.

## Admitted construction

Only a history-known axis-aligned box with the convex positive corner is admitted:

```text
SharedEdge(+X,+Z), SharedEdge(+Y,+Z), constant equal radius R
```

Feature AIR owns two `AirEdgeFinishFeature` intents.  Construction AIR owns the
two exact `CylinderSurface` replacements and one
`LocalizedEdgeJunctionDirectIntersectionClosure`.  The closure is `Direct`, not
`Patch`; there is no spherical face and no legacy BRep surgery.

Let the positive box corner be `(hx, hy, hz)` and set:

```text
C  = (hx-R, hy-R, hz-R)
A  = cylinder through C, axis +Y, radius R
B  = cylinder through C, axis +X, radius R
```

Their equations are:

```text
(x-cx)^2 + (z-cz)^2 = R^2
(y-cy)^2 + (z-cz)^2 = R^2
```

The material-side branch is `x-cx = y-cy >= 0`, `z-cz >= 0`.  With parameter
`t in [0, pi/2]`, its exact finite seam is:

```text
P(t) = C + (R cos(t), R cos(t), R sin(t))
```

This lies in `x-y = cx-cy` and is an exact planar ellipse: major radius
`sqrt(2) R`, minor radius `R`, plane normal `(1,-1,0)`, major direction
`(1,1,0)`.  It runs from `(hx,hy,hz-R)` to `(hx-R,hy-R,hz)`.  Substitution in
both cylinder equations gives zero containment deviation.  The opposite-sign
branch is outside the selected convex replacement regions and is not admitted.

Each cylinder is trimmed by its remote quarter-circle, two planar tangent
boundaries, and this shared ellipse.  The result is a closed eight-face shell:
six retained/unaffected planes plus two cylinders, 11 vertices, 17 edges, and
one shared ellipse.  No corner patch is present.

For an independent volume cross-check (not the signed-shell volume path), the
two single-edge removals overlap by

```text
R^3 (5/3 - pi/2) = integral[0,R] (R - sqrt(R^2-z^2))^2 dz.
```

Thus the expected retained volume is
`W*D*H - R^2(1-pi/4)(W+D) + R^3(5/3-pi/2)`.  The test suite checks this exact
overlap against a 100,000-interval independent trapezoidal integration.

## Plan, preflight, and STEP

One authoritative `LocalizedEdgeJunctionTopologyPlan` drives the emitter.  Its
shared edge has `DirectJunctionBoundary` and `SharedJunction` roles.  Export
preflight validates the ellipse on both cylinder faces (endpoints and midpoint)
and, additionally, verifies that every edge-curve trim endpoint agrees with its
topology edge endpoint in the binding's orientation.  The latter check caught a
reversed remote-B circular cap binding: although its geometric locus was right,
its parameter direction disagreed with the BRep edge and CAD Assistant folded
the far-end closure.  The cap now has the same direction as its topology edge;
the old binding fails Enforce preflight.  STEP exports the analytic `ELLIPSE` and
two `CYLINDRICAL_SURFACE` supports; no spline or faceting is used.

The CLI report exposes `closure.kind=DirectIntersection`, `curveKind=Ellipse`,
`exact=true`, `sharedEdges=1`, `replacementFaces=2`, and `junctionFaces=0`.
There is one hard-valid plan, so selection is `Direct` and utility scoring remains
dormant.

CAD Assistant opened the regenerated canonical `R=1` and `R=2` AP242 artifacts
in shaded-with-edges mode. Both rendered as continuous rounded box corners with
no gap, inversion, fold, or extra face visible, including the previously bad
remote-B closure. The viewer does not visibly draw the analytic direct seam in
its default display; its exactness remains established by STEP reimport and
two-cylinder containment checks rather than raster appearance alone. Hashes:
`E2CF4D60A5A6CA99AB045CFCA5FABAA7EF933E7DD55866D8433F5FD49A429AEA` (R=1)
and `A4CCE35767C3BC296E0E24C39C3887EDC3B6FFFAF72990AF7D58D50F32770218` (R=2).

## Spherical negative evidence and scope

The former radius-R sphere centered at `C` has a third seam for the unselected
`SharedEdge(+X,+Y)` cylinder.  That is exactly why it is the three-edge equal-
radius corner construction, not the two-edge direct chain construction.

Deferred: unequal/variable radii, mixed finishes, three selected edges, concave
or non-orthogonal/curved supports, imported bodies, long chains, and oversized
radii.  The next separate milestone is the three-edge spherical corner.
