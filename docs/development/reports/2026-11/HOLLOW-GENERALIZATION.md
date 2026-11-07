# Hollow generalization: design

## Where it stands

`<Hollow>` is admitted on three primitives by name (`RoundedBox`, `Cylinder`, `Frustum`,
`FirmamentV2MechanicalBinding.BindMechanicalHollow`), each with a bespoke planner in
`ThinWalledBodyBRepPlanner`, one opening (`Top`), and one special case (`Cylinder.BottomBlendRadius`).
`Loft<Hollow>` has its own sectional route. `PlasticShell` admits only a coaxial frustum exterior.
`HollowConstructionWitness` is documented as "primitive-owned proof that its inner boundary can be
derived exactly". That ownership is the thing to change.

## Principle

A hollow is not a geometric operation on a finished body. It is a **transformation of the
construction program**: the cavity is built by the same program as the exterior, with every
parameter moved inward by the wall thickness, and removed from it. The witness belongs to the
*generator* (extrude, compose, revolve), not to the primitive, because the generator defines the
space in which the offset is exact:

| Generator | Offset space | Exact? |
| --- | --- | --- |
| Extrude, Compose (section stack) | section plane × axial levels | yes: planes stay planes, arcs stay arcs, levels shift by t |
| Extrude with uniform draft α | section plane, distance t / cos α | yes: drafted planes and cone corners offset to the same classes |
| Revolve | meridian half-plane (r, z) | yes: a surface of revolution's normal lies in the meridian, so the 2D parallel curve revolves to the 3D offset |
| Loft (ruled) | none | no: keeps its sectional "nominal" witness, reported as such |
| Arbitrary B-rep | none | refused; this is the commercial shell problem |

The primitives desugar: `RoundedBox` is an extrude of a rounded rectangle, `Cylinder` an extrude
or revolve of a circle, `Frustum` and the bottom-blended cup a revolve of a line/arc meridian. The
three bespoke planners and `BottomBlendRadius` become instances and are deleted (convergence rule).

This is the same move as rounded rectangles and the thread run-out: the cavity is derived from
intent the compiler already holds, never inferred back from B-rep faces.

## The transformation pass

One pass over the construction IR, before materialization. Every rule is closed-form:

| Exterior construct | Cavity construct |
| --- | --- |
| Profile line | parallel line at distance t inward |
| Convex arc, radius r > t | concentric arc r − t |
| Convex arc, r ≤ t | vanishes; neighbours meet at a sharp vertex (normal: outer radius smaller than wall) |
| Concave arc, radius r | concentric arc r + t |
| Sharp convex vertex | sharp vertex (offset lines intersect) |
| Sharp reflex vertex | arc of radius t about the vertex (true constant thickness; free in 2D) |
| Closed cap at level z | level z ± t |
| Cap named in `Openings` | not offset; the cavity runs through it |
| `Round` stage radius r | r − t (handled by the arc rules) |
| 45° chamfer of size d on a right-angle corner | chamfer of size d − t(2 − √2) (offset planes; exact) |
| Constant `EdgeFinish<Fillet>` r on a convex exterior edge | concentric fillet r − t on the cavity edge, or sharp if r ≤ t |
| Revolve span on the axis | exempt: the axis is not a material boundary |

Section stacks (Compose) apply the 2D rules per slab. Between slabs the cavity's level breaks move
by ±t, and its section in each band is the offset of the intersection of the exterior sections
within ±t of that band (2D arrangement intersection, which `ProfileArrangementBuilder` already
does). The result is again a section stack, so it lowers through the existing Compose route as
`Remove` operations: no new B-rep builder.

## Admission and diagnostics

The pass refuses, with the location named (`plastic-shell-wall-offset-collapse` already exists):

- an offset curve that vanishes or reverses other than a convex arc of r ≤ t;
- a cavity section whose region count or loop nesting differs from the exterior's (a neck
  thinner than 2t would split the cavity);
- a draft that is not uniform over a wall;
- a generator with no offset space (arbitrary B-rep, booleans of imported bodies).

