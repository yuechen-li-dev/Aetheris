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
regression role. STEP exports one `TOROIDAL_SURFACE`. Manual OpenCascade-family
smoke inspection in FreeCAD and CAD Assistant retains the intended rounded
corner, including the controlled horn endpoint.

## Compatibility override

The source-bound rolling construction remains the default. A consumer that
cannot preserve the controlled horn endpoint may opt into the conventional
sphere-seam presentation explicitly:

```firmament
EdgeFinish ReflexRound {
    Target: ReflexNotch
    On: Top
    Kind: Fillet
    Radius: 2mm
    ReflexJunction: SphereSeamCompatibility
}
```

This emits a `SPHERICAL_SURFACE` junction presentation while preserving the
same two source-bound rolls and external termination policy. It is a
compatibility choice, not an alternative rolling derivation, and is never
selected implicitly. `profile-fillet-reflex-two-segment-sphere-seam-compatibility.firmament`
is the canonical comparison fixture. A kernel vendor may heal the horn pole
into another topology; that behavior is an importer compatibility limitation,
not a reason to silently replace Aetheris's toroidal default.
