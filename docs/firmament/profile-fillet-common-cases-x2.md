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
terminations.  X5 exposes those cylinders and the sharp sphere as reusable
components, and does the equivalent for M3's horn torus and the opt-in
SphereSeamCompatibility sphere.  The finite fixtures retain their external
terminations; reusable components do not own them.  Chains longer than two and
a complete loop still report `ProfileBoundaryFilletLoopTopologyNotMaterialized`
until the parent-owned mixed cap/side composer is emitted. Composed hosts keep M1's
conservative single-roll Shaft/Counterbore corridor check and report the
feature-specific collision diagnostics before the separate compose boundary.

The next bounded implementation composes the extracted components with the X4
rounded-source components into one authoritative closed-loop plan. It cannot
be obtained safely by stitching M1 endpoint faces or by generic B-rep edge
editing. See `fillet-patch-extraction-x5.md`.

## Rounded source boundary

Circular Profile source segments are bound as source semantics but are not yet
materialized by this line-only shell route. X2 classifies them before topology
emission and reports the selected exact planner, family, regularity, and typed
invalid reason through `ProfileBoundaryFilletArcMaterializationNotImplemented`.
Convex rolling has signed major locus `Rs - F` (spindle / sphere-limit / horn
boundaries); reflex rolling has `Rs + F` (regular ring torus for positive
`Rs`). The full code-backed table is in `profile-edgefinish-chimera-closure-x2.md`.
# Profile fillet common cases

Two adjacent outer straight segments at a supported 90-degree reflex vertex
use M3's horn-torus rolling materialization by default. Select the pair as one
`ConnectedChain` and use the ordinary Profile `EdgeFinish` syntax; authors do
not provide torus parameters or trimming information. OpenCascade-family
inspection retains this exact controlled horn endpoint. For a downstream
consumer that cannot retain that endpoint, the explicit
`ReflexJunction: SphereSeamCompatibility` override chooses a deterministic
sphere-seam presentation instead; it never changes the default.