The witness reports, per corresponding face pair, the exact distance and the surface class pair.
Where slab steps meet at a sharp reflex edge the wall is thicker than t (up to √2·t at 90°); that is
reported as a measured ratio, the same thick-section proxy PlasticShell already uses for junctions.

## Ordering

Exterior construction (including its finishes and exterior bosses) → derived cavity → `Modify`
features on the shell (perforations, holes, cut-outs through the wall) → interior features grown
from the cavity (ribs, standoffs, bosses with core holes). The last group is where PlasticShell
lives; its `Exterior` stops being frustum parameters and becomes any `HollowConstructible` model.

## Phases

1. Exact 2D line/arc offset of `ResolvedProfile2D` with collapse detection. Everything else rests on it.
2. `Extrude<Hollow>` and `Compose<Hollow>` through the section stack; `Openings: [Top]`, `[Bottom]`,
   `[Top, Bottom]` (a tube). Desugar `RoundedBox<Hollow>` and `Cylinder<Hollow>`; the existing
   fixtures must produce the same topology and volumes, then the bespoke planners go.
3. `Revolve<Hollow>` through the meridian; desugar `Frustum<Hollow>` and the bottom-blended cup.
4. Finish transfer: constant fillets and chamfers on exterior edges.
5. PlasticShell exterior from any hollow-constructible model; uniform draft.

## Decisions for the author

1. **Reflex corners.** Rolling-ball (arc of radius t, constant thickness) in-plane, as proposed, or
   sharp like commercial shell? Between slabs, sharp is proposed so the cavity stays a section stack.
2. **Exterior features.** A boss declared in the exterior program is hollowed with it (proposed);
   a boss added by `Modify` after the hollow stays solid.

## Decisions taken

Both as proposed: in-plane reflex corners round at the wall (constant thickness), slab steps stay sharp; a
boss in the exterior program is hollowed with it, features added afterwards are not.

## Implementation status

Phases 1 and 2 are in.

- `ProfileInset2D`: exact inset of line/arc regions. Lines parallel, arcs concentric, reflex corners round at t,
  convex arcs of r ≤ t vanish into sharp corners; reversal, self-contact and hole/outer contact are refused.
- `PrismaticHollowCavity`: the cavity is the exterior eroded by a cylinder of radius t and half-height t. Bands
  break at every slab level ± t; each band's section is the inset of the intersection of the exterior sections
  within t of it. Bands governed by the same section merge into one `Remove`. The result lowers through the
  unchanged section-stack emitter.
- `Compose<Hollow>` (`wallThickness`, `openings: [Top|Bottom]`) and `Extrude<Hollow>`, which lowers to a one-`Base`
  hollow Compose.
- Closed vessels are refused: the cavity would be a second (void) shell, which the stack emitter rejects as a
  disconnected solid.

Two defects found and fixed on the way:

- `ProfileArrangementBuilder.ToRegions` gave a hole to every outer loop containing it. An island inside another
  region's hole (the ring of wall around a pocket inside a hollow body) then shared its hole with the region around
  it, and the emitter duplicated those walls. Each hole now belongs to the smallest containing outer.
- `BrepMassProperties`' exact vertical-prism lane decided loop nesting from the average of a loop's vertices. For
  concentric walls that point lies inside the inner wall, so the outer wall was subtracted; with three loops the
  wrong sum stayed positive and was reported as an exact volume (29274 mm³ for an 8128 mm³ tray). Nesting now uses a
  point on the loop against the other loops traced along their arcs. Its arc area also assumed every circle's
  parameter starts at +X; it now uses chord plus segment.

Evidence: rounded tray, enclosure lid with boss and pocket, and L-bracket tray with a through bore build as single
valid solids in OCCT, with volumes equal to closed forms (35369.8, 26302.9, 8127.7 mm³). The rounded tray matches
the bespoke `RoundedBox<Hollow>` planner's volume exactly (26 faces there, 27 here: the stack splits the outer walls
at the floor level).

