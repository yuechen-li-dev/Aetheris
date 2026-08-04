# AIR-FILLET-JUNCTION-M4 — two-edge equal-radius fillet investigation

## Result

M4 does **not** admit a two-edge fillet STEP route yet.  It closes the former
implicit rejection with a typed Construction AIR investigation and an exact
geometric witness.  No BRep, STEP, or legacy fallback is emitted for this case.

The investigated source domain is a history-known axis-aligned box with:

```text
SharedEdge(+X,+Z), SharedEdge(+Y,+Z), equal constant radius R, convex +X/+Y/+Z corner
```

It validates `10 x 8 x 6, R=1`, `10 x 8 x 6, R=2`, and `12 x 5 x 7, R=1`
up to shared-patch admission.  Zero, oversized, unequal, non-sharing, and
no-history inputs are rejected before topology emission.

## Exact candidate derivation

Let the box's positive corner be `(hx, hy, hz)`.  The two single-edge cylinders
have common radius `R` and axes through

```text
C = (hx-R, hy-R, hz-R)
axis A = +Y   for SharedEdge(+X,+Z)
axis B = +X   for SharedEdge(+Y,+Z)
```

The only simple equal-radius spherical candidate tangent to both cylinders is
the sphere centered at `C` with radius `R`.  Its two shared seams have zero
positional and tangent-plane deviation from the selected cylinders.

However, its remaining exact boundary is the quarter circle in `z = hz-R`
between `(hx, hy-R, hz-R)` and `(hx-R, hy, hz-R)`.  This is precisely the seam
of the unselected radius-`R` cylinder about `+Z` — the fillet for
`SharedEdge(+X,+Y)`.  The sphere meets each retained support plane only at its
one tangency point, so it has no non-degenerate trim curve on `+X`, `+Y`, or
`+Z` with which to close the two-edge shell.

Consequently a spherical/octant patch would covertly materialize a third fillet
and is rejected.  A torus cannot supply the forced spherical tangent seams, and
no other exact supported surface family has been proven to meet the two
cylinders and retained planes with G0/G1 closure.  M4 returns the typed
`CornerPatchSurfaceRequired` error with the sphere center, radius, zero cylinder
tangency deviation, missing third-boundary length `pi*R/2`, and required third
surface as evidence.

## Construction AIR and policy

The compiler constructs the two semantic `AirEdgeFinishFeature` fillet intents
and the immutable spherical candidate witness before rejecting it.  It uses no
topology identifiers in Firmament and produces no BRepPlan because no
hard-valid plan exists.  Candidate count is zero hard-valid plans; utility
scoring is deliberately dormant.

The existing two-edge chamfer remains the sole authoritative
`LocalizedEdgeJunction` BRepPlan route.  It must not be repurposed for fillets.

## Continuity, preflight, STEP, and volume

The rejected sphere candidate has exact G0/G1 cylinder seams (`0` measured
deviation).  The unowned third seam has length `pi*R/2`, so there is no closed,
manifold two-edge shell to preflight or export.  Accordingly no STEP hash,
reimport, CAD Assistant session, or volume claim is produced for M4; exporting
an analytically invalid shell would be false evidence.  Existing sphere and
cylinder containment checks in `BrepExportPreflight` already cover a future
valid analytic plan, but no patch-specific preflight is added until a closing
surface is proven.

## Deferred scope and next milestone

This result does not claim rolling-ball, chain, three-edge, mixed, variable-
radius, concave, curved-support, or imported-body fillets.  The next bounded
milestone should either admit the three equal-radius edges at this corner (where
the derived spherical patch has all three cylindrical seams), or derive and
prove a separate exact two-cylinder-to-support corner surface before adding a
two-edge emission path.
