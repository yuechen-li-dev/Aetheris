# Hyperbola curve X1

`Hyperbola3Curve` is the kernel's exact one-branch analytic hyperbola support.
It uses the right-handed frame `(AxisU, AxisV, PlaneNormal)` and the stable
parameterization:

`P(t) = Center + branchSign * A * cosh(t) * AxisU + B * sinh(t) * AxisV`.

The support is unbounded; the authoritative `EdgeGeometryBinding.TrimInterval`
owns each finite B-rep use. Edge orientation remains an `OrientedEdgeSense`
decision, so a shared bounded hyperbola edge has one support and one trim, even
when adjacent face uses traverse it oppositely.

The initial production intersection is deliberately narrow:
`TransverseConePlaneIntersection.IntersectWorldZ` accepts only a `ConeSurface`
whose axis is exactly one of `+X`, `-X`, `+Y`, or `-Y`, and a world-Z plane that
does not contain the apex. With `d = planeZ - apexZ` and cone half-angle `a`,
it produces:

- `Center = (apex.X, apex.Y, planeZ)`
- `AxisU = cone.Axis`
- `AxisV = +Z x AxisU`
- `A = abs(d) / tan(a)`
- `B = abs(d)`
- `Branch = PositiveAxisU`

This is the forward cone sheet (`v >= 0`). Apex planes and non-transverse axes
reject explicitly rather than being misclassified as hyperbolas.

`BrepDisplayTessellator` uses bounded adaptive midpoint-deviation and tangent
turn subdivision. It preserves exact endpoints, but is only a derived display
and M8 carrier: tessellation never replaces `Hyperbola3Curve` in topology.

STEP AP242 uses a real `HYPERBOLA` support plus `TRIMMED_CURVE` parameter
bounds. A negative local branch is serialized by a canonically flipped STEP
placement frame; reimport canonicalizes the physical branch to
`PositiveAxisU` in that exported frame. The geometry and trim direction are
therefore preserved without a rational B-spline fallback.

A known analytic hyperbola must remain a hyperbola through BRep, STEP, and
reimport. A section-stack partition plane may trim a transverse cone with a
hyperbola; that trim is topology, not decoration.

Current limits: parabola, arbitrary conic recovery, rational B-splines,
arbitrary cone/plane orientations, and generic surface/surface intersection
remain outside this route. The next cavity-planner step consumes these exact
supports to form shared cone partition edges; it must not synthesize caps at
section-stack planning levels.