## Phase 3: primitives retired, Revolve<Hollow>

- The inset moved to Core as `LineArcInset`, the one offset authority. `ProfileInset2D` adapts Firmament profiles
  to it and keeps the region-level checks that need the arrangement.
- `MeridianHollow`: the full axial section is the meridian and its mirror; its inset, cut back to the half-plane,
  is the cavity. An open cap is pushed out by 2t along the straight wall reaching it before the inset, and the
  cavity is cut at the cap's plane, so the opening is exact.
- `BrepRevolvedShell`: a full turn of a closed line/arc meridian with no seam edges. Every face is bounded by the
  circles its span's endpoints sweep. Only radial spans may meet the axis. This is the topology the bespoke
  frustum and cylinder builders hand-wrote.
- `ThinWalledBodyBRepPlanner.CreateFrustum` and `CreateCylinder` (with or without the bottom blend) keep their
  admission and witnesses but take their bodies from `MeridianHollow` + `BrepRevolvedShell`. The four hand-written
  builders are deleted. `RoundedBox<Hollow>` moved to `HollowPrimitives.RoundedBox` on the section stack.
- `Revolve<Hollow>` (`wallThickness`, `openings`, full turn only) maps the Profile into the meridian about its
  axis and takes the same route.

Evidence: the frustum cups, the cylinder body and the blended utensil holder rebuild with face counts and OCCT
volumes identical to the bespoke builders; the rounded enclosures keep their volumes (27 faces instead of 26: the
stack splits the outer walls at the floor level, and `BrepSplitFaceMerge` leaves that body as it is). The bottle and
sleeve match Pappus volumes computed independently (shapely, 512-segment round erosion) to 6 figures.

A third defect surfaced in the round trip: the STEP importer decided a cylinder's or cone's face side from a loop
running once round it as if the face always lay on the loop's +v side. That holds for an upright cup's cone, not
for a bottle's shoulder, whose cone opens downward; the reimported bottle measured 42515 mm³ for 22658. With
another loop on the face, the importer now takes the side from where that loop lies. Five NIST files re-hash; each
kernel volume moves towards OCCT's:

| File | Before | After | OCCT |
| --- | --- | --- | --- |
| FTC-08 | 505510.6 | 503678.9 | 503633.0 |
| FTC-11 | 6861.5 | 5271.3 | 5135.1 |
| STC-10 | 200664.9 | 190412.0 | 188312.8 |
| CTC-05 | 4837978.1 | 9277231.0 | 12694917.8 |
| STC-06 | 94655.2 | 179703.7 | 3323540.5 |

CTC-05 and STC-06 were still far from OCCT; see "Loop orientation and the NIST volumes" below.

## Loop orientation and the NIST volumes

### The perforated holder

The bespoke thin-wall planner bound coedge directions to the surface's own normal XOR'd with the face sense, and
`BrepRevolvedShell` and both radial-cut builders inherited it. STEP (and OCCT) read a loop as counter-clockwise about
the face's outward normal. Planes and cylinders tolerate the wrong winding in OCCT, so their volumes looked right; on a
torus the loop selects the band, and OCCT took the 270° complement of the blend. The blended cup measured 95528.8 mm³
for 46411.3, and the perforated holder came out inside out (−21002 mm³).

Loops now follow the outward normal in `BrepRevolvedShell`, `BrepHollowRadialCut` and `BrepHollowRadialCutPattern`:
each edge is used once each way. The single radial cut refuses bottom-blended hosts; the pattern builder owns those.
`ordning-utensil-holder-blended` measures 39636.9 mm³ in OCCT (blank 40569.2 less 932.4 of vents).

### The NIST volumes

The two outliers had four unrelated import defects, each of which picked the wrong side of a face:

