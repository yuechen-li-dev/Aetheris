# Profile reflex fillet junction M3

M3 rounds one source-selected 90-degree inside notch: two adjacent outer
`Line2` Profile segments with a `270deg` material interior angle.  The route is
resolved Profile -> `ProfileFilletShellPlan` -> two `StraightRoll` plans ->
`ReflexFilletJunctionPlan` -> authoritative B-rep; no materialized edge is
searched and no Boolean or generic fillet operation is used.

For the shared source vertex `V`, inward material normals `nA`, `nB`, outward
cap direction `c`, and `into = -c`, each straight roll is unchanged from M1.
At the reflex vertex its generating-ball centres run from
`V + r*nA + r*into` to `V + r*nB + r*into` on a quarter circle of radius `r`
around `O = V + r*into`.  Sweeping the same radius `r` ball yields the bounded
horn-torus patch `Torus(O, c, major=r, minor=r)`.  Its domains are the signed
quarter major interval from `nA` to `nB` and minor interval `[pi/2, pi]`.
The final minor boundary collapses exactly to the retained vertical-notch point
`O`; this admitted singular endpoint is not an unbounded horn-torus feature.

The cap contact is a quarter circle centred at `V`.  The cylinder/torus seams
are exact quarter circles centred at the two endpoint rolling-ball centres.
Each side-plane contact is the shared vertical-notch point.  The two roll seams
and cap contact are exact, but the `major = minor` torus has a pole at that
shared notch point: the major direction, and therefore the analytic normal, is
not unique there.  This is a geometrically exact horn-torus construction, not
a regular `C1` B-rep vertex. Only external start/end termination faces are
emitted.

M3 admits positive radii below host thickness and requires both selected spans
to exceed `EndClearance + Radius`; non-orthogonal reflex pairs fail with
`ProfileBoundaryFilletReflexAngleUnsupported`, and exhausted spans with
`ProfileBoundaryFilletReflexRadiusTooLarge`.  It intentionally does not admit
3+ chains, loops, inner loops, curves, or arbitrary angles.

`profile-fillet-reflex-two-segment-top.firmament` and Bottom demonstrate the
local-frame mirror.  Their assertion literal is `5595.374298764066 mm^3` with
`0.5 mm^3` tolerance.  An independent symbolic/numerical volume derivation is
still required before this value can be promoted beyond its deterministic
regression role.  STEP exports one `TOROIDAL_SURFACE` and Aetheris/OCCT-style
import paths can retain the topology, but this horn-pole trim is currently
**not an external-kernel interchange guarantee**. A regular replacement patch
or a different bounded construction is required before claiming portable M3
support; viewers may fan tessellation or heal/split the pole differently.
