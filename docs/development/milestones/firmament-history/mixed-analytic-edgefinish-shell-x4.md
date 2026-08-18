# Mixed analytic Profile EdgeFinish shell X4

X4 introduces the pre-emission authoritative shell plan for a source-selected,
closed Profile loop. It is intentionally a planning boundary: it records every
analytic patch and every shared curve before a B-rep emitter allocates an edge,
loop, face, shell, or STEP entity. This replaces the former situation in which
the policy card knew the selected surface family but did not preserve enough
neighbouring information to materialize a mixed loop safely.

The release source remains the seven-station chimera with `F = 4 mm`, a
24-mm extrusion, and the base proof `(38240 + 4 - pi) * 24 =
917780.601776314 mm^3`. Its authored outer loop has 17 segments: 12 lines and
five circular arcs. The earlier X1/X3 prose saying 18 segments was a
documentation error; no geometry or station was changed.

## Shell plan

`ProfileEdgeFinishMixedShellPlanner` consumes the resolved outer loop in its
authored order and requires the selected target to be that complete closed
loop. It produces `ProfileEdgeFinishMixedShellPlan` with typed patch variants:

- `PlanarChamferPatch` and `ConicalChamferPatch`;
- `CylindricalFilletPatch`, `SphericalFilletPatch`, and
  `ToroidalFilletPatch`.

Each patch retains its source segment, selected policy, regularity, analytic
frame, contact boundaries, side boundaries, and semantic descendants. The
plan also contains a final last-to-first seam: it has no endpoint terminations.
`ConeApex` and `SphereLimit` are explicit degeneracy records rather than
zero-radius circles or zero-major-radius tori.

## Exact mixed seams

At a tangent line/arc source transition the Chamfer seam is a **line**. With
source endpoint `E`, transition station `zt`, cap station `zc`, and the
source inward normal `n`, it joins `E@zt` to `(E + F*n)@zc`. The plane patch
and the cone patch both contain that generator; their tangent planes agree
because the source tangent is shared. A ConvexMedium endpoint follows the same
line to the finite cone apex.

A regular rounded **concave** source station is not terminated by a planar
right-angle/miter face. It is a single `ConicalChamferPatch` with
`FrustumSector` trim topology: the source circular arc at the transition, the
offset circular arc at the cap, and exactly two generator lines. Those
generator edges are materialized from the owning `PlaneConeSeam` plan records,
including their `same_sense` direction, so they cannot become anonymous
proximity-derived seams. `ReflexSmallArc` is the focused regression: for
`Rs=2, F=4` it has radius-2 and radius-6 circular trims plus the two generators.
This is the same analytic boundary family as the supplied SolidWorks reference
frustum; a visible wedge inside that angular interval is a defect.

The Fillet seam is a **circle**, not a planar termination. It is the common
quarter-circle of the line-roll and circular rolling patch, joining the
side-contact point at `zt` to the cap-contact point at `zc`; its centre is the
corresponding rolling centre. It is represented by `CylinderTorusSeam` for
regular/horn torus patches and `CylinderSphereSeam` for the ConvexMedium sphere
limit. Both seam records explicitly carry source vertex, endpoints, oriented
traversal, predecessor/successor patch ids, and provenance. STEP emission must
map that traversal to `EDGE_CURVE.same_sense`; no seam record permits a
hard-coded value.

The plan preserves policy-derived torus regimes: ConvexLarge is horn and
interop-sensitive (`R=4, r=4`); ReflexSmall/Medium/Large are regular ring tori
(`R=6/8/12, r=4`). ConvexMedium Fillet is the bounded spherical limit.
`SphereSeamCompatibility` remains an opt-in sharp-reflex policy and is not
selected by the whole-loop plan.

## Current completion status

The plan and its focused tests are complete and deterministic. It proves the
release order, the 5 Plane/Cone arc patches, the 4 Torus + 1 Sphere arc fillet
patches, all 10 line/arc Chamfer seams, all 8 cylinder/torus seams, both
cylinder/sphere seams, and both bounded degeneracies before B-rep emission.

The Chamfer emitter now consumes that plan as one shell. Its persistent outputs
are `artifacts/edgefinish/profile-edgefinish-chimera-chamfer.step` and
`artifacts/edgefinish/profile-edgefinish-chimera-chamfer.canonical.step`; both
reimport as one enclosed manifold shell with 26 Plane, 5 Cylinder, and 5 Cone
faces and zero NURBS. The finite ConvexMedium apex is a topological vertex,
not a zero-radius edge.

X5 has extracted the M1/M2/M3 sharp analytic components and records them in
the finite-plan output without granting them ownership of endpoint policy.
See [fillet-patch-extraction-x5.md](fillet-patch-extraction-x5.md).  The
closed-loop emitter remains intentionally unclaimed: preflight demonstrates
that sharp contacts cannot be represented as ordinary source-offset seams.
The next step is the exact parent-owned cap/side composer, without NURBS,
Booleans, post-Brep surgery, or preflight bypass.