| Defect | Where | Fix |
| --- | --- | --- |
| FACE_OUTER_BOUND on a hole loop (23 planar faces of STC-06) | orientation baseline taken from the declared outer loop | on a plane, the loop enclosing the most area is the outer boundary |
| One loop on a sphere: small cap one way, large cap the other wind alike | baseline assumed the small cap | trust same_sense |
| Two full circles on a torus: quarter tube or three-quarter tube | baseline from u direction alone | trust same_sense |
| Lone full-turn loop on a cone (drill point) | face side not decided | the face holds the apex |
| Two loops each running once round a cylinder or cone, one wavy (a notch in a bore end) | each tessellated as its own polygon, losing the band | stitched into one band polygon at a common seam |

| File | Before | After | OCCT |
| --- | --- | --- | --- |
| STC-06 | 179703.7 | 3323539.5 | 3323540.5 |
| CTC-05 | 9277231.0 | 12726632.0 | 12694917.8 |
| FTC-11 | 5271.3 | 5134.4 | 5135.1 |
| STC-10 | 190412.0 | 188525.9 | 188312.8 |

Those four files re-hash; FTC-08 does not move (503632.9 against OCCT's 503633.0). The two tests
that asserted STC-06 stays orientation-ambiguous now assert it resolves, and to OCCT's volume.

CTC-05's remaining +0.25% is one torus face (a quarter-tube fillet 63° long): the mass-property tessellation hits its
65536-triangle budget before refining the trim, and chord-to-arc slivers along its circular edges add 986 mm² of area.
The display tessellation of the same face matches OCCT's area. It is an estimator limit, not a topology defect.

### Loop direction across the kernel

A sweep of the 278 canonical fixtures that export STEP found edges used the same way twice in 230 of them, starting
with `Basics/box`. The kernel has no shared loop convention: `BrepExtrude` wound both caps about the extrude normal,
`BrepBooleanBoxCylinderHoleBuilder` both caps and their holes about +Z, the gear routes their own way. Nothing in the
kernel reads it (tessellation, mass properties and the importer derive orientation themselves), so only STEP saw it;
OCCT tolerates it on planes and cylinders.

STEP owns the contract, so the writer now derives loop direction (`StepLoopOrientation`). Two loops sharing an edge
must use it in opposite directions; that fixes every loop up to one choice per connected set, which the set's planar
faces settle (signed area about the declared outward normal, each face's largest loop as its outer boundary). After it,
every closed solid in the sweep uses each edge once each way. `BrepExtrude`'s caps are also corrected at source, which
keeps box round-trip text stable.

That pass exposed builders whose faces declare the wrong outward normal, which the inconsistent loops had masked:

- `WireCoils`: the tube's B-spline nets have normals into the wire but were bound aligned. Fixed (bound opposed, loops
  reversed). The STEP importer's B-spline alignment fallback, a polygon-normal test that is meaningless on a face
  turning through more than a right angle, now reads same_sense instead.
- Still open, now reported by the writer as `step-export-face-normal-inverted` (a warning; the CLI does not print
  export warnings): 42 fixtures, by planar face count: sheet-metal lowering (most of the 7–20 counts), structural
  members and joints, PlasticShell rib/standoff faces, `EdgeFinish` end faces of a bounded fillet, the counterbore
  shoulder, `EquationCurves` arch caps, `Revolve/arbitrary-axis`, one section-chain duct. OCCT reorients these on read
  and our importer re-solves them, so volumes are right; a receiving system that trusts same_sense sees faces turned
  inward.

`BrepRevolvedShell` now binds an annulus's larger circle as its outer boundary (the writer had labelled whichever
circle came first).

The 2026-11-06 V8 display meshes re-mesh from different loop start points (the parts round-trip through STEP); all 21
definitions keep their area, signed volume and bounds, and the archive was refrozen (see the fixture README). STC-10
moves to 188525.9 (OCCT 188312.8) with the B-spline fallback.

## Next

- Closed vessels: the cavity is a second shell; the section stack and the revolved shell both need void shells.
- Apexes and poles in `BrepRevolvedShell` (pointed cones, domed bottoms).
- Perforation onto the general hollow body instead of its own cylinder builder.
- Outward normals in the builders listed above.
- Phases 4 and 5 as planned.
