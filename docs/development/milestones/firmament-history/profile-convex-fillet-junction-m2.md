# Profile convex fillet junction M2

M2 adds one source-bound connected fillet: exactly two adjacent outer `Line2`
segments meeting at one 90-degree `ConvexProfileJunction`. It uses the existing
selection spelling:

```firmament
Selection CornerPair { Source: Bracket.Outer.[South, East] Require: ConnectedChain }
Modify Body { EdgeFinish RoundedCorner { Target: CornerPair On: Top Kind: Fillet Radius: 2mm EndClearance: 3mm } }
```

The route is plan-first: resolved Profile -> `ProfileFilletShellPlan` -> two
typed `StraightRoll` plans -> one `ConvexSphericalJunction` plan -> one B-rep
shell. M1 is still used unchanged for a single segment. M2 applies
`EndClearance` only at the external start of A and external end of B.

For source vertex `V`, inward material normals `nA`, `nB`, local outward cap
normal `c`, and `into = -c`, the same-radius sphere centre is
`V + r*nA + r*nB + r*into`. For the canonical Top XY example this is
`(18, 2, 6)`; Bottom changes only the axial term, giving `(18, 2, 2)`.
Each roll ends at the plane through that centre. Its sphere/cylinder seam is an
exact quarter circle. The sphere has three quarter-circle boundaries: one to
each roll and one to the retained local vertical-corner support. Cap contact is
the shared tangent point; each vertical side contact is a tangent point. The
support is an actual horizontal retained-corner face, not either suppressed M1
endpoint termination face; the original vertical corner remains below the
fillet depth.

The emitted body has two `Cylinder` faces, one `Sphere` face, exact `Circle3`
seams, two external planar termination faces, and no internal termination
descendant. Descendants retain both source segment stable IDs, the shared
Profile vertex, `ConvexJunctionPatch`, both roll/cap/side contacts, and the
two external terminations.

M2 admits only 90-degree convex line-line pairs, positive radii below host
thickness, and `length > EndClearance + Radius` for both source segments.
Reflex, collinear, degenerate, non-orthogonal, whole-loop, and three-or-more
chain routes fail specifically before topology emission. Compose/cavity support
remains M1-only and is intentionally not widened here.

`profile-fillet-convex-two-segment-top.firmament` and Bottom declare an
`Assert Volume` literal of `1570.7317878719023 mm^3` with `0.5 mm^3`
tolerance, matching the deterministic trimmed-face mass evaluator for the
12-face canonical construction. The Concept Path and explicit Segment/Trace
fixtures export byte-identical STEP (`0EE611CCB963620D2ADD6DB94044EFBCD4FDAB142679325BE78758A60DA1462A`).
