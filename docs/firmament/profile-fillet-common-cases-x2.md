# Profile fillet common cases X2 — current implementation boundary

X2 normalizes authored Profile fillet targets before topology materialization.
The target is source-bound and deterministic:

`Profile ID + outer loop ID + Profile-ordered source segment IDs + Top/Bottom
+ radius + endpoint policy + source provenance`.

Supported target spelling is deliberately the same as existing Profile chamfer
selection spelling:

```firmament
Selection OutsideCorner {
    Source: Bracket.Outer.[South, East]
    Require: ConnectedChain
}
Modify Body {
    EdgeFinish Round { Target: OutsideCorner On: Top Kind: Fillet Radius: 2mm }
}
```

`Target: Bracket.Outer.South` is a `SingleSegment`; `Target: Bracket.Outer` is
a `ClosedLoop`; a `ConnectedChain` selection is normalized in resolved Profile
order even when its source list is reversed. Duplicate and disconnected source
sets fail before planning with `ProfileBoundaryFilletDuplicateSegment` and
`ProfileBoundaryFilletDisconnectedChain`. Inner loops and line/arc targets
remain explicitly unsupported.

## Materialized geometry

The only materialized topology remains M1's finite straight segment. It has an
exact quarter-cylinder, two straight cap/side contact curves, two exact
quarter-circle endpoint arcs, and planar endpoint termination faces. Its local
Top/Bottom frame comes from the Profile construction plane, never global Z.
The radius must be positive, below host thickness, and leave the requested
finite span after both endpoint clearances.

For a 90-degree Profile junction, the required continuous rolling construction
is not two M1 cylinders meeting at the original Profile vertex. The cylinder
centre lines terminate on a radius-offset corner, and a sphere of the same
radius supplies the trihedral cylinder/cap/side transition at a convex corner.
The cylinder/sphere trim is a circle; cap/sphere and side/sphere contacts are
also circular arcs. Reflex material corners have the opposite material-side
selection and require a separately oriented internal spherical trim patch plus
notch-width and local-medial-space admission. Reusing convex face orientation
for a reflex vertex would invert material side, so neither patch is emitted by
M1.

M2 now materializes the first admitted chain: two adjacent 90-degree convex
line segments use two cylinders, one exact sphere, and only their two external
terminations. Reflex pairs and chains longer than two still report their typed
topology boundary; a complete loop reports
`ProfileBoundaryFilletLoopTopologyNotMaterialized`. Composed hosts keep M1's
conservative single-roll Shaft/Counterbore corridor check and report the
feature-specific collision diagnostics before the separate compose boundary.

The next bounded implementation must create one authoritative
`ProfileBoundaryFilletPlan` with typed straight-roll, convex-junction,
reflex-junction, and endpoint-termination subplans, then emit the complete
shell from that plan. It cannot be obtained safely by stitching M1 endpoint
faces or by generic B-rep edge editing.
